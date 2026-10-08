using System.Net;
using System.Net.Http.Json;
using System.Data.Common;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Domain;
using PriceSync.Modules.TradingRelationships.Persistence;
using PriceSync.Modules.TradingRelationships.IntegrationTests.TestSupport;

namespace PriceSync.Modules.TradingRelationships.IntegrationTests;

[Collection(TradingRelationshipsPostgreSqlCollection.Name)]
public sealed partial class CreateApiTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 24, 8, 30, 0, TimeSpan.Zero);

    public async Task InitializeAsync() => await fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Create_requires_authentication()
    {
        var supplierId = NewOrganizationId();
        var customerId = NewOrganizationId();
        await using var application = new ApiApplication(
            fixture.ConnectionString,
            null,
            new StubOrganizationAuthorizer(allowed: true),
            new StubOrganizationStatusReader(x => OrganizationStatusResult.Active),
            new FrozenTimeProvider(CreatedAt));
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", supplierId.Value.ToString());

        var response = await client.PostAsJsonAsync(
            "/api/v1/trading-relationships",
            CreateRequest(supplierId, customerId));

        await ApiAssertions.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "authentication_required");
    }

    [Fact]
    public async Task Authorized_party_can_create_an_active_relationship()
    {
        var supplierId = NewOrganizationId();
        var customerId = NewOrganizationId();

        await using var application = CreateApplication();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", supplierId.Value.ToString());

        var response = await client.PostAsJsonAsync(
            "/api/v1/trading-relationships",
            CreateRequest(supplierId, customerId));

        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, responseBody);
        var relationship = await response.Content.ReadFromJsonAsync<JsonElement>();
        relationship.GetProperty("id").GetGuid().Should().NotBeEmpty();
        response.Headers.Location?.ToString().Should().Be(
            $"/api/v1/trading-relationships/{relationship.GetProperty("id").GetGuid()}");
        relationship.GetProperty("supplierId").GetGuid().Should().Be(supplierId.Value);
        relationship.GetProperty("customerId").GetGuid().Should().Be(customerId.Value);
        relationship.GetProperty("status").GetString().Should().Be("active");
        relationship.GetProperty("createdAt").GetDateTimeOffset().Should().Be(CreatedAt);
        relationship.GetProperty("endedAt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Create_requires_an_acting_organization()
    {
        var supplierId = NewOrganizationId();
        var customerId = NewOrganizationId();

        await using var application = CreateApplication();
        using var client = application.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/trading-relationships",
            CreateRequest(supplierId, customerId));

        await ApiAssertions.AssertProblemAsync(response, HttpStatusCode.BadRequest, OrganizationContextErrors.Required);
    }

    [Fact]
    public async Task Unrelated_acting_organization_is_rejected_before_counterparty_validation()
    {
        var supplierId = NewOrganizationId();
        var customerId = NewOrganizationId();
        var statusReader = new StubOrganizationStatusReader();
        var authorizer = new StubOrganizationAuthorizer();

        await using var application = CreateApplication(authorizer, statusReader);
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", NewOrganizationId().Value.ToString());

        var response = await client.PostAsJsonAsync(
            "/api/v1/trading-relationships",
            CreateRequest(supplierId, customerId));

        await ApiAssertions.AssertProblemAsync(response, HttpStatusCode.Forbidden, "forbidden");
        authorizer.Calls.Should().BeEmpty();
        statusReader.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Acting_organization_without_management_capability_is_forbidden_before_status_validation()
    {
        var supplierId = NewOrganizationId();
        var customerId = NewOrganizationId();
        var statusReader = new StubOrganizationStatusReader();
        var authorizer = new StubOrganizationAuthorizer(allowed: false);

        await using var application = CreateApplication(authorizer, statusReader);
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", customerId.Value.ToString());

        var response = await client.PostAsJsonAsync(
            "/api/v1/trading-relationships",
            CreateRequest(supplierId, customerId));

        await ApiAssertions.AssertProblemAsync(response, HttpStatusCode.Forbidden, "forbidden");
        authorizer.Calls.Should().ContainSingle()
            .Which.Should().Be((customerId, OrganizationCapability.ManageTradingRelationships));
        statusReader.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Archived_acting_organization_is_forbidden_before_counterparty_validation()
    {
        var supplierId = NewOrganizationId();
        var customerId = NewOrganizationId();
        var statusReader = new StubOrganizationStatusReader(id =>
            id == supplierId ? OrganizationStatusResult.Archived : OrganizationStatusResult.Active);
        var authorizer = new StubOrganizationAuthorizer(allowed: false);

        await using var application = CreateApplication(authorizer, statusReader);
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", supplierId.Value.ToString());

        var response = await client.PostAsJsonAsync(
            "/api/v1/trading-relationships",
            CreateRequest(supplierId, customerId));

        await ApiAssertions.AssertProblemAsync(response, HttpStatusCode.Forbidden, "forbidden");
        statusReader.Calls.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true, OrganizationStatusResult.Archived, HttpStatusCode.BadRequest, "supplier_organization_ended")]
    [InlineData(true, OrganizationStatusResult.NotFound, HttpStatusCode.NotFound, "supplier_not_found")]
    [InlineData(false, OrganizationStatusResult.Archived, HttpStatusCode.BadRequest, "customer_organization_ended")]
    [InlineData(false, OrganizationStatusResult.NotFound, HttpStatusCode.NotFound, "customer_not_found")]
    public async Task Create_returns_stable_problem_for_unavailable_party(
        bool unavailableSupplier,
        OrganizationStatusResult unavailableStatus,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        var supplierId = NewOrganizationId();
        var customerId = NewOrganizationId();
        var unavailableId = unavailableSupplier ? supplierId : customerId;
        var actingOrganizationId = unavailableSupplier ? customerId : supplierId;
        var statusReader = new StubOrganizationStatusReader(id =>
            id == unavailableId ? unavailableStatus : OrganizationStatusResult.Active);

        await using var application = CreateApplication(statusReader: statusReader);
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", actingOrganizationId.Value.ToString());

        var response = await client.PostAsJsonAsync(
            "/api/v1/trading-relationships",
            CreateRequest(supplierId, customerId));

        await ApiAssertions.AssertProblemAsync(response, expectedStatus, expectedCode);
    }

    [Fact]
    public async Task Create_rejects_a_self_relationship_before_calling_organization_access()
    {
        var organizationId = NewOrganizationId();
        var statusReader = new StubOrganizationStatusReader();
        var authorizer = new StubOrganizationAuthorizer();

        await using var application = CreateApplication(authorizer, statusReader);
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", organizationId.Value.ToString());

        var response = await client.PostAsJsonAsync(
            "/api/v1/trading-relationships",
            CreateRequest(organizationId, organizationId));

        await ApiAssertions.AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed");
        authorizer.Calls.Should().BeEmpty();
        statusReader.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Repeated_create_returns_the_existing_active_relationship()
    {
        var supplierId = NewOrganizationId();
        var customerId = NewOrganizationId();

        await using var application = CreateApplication();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", supplierId.Value.ToString());

        var firstResponse = await client.PostAsJsonAsync(
            "/api/v1/trading-relationships",
            CreateRequest(supplierId, customerId));
        var secondResponse = await client.PostAsJsonAsync(
            "/api/v1/trading-relationships",
            CreateRequest(supplierId, customerId));

        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var first = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();
        var second = await secondResponse.Content.ReadFromJsonAsync<JsonElement>();
        second.GetProperty("id").GetGuid().Should().Be(first.GetProperty("id").GetGuid());
        second.GetProperty("createdAt").GetDateTimeOffset().Should().Be(first.GetProperty("createdAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Create_after_an_ended_relationship_establishes_new_history()
    {
        var supplierId = NewOrganizationId();
        var customerId = NewOrganizationId();
        var historical = TradingRelationship.Create(supplierId, customerId, CreatedAt.AddDays(-2));
        historical.End(CreatedAt.AddDays(-1));

        await using (var context = new TradingRelationshipsDbContext(fixture.CreateOptions()))
        {
            context.TradingRelationships.Add(historical);
            await context.SaveChangesAsync();
        }

        await using var application = CreateApplication();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", customerId.Value.ToString());

        var response = await client.PostAsJsonAsync(
            "/api/v1/trading-relationships",
            CreateRequest(supplierId, customerId));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("id").GetGuid().Should().NotBe(historical.Id.Value);

        await using var verificationContext = new TradingRelationshipsDbContext(fixture.CreateOptions());
        var relationships = await verificationContext.TradingRelationships
            .Where(relationship =>
                relationship.SupplierOrganizationId == supplierId &&
                relationship.CustomerOrganizationId == customerId)
            .ToListAsync();
        relationships.Should().HaveCount(2);
        relationships.Should().ContainSingle(relationship => relationship.Status == TradingRelationshipStatus.Active);
        relationships.Should().ContainSingle(relationship => relationship.Status == TradingRelationshipStatus.Ended);
    }

    [Fact]
    public async Task Reestablishment_does_not_create_new_history_when_counterparty_is_archived()
    {
        var supplierId = NewOrganizationId();
        var customerId = NewOrganizationId();
        var historical = TradingRelationship.Create(supplierId, customerId, CreatedAt.AddDays(-2));
        historical.End(CreatedAt.AddDays(-1));

        await using (var context = new TradingRelationshipsDbContext(fixture.CreateOptions()))
        {
            context.TradingRelationships.Add(historical);
            await context.SaveChangesAsync();
        }

        var statusReader = new StubOrganizationStatusReader(id =>
            id == customerId ? OrganizationStatusResult.Archived : OrganizationStatusResult.Active);
        await using var application = CreateApplication(statusReader: statusReader);
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", supplierId.Value.ToString());

        var response = await client.PostAsJsonAsync(
            "/api/v1/trading-relationships",
            CreateRequest(supplierId, customerId));

        await ApiAssertions.AssertProblemAsync(response, HttpStatusCode.BadRequest, "customer_organization_ended");
        await using var verificationContext = new TradingRelationshipsDbContext(fixture.CreateOptions());
        var relationships = await verificationContext.TradingRelationships.ToListAsync();
        relationships.Should().ContainSingle();
        relationships[0].Id.Should().Be(historical.Id);
        relationships[0].Status.Should().Be(TradingRelationshipStatus.Ended);
    }

    [Fact]
    public async Task Concurrent_create_returns_one_created_and_one_existing_representation()
    {
        var supplierId = NewOrganizationId();
        var customerId = NewOrganizationId();
        var lookupBarrier = new ConcurrentLookupBarrierInterceptor();

        await using var application = CreateApplication(interceptors: [lookupBarrier]);
        using var firstClient = application.CreateClient();
        using var secondClient = application.CreateClient();
        firstClient.DefaultRequestHeaders.Add("organization_id", supplierId.Value.ToString());
        secondClient.DefaultRequestHeaders.Add("organization_id", supplierId.Value.ToString());

        var responses = await Task.WhenAll(
            firstClient.PostAsJsonAsync("/api/v1/trading-relationships", CreateRequest(supplierId, customerId)),
            secondClient.PostAsJsonAsync("/api/v1/trading-relationships", CreateRequest(supplierId, customerId)));

        responses.Select(response => response.StatusCode)
            .Should().BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.OK]);

        var representations = await Task.WhenAll(
            responses.Select(response => response.Content.ReadFromJsonAsync<JsonElement>()));
        representations.Select(item => item.GetProperty("id").GetGuid()).Distinct().Should().ContainSingle();
    }

    private ApiApplication CreateApplication(
        IOrganizationAuthorizer? authorizer = null,
        IOrganizationStatusReader? statusReader = null,
        PersonId? personId = null,
        IReadOnlyCollection<IInterceptor>? interceptors = null)
    {
        return new ApiApplication(
            fixture.ConnectionString,
            personId ?? new PersonId(Guid.CreateVersion7()),
            authorizer ?? new StubOrganizationAuthorizer(allowed: true),
            statusReader ?? new StubOrganizationStatusReader(x => OrganizationStatusResult.Active),
            new FrozenTimeProvider(CreatedAt),
            interceptors);
    }

    private static object CreateRequest(OrganizationId supplierId, OrganizationId customerId) => new
    {
        supplierOrganizationId = supplierId.Value,
        customerOrganizationId = customerId.Value,
    };

    private static OrganizationId NewOrganizationId() => new(Guid.CreateVersion7());

    private sealed class ConcurrentLookupBarrierInterceptor : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _bothLookupsStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _lookupCount;

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("TradingRelationships", StringComparison.Ordinal))
            {
                var lookupNumber = Interlocked.Increment(ref _lookupCount);
                if (lookupNumber <= 2)
                {
                    if (lookupNumber == 2)
                    {
                        _bothLookupsStarted.SetResult();
                    }

                    await _bothLookupsStarted.Task.WaitAsync(cancellationToken);
                }
            }

            return result;
        }
    }
}
