using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Domain;
using PriceSync.Modules.TradingRelationships.Persistence;
using PriceSync.Modules.TradingRelationships.IntegrationTests.TestSupport;

namespace PriceSync.Modules.TradingRelationships.IntegrationTests;

[Collection(TradingRelationshipsPostgreSqlCollection.Name)]
public sealed class CreateApiTests(PostgreSqlFixture fixture) : IAsyncLifetime
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
        var saveBarrier = new ConcurrentCreateSaveBarrierInterceptor();

        await using var application = CreateApplication(interceptors: [saveBarrier]);
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
        var relationshipId = representations.Select(item => item.GetProperty("id").GetGuid())
            .Distinct().Should().ContainSingle().Which;

        await using var verificationContext = new TradingRelationshipsDbContext(fixture.CreateOptions());
        var activeRelationships = await verificationContext.TradingRelationships
            .Where(x => x.SupplierOrganizationId == supplierId &&
                x.CustomerOrganizationId == customerId &&
                x.Status == TradingRelationshipStatus.Active)
            .ToListAsync();
        activeRelationships.Should().ContainSingle()
            .Which.Id.Value.Should().Be(relationshipId);
    }

    [Fact]
    public async Task Concurrent_create_returns_ok_when_winner_ends_before_conflict_readback()
    {
        var supplierId = NewOrganizationId();
        var customerId = NewOrganizationId();
        var barrier = new ConcurrentCreateSaveBarrierInterceptor();
        var endWinner = new EndWinnerOnSaveFailureInterceptor();
        await using var application = CreateApplication(interceptors: [barrier, endWinner]);
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", supplierId.Value.ToString());
        endWinner.EndWinner = async () =>
        {
            await using var context = new TradingRelationshipsDbContext(fixture.CreateOptions());
            var winner = await context.TradingRelationships.SingleAsync();
            var response = await client.PostAsync($"/api/v1/trading-relationships/{winner.Id.Value}/end", null);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        };

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/v1/trading-relationships", CreateRequest(supplierId, customerId)),
            client.PostAsJsonAsync("/api/v1/trading-relationships", CreateRequest(supplierId, customerId)));

        responses.Select(x => x.StatusCode).Should().BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.OK]);
        endWinner.Calls.Should().Be(1);
        var created = await responses.Single(x => x.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<JsonElement>();
        var replay = await responses.Single(x => x.StatusCode == HttpStatusCode.OK).Content.ReadFromJsonAsync<JsonElement>();
        replay.GetProperty("id").GetGuid().Should().Be(created.GetProperty("id").GetGuid());
        replay.GetProperty("status").GetString().Should().Be("ended");
        await using var verification = new TradingRelationshipsDbContext(fixture.CreateOptions());
        (await verification.TradingRelationships.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("created", "Succeeded", "Created", HttpStatusCode.Created)]
    [InlineData("existing", "Succeeded", "Existed", HttpStatusCode.OK)]
    [InlineData("missing-actor", "Denied", "ActingOrganizationRequired", HttpStatusCode.BadRequest)]
    [InlineData("unrelated", "Denied", "Forbidden", HttpStatusCode.Forbidden)]
    [InlineData("forbidden", "Denied", "Forbidden", HttpStatusCode.Forbidden)]
    [InlineData("archived", "Denied", "CustomerOrganizationEnded", HttpStatusCode.BadRequest)]
    [InlineData("missing", "Denied", "CustomerNotFound", HttpStatusCode.NotFound)]
    [InlineData("invalid", "Denied", "ValidationFailed", HttpStatusCode.BadRequest)]
    [InlineData("failure", "Failed", "TechnicalFailure", HttpStatusCode.InternalServerError)]
    public async Task Create_audits_actor_pair_and_outcome(
        string scenario, string outcome, string reason, HttpStatusCode expectedStatus)
    {
        var supplierId = NewOrganizationId();
        var customerId = scenario == "invalid" ? supplierId : NewOrganizationId();
        var personId = new PersonId(Guid.CreateVersion7());
        var actorId = scenario == "unrelated" ? NewOrganizationId() : supplierId;
        var logs = new RecordingLogProvider();
        if (scenario == "existing")
            await fixture.CreateTradingRelationshipAsync(CreatedAt, supplierId, customerId);
        await using var application = CreateApplication(
            authorizer: new StubOrganizationAuthorizer(allowed: scenario != "forbidden"),
            statusReader: new StubOrganizationStatusReader(id =>
                scenario == "failure" ? throw new InvalidOperationException("Unavailable") :
                id == customerId && scenario == "archived" ? OrganizationStatusResult.Archived :
                id == customerId && scenario == "missing" ? OrganizationStatusResult.NotFound :
                OrganizationStatusResult.Active),
            personId: personId);
        using var loggedApplication = application.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = loggedApplication.CreateClient();
        if (scenario != "missing-actor")
            client.DefaultRequestHeaders.Add("organization_id", actorId.Value.ToString());

        var response = await client.PostAsJsonAsync(
            "/api/v1/trading-relationships", CreateRequest(supplierId, customerId));

        response.StatusCode.Should().Be(expectedStatus);
        var entry = logs.Entries.Should().ContainSingle().Subject;
        entry["Operation"].Should().Be("CreateTradingRelationship");
        entry["PersonId"].Should().Be(personId.Value);
        entry["OrganizationId"].Should().Be(scenario == "missing-actor" ? null : actorId.Value);
        entry["SupplierOrganizationId"].Should().Be(supplierId.Value);
        entry["CustomerOrganizationId"].Should().Be(customerId.Value);
        entry["Outcome"].Should().Be(outcome);
        entry["Reason"].Should().Be(reason);
        entry["TraceId"].Should().BeOfType<string>().Which.Should().NotBeNullOrEmpty();
    }

    private sealed class EndWinnerOnSaveFailureInterceptor : SaveChangesInterceptor
    {
        public Func<Task> EndWinner { get; set; } = null!;
        public int Calls { get; private set; }

        public override async Task SaveChangesFailedAsync(
            DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            Calls++;
            await EndWinner();
        }
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

    private sealed class ConcurrentCreateSaveBarrierInterceptor : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _bothCreatesReady = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _saveCount;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var isCreatingRelationship = eventData.Context!.ChangeTracker
                .Entries<TradingRelationship>()
                .Any(entry => entry.State == EntityState.Added);

            if (isCreatingRelationship)
            {
                var saveNumber = Interlocked.Increment(ref _saveCount);
                if (saveNumber <= 2)
                {
                    if (saveNumber == 2)
                    {
                        _bothCreatesReady.TrySetResult();
                    }

                    // Hold both concurrent creates at the save boundary so their INSERTs race.
                    await _bothCreatesReady.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
                }
            }

            return result;
        }
    }
}
