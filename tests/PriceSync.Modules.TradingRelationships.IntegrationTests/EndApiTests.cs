using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.IntegrationTests.TestSupport;

namespace PriceSync.Modules.TradingRelationships.IntegrationTests;

[Collection(TradingRelationshipsPostgreSqlCollection.Name)]
public sealed partial class EndApiTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 24, 8, 30, 0, TimeSpan.Zero);
    private readonly FrozenTimeProvider _clock = new(CreatedAt);
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Either_authorized_party_can_end_a_relationship_and_preserve_its_history(bool supplierActs)
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var supplier = Guid.CreateVersion7();
        var customer = Guid.CreateVersion7();
        client.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
        var created = await CreateRelationshipAsync(client, supplier, customer);
        client.DefaultRequestHeaders.Remove("organization_id");
        client.DefaultRequestHeaders.Add("organization_id", (supplierActs ? supplier : customer).ToString());
        _clock.UtcNow = CreatedAt.AddDays(1);

        var response = await client.DeleteAsync(Url(created.GetProperty("id").GetGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var ended = await response.Content.ReadFromJsonAsync<JsonElement>();
        ended.GetProperty("id").GetGuid().Should().Be(created.GetProperty("id").GetGuid());
        ended.GetProperty("supplierId").GetGuid().Should().Be(supplier);
        ended.GetProperty("customerId").GetGuid().Should().Be(customer);
        ended.GetProperty("createdAt").GetDateTimeOffset().Should().Be(CreatedAt);
        ended.GetProperty("status").GetString().Should().Be("ended");
        ended.GetProperty("endedAt").GetDateTimeOffset().Should().Be(_clock.UtcNow);
    }

    [Fact]
    public async Task Repeated_end_preserves_the_original_timestamp_after_time_advances()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var supplier = Guid.CreateVersion7();
        client.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
        var created = await CreateRelationshipAsync(client, supplier, Guid.CreateVersion7());
        var url = Url(created.GetProperty("id").GetGuid());
        _clock.UtcNow = CreatedAt.AddDays(1);
        var firstResponse = await client.DeleteAsync(url);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var first = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();
        _clock.UtcNow = CreatedAt.AddDays(2);

        var secondResponse = await client.DeleteAsync(url);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await secondResponse.Content.ReadFromJsonAsync<JsonElement>();
        second.GetRawText().Should().Be(first.GetRawText());
        second.GetProperty("endedAt").GetDateTimeOffset().Should().Be(CreatedAt.AddDays(1));
    }
    [Theory]
    [InlineData("unauthenticated", HttpStatusCode.Unauthorized, "authentication_required")]
    [InlineData("no_organization", HttpStatusCode.BadRequest, "acting_organization_required")]
    [InlineData("no_capability", HttpStatusCode.Forbidden, "forbidden")]
    [InlineData("unrelated", HttpStatusCode.NotFound, "not_found")]
    [InlineData("archived_actor", HttpStatusCode.Forbidden, "forbidden")]
    public async Task Rejected_end_leaves_the_relationship_active(string scenario, HttpStatusCode expectedStatus, string code)
    {
        var supplier = Guid.CreateVersion7();
        var customer = Guid.CreateVersion7();
        await using var ownerApplication = CreateApplication();
        using var owner = ownerApplication.CreateClient();
        owner.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
        var created = await CreateRelationshipAsync(owner, supplier, customer);
        var url = Url(created.GetProperty("id").GetGuid());
        await using var deniedApplication = CreateApplication(
            allowed: scenario is not ("no_capability" or "archived_actor"), authenticated: scenario != "unauthenticated",
            status: id => scenario == "archived_actor" && id.Value == supplier
                ? OrganizationStatusResult.Archived : OrganizationStatusResult.Active);
        using var denied = deniedApplication.CreateClient();
        if (scenario != "no_organization")
            denied.DefaultRequestHeaders.Add("organization_id", (scenario == "unrelated" ? Guid.CreateVersion7() : supplier).ToString());

        var response = await denied.DeleteAsync(url);

        await ApiAssertions.AssertProblemAsync(response, expectedStatus, code);
        var stillActiveResponse = await owner.PostAsJsonAsync("/api/v1/trading-relationships", new
        {
            supplierOrganizationId = supplier, customerOrganizationId = customer,
        });
        stillActiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var stillActive = await stillActiveResponse.Content.ReadFromJsonAsync<JsonElement>();
        stillActive.GetRawText().Should().Be(created.GetRawText());
    }
    [Theory]
    [InlineData(false, HttpStatusCode.NotFound, "not_found")]
    [InlineData(true, HttpStatusCode.BadRequest, "validation_failed")]
    public async Task End_rejects_unknown_or_empty_relationship_id(bool empty, HttpStatusCode expectedStatus, string code)
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", Guid.CreateVersion7().ToString());
        var response = await client.DeleteAsync(Url(empty ? Guid.Empty : Guid.CreateVersion7()));
        await ApiAssertions.AssertProblemAsync(response, expectedStatus, code);
    }
    [Fact]
    public async Task Active_party_can_end_a_relationship_with_an_archived_counterparty()
    {
        var supplier = Guid.CreateVersion7();
        var customer = Guid.CreateVersion7();
        await using var setupApplication = CreateApplication();
        using var setup = setupApplication.CreateClient();
        setup.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
        var created = await CreateRelationshipAsync(setup, supplier, customer);
        await using var application = CreateApplication(status: id =>
            id.Value == customer ? OrganizationStatusResult.Archived : OrganizationStatusResult.Active);
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", supplier.ToString());

        var response = await client.DeleteAsync(Url(created.GetProperty("id").GetGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ended = await response.Content.ReadFromJsonAsync<JsonElement>();
        ended.GetProperty("status").GetString().Should().Be("ended");
    }
    [Fact]
    public async Task Concurrent_end_returns_the_same_persisted_transition_timestamp_to_both_parties()
    {
        var supplier = Guid.CreateVersion7();
        var customer = Guid.CreateVersion7();
        await using var setupApplication = CreateApplication();
        using var setup = setupApplication.CreateClient();
        setup.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
        var created = await CreateRelationshipAsync(setup, supplier, customer);
        var url = Url(created.GetProperty("id").GetGuid());
        var barrier = new EndLookupBarrier();
        var firstTime = CreatedAt.AddDays(1);
        var secondTime = CreatedAt.AddDays(2);
        await using var firstApplication = CreateApplication(interceptors: [barrier], clock: new FrozenTimeProvider(firstTime));
        await using var secondApplication = CreateApplication(interceptors: [barrier], clock: new FrozenTimeProvider(secondTime));
        using var firstClient = firstApplication.CreateClient();
        using var secondClient = secondApplication.CreateClient();
        firstClient.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
        secondClient.DefaultRequestHeaders.Add("organization_id", customer.ToString());

        var responses = await Task.WhenAll(firstClient.DeleteAsync(url), secondClient.DeleteAsync(url));

        foreach (var response in responses)
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var representations = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<JsonElement>()));
        representations[0].GetRawText().Should().Be(representations[1].GetRawText());
        var timestamp = representations[0].GetProperty("endedAt").GetDateTimeOffset();
        timestamp.Should().BeOneOf(firstTime, secondTime);
        _clock.UtcNow = CreatedAt.AddDays(3);
        var repeatedResponse = await setup.DeleteAsync(url);
        repeatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var repeated = await repeatedResponse.Content.ReadFromJsonAsync<JsonElement>();
        repeated.GetProperty("endedAt").GetDateTimeOffset().Should().Be(timestamp);
    }

    // Both initial reads finish before either update; later readbacks must not wait.
    private sealed class EndLookupBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _bothRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _readCount;
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("TradingRelationships", StringComparison.Ordinal))
            {
                var count = Interlocked.Increment(ref _readCount);
                if (count == 2) _bothRead.TrySetResult();
                if (count <= 2)
                    await _bothRead.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }
    [Fact]
    public async Task Reestablishment_creates_a_new_identity_and_preserves_the_ended_relationship()
    {
        var supplier = Guid.CreateVersion7();
        var customer = Guid.CreateVersion7();
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
        var original = await CreateRelationshipAsync(client, supplier, customer);
        var originalUrl = Url(original.GetProperty("id").GetGuid());
        _clock.UtcNow = CreatedAt.AddDays(1);
        var endResponse = await client.DeleteAsync(originalUrl);
        endResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var ended = await endResponse.Content.ReadFromJsonAsync<JsonElement>();
        _clock.UtcNow = CreatedAt.AddDays(2);

        var reestablished = await CreateRelationshipAsync(client, supplier, customer);

        reestablished.GetProperty("id").GetGuid().Should().NotBe(original.GetProperty("id").GetGuid());
        reestablished.GetProperty("status").GetString().Should().Be("active");
        var historyResponse = await client.DeleteAsync(originalUrl);
        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await historyResponse.Content.ReadFromJsonAsync<JsonElement>();
        history.GetRawText().Should().Be(ended.GetRawText());
    }
    [Fact]
    public async Task End_logs_actor_relationship_and_distinguishes_repeated_success()
    {
        var logs = new RecordingLogProvider();
        var supplier = Guid.CreateVersion7();
        await using var application = CreateApplication();
        using var loggedApplication = application.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = loggedApplication.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
        var created = await CreateRelationshipAsync(client, supplier, Guid.CreateVersion7());
        var relationshipId = created.GetProperty("id").GetGuid();
        _clock.UtcNow = CreatedAt.AddDays(1);

        var first = await client.DeleteAsync(Url(relationshipId));
        var repeated = await client.DeleteAsync(Url(relationshipId));

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        repeated.StatusCode.Should().Be(HttpStatusCode.OK);
        var entries = logs.Entries.ToArray();
        entries.Should().HaveCount(2);
        entries.Select(entry => entry["Reason"]).Should().Equal("Ended", "AlreadyEnded");
        foreach (var entry in entries)
        {
            entry["Operation"].Should().Be("EndTradingRelationship");
            entry["Outcome"].Should().Be("Succeeded");
            entry["PersonId"].Should().BeOfType<Guid>().Which.Should().NotBeEmpty();
            entry["OrganizationId"].Should().Be(supplier);
            entry["TradingRelationshipId"].Should().Be(relationshipId);
            entry["EndedAt"].Should().Be(_clock.UtcNow);
            entry["TraceId"].Should().BeOfType<string>().Which.Should().NotBeNullOrEmpty();
        }
        entries[0]["PersonId"].Should().Be(entries[1]["PersonId"]);
    }

    [Theory]
    [InlineData(false, HttpStatusCode.Forbidden, "Denied", "AuthorizationDenied")]
    [InlineData(true, HttpStatusCode.InternalServerError, "Failed", "TechnicalFailure")]
    public async Task Unsuccessful_end_logs_its_context_and_preserves_the_error_response(
        bool technicalFailure, HttpStatusCode expectedStatus, string outcome, string reason)
    {
        var logs = new RecordingLogProvider();
        var organizationId = Guid.CreateVersion7();
        var relationshipId = Guid.CreateVersion7();
        await using var application = CreateApplication(allowed: technicalFailure,
            status: _ => throw new InvalidOperationException("Status service unavailable."));
        using var loggedApplication = application.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = loggedApplication.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", organizationId.ToString());

        var response = await client.DeleteAsync(Url(relationshipId));

        response.StatusCode.Should().Be(expectedStatus);
        var entry = logs.Entries.Should().ContainSingle().Subject;
        entry["Outcome"].Should().Be(outcome);
        entry["Reason"].Should().Be(reason);
        entry["PersonId"].Should().BeOfType<Guid>().Which.Should().NotBeEmpty();
        entry["OrganizationId"].Should().Be(organizationId);
        entry["TradingRelationshipId"].Should().Be(relationshipId);
        if (technicalFailure)
            entry["ErrorType"].Should().Be(typeof(InvalidOperationException).FullName);
    }

    [Theory]
    [InlineData(OrganizationStatusResult.Archived)]
    [InlineData(OrganizationStatusResult.NotFound)]
    public async Task Actor_becoming_unavailable_after_authorization_is_forbidden_and_leaves_relationship_active(
        OrganizationStatusResult status)
    {
        var supplier = Guid.CreateVersion7();
        var customer = Guid.CreateVersion7();
        await using var setupApplication = CreateApplication();
        using var setup = setupApplication.CreateClient();
        setup.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
        var created = await CreateRelationshipAsync(setup, supplier, customer);
        await using var application = CreateApplication(status: _ => status);
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", supplier.ToString());

        var response = await client.DeleteAsync(Url(created.GetProperty("id").GetGuid()));

        await ApiAssertions.AssertProblemAsync(response, HttpStatusCode.Forbidden, "forbidden");
        var existingResponse = await setup.PostAsJsonAsync("/api/v1/trading-relationships", new
        {
            supplierOrganizationId = supplier, customerOrganizationId = customer,
        });
        existingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var existing = await existingResponse.Content.ReadFromJsonAsync<JsonElement>();
        existing.GetRawText().Should().Be(created.GetRawText());
    }

    [Fact]
    public async Task Unknown_and_out_of_scope_relationships_have_the_same_public_problem()
    {
        var supplier = Guid.CreateVersion7();
        await using var application = CreateApplication();
        using var owner = application.CreateClient();
        owner.DefaultRequestHeaders.Add("organization_id", supplier.ToString());
        var created = await CreateRelationshipAsync(owner, supplier, Guid.CreateVersion7());
        using var outsider = application.CreateClient();
        outsider.DefaultRequestHeaders.Add("organization_id", Guid.CreateVersion7().ToString());

        var knownResponse = await outsider.DeleteAsync(Url(created.GetProperty("id").GetGuid()));
        var unknownResponse = await outsider.DeleteAsync(Url(Guid.CreateVersion7()));

        knownResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unknownResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var known = await knownResponse.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        var unknown = await unknownResponse.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        known!["code"].GetString().Should().Be("not_found");
        known.Remove("traceId");
        unknown!.Remove("traceId");
        known.ToDictionary(item => item.Key, item => item.Value.GetRawText()).Should()
            .BeEquivalentTo(unknown.ToDictionary(item => item.Key, item => item.Value.GetRawText()));
    }

    [Fact]
    public async Task Invalid_end_logs_validation_denial_with_actor_and_request_context()
    {
        var logs = new RecordingLogProvider();
        var organizationId = Guid.CreateVersion7();
        await using var application = CreateApplication();
        using var loggedApplication = application.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = loggedApplication.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", organizationId.ToString());

        var response = await client.DeleteAsync(Url(Guid.Empty));

        await ApiAssertions.AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed");
        var entry = logs.Entries.Should().ContainSingle().Subject;
        entry["Operation"].Should().Be("EndTradingRelationship");
        entry["Outcome"].Should().Be("Denied");
        entry["Reason"].Should().Be("ValidationFailed");
        entry["PersonId"].Should().BeOfType<Guid>().Which.Should().NotBeEmpty();
        entry["OrganizationId"].Should().Be(organizationId);
        entry["TradingRelationshipId"].Should().Be(Guid.Empty);
        entry["TraceId"].Should().BeOfType<string>().Which.Should().NotBeNullOrEmpty();
    }

    private ApiApplication CreateApplication(bool allowed = true, bool authenticated = true,
        Func<OrganizationId, OrganizationStatusResult>? status = null,
        IReadOnlyCollection<IInterceptor>? interceptors = null, TimeProvider? clock = null) => new(
        fixture.ConnectionString,
        authenticated ? new PersonId(Guid.CreateVersion7()) : null,
        new StubOrganizationAuthorizer(allowed), new StubOrganizationStatusReader(status ?? (x => OrganizationStatusResult.Active)), clock ?? _clock, interceptors);

    private static string Url(Guid id) => $"/api/v1/trading-relationships/{id}";

    private static async Task<JsonElement> CreateRelationshipAsync(HttpClient client, Guid supplier, Guid customer)
    {
        var response = await client.PostAsJsonAsync("/api/v1/trading-relationships", new
        {
            supplierOrganizationId = supplier, customerOrganizationId = customer,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

  
}
