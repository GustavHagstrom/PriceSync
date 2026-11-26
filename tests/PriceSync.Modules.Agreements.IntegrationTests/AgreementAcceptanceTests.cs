using System.Data.Common;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.TradingRelationships.Contracts;
using PriceSync.Modules.TradingRelationships.Persistence;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;
using PriceSync.Modules.OrganizationAccess.Persistence;

namespace PriceSync.Modules.Agreements.IntegrationTests;

public sealed partial class AgreementApiTests
{
    [Fact]
    public async Task Customer_owner_can_accept_and_read_an_exact_published_version()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        using var published = await supplier.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(scope.List));
        published.StatusCode.Should().Be(HttpStatusCode.Created);
        var url = $"{published.Headers.Location}/acceptance";
        var owner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        await using var customerApp = new ApiApplication(fixture.ConnectionString, owner);
        using var customer = AcceptanceClient(customerApp, scope.Customer);

        var before = await customer.GetFromJsonAsync<JsonElement>(url);
        before.GetProperty("accepted").GetBoolean().Should().BeFalse();
        using var response = await customer.PostAsync(url, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var acceptance = await response.Content.ReadFromJsonAsync<JsonElement>();
        acceptance.GetProperty("accepted").GetBoolean().Should().BeTrue();
        acceptance.GetProperty("acceptedByPersonId").GetGuid().Should().Be(owner.Value);
        acceptance.GetProperty("acceptedAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        JsonElement.DeepEquals(acceptance, await customer.GetFromJsonAsync<JsonElement>(url)).Should().BeTrue();
    }

    [Fact]
    public async Task Concurrent_acceptance_and_retries_preserve_the_first_person_and_timestamp()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        using var published = await supplier.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(scope.List));
        published.StatusCode.Should().Be(HttpStatusCode.Created);
        var url = $"{published.Headers.Location}/acceptance";
        var firstOwner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        var secondOwner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        var gate = new AcceptanceInsertGate();
        await using var firstApp = new ApiApplication(fixture.ConnectionString, firstOwner);
        await using var secondApp = new ApiApplication(fixture.ConnectionString, secondOwner);
        await using var guardedFirst = WithAcceptanceGate(firstApp, gate);
        await using var guardedSecond = WithAcceptanceGate(secondApp, gate);
        using var first = guardedFirst.CreateClient();
        using var second = guardedSecond.CreateClient();
        first.DefaultRequestHeaders.Add("organization_id", scope.Customer.ToString());
        second.DefaultRequestHeaders.Add("organization_id", scope.Customer.ToString());

        var responses = await Task.WhenAll(first.PostAsync(url, null), second.PostAsync(url, null));
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var winner = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();
        new[] { firstOwner.Value, secondOwner.Value }.Should().Contain(winner.GetProperty("acceptedByPersonId").GetGuid());
        JsonElement.DeepEquals(winner, await secondResponse.Content.ReadFromJsonAsync<JsonElement>()).Should().BeTrue();
        using var retry = await first.PostAsync(url, null);
        retry.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement.DeepEquals(winner, await retry.Content.ReadFromJsonAsync<JsonElement>()).Should().BeTrue();
        JsonElement.DeepEquals(winner, await supplier.GetFromJsonAsync<JsonElement>(url)).Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Members_can_read_acceptance_history_after_the_relationship_ends(bool customerParty)
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        using var published = await supplier.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(scope.List));
        published.StatusCode.Should().Be(HttpStatusCode.Created);
        var url = $"{published.Headers.Location}/acceptance";
        var owner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        await using var customerApp = new ApiApplication(fixture.ConnectionString, owner);
        using var customer = AcceptanceClient(customerApp, scope.Customer);
        using var accepted = await customer.PostAsync(url, null);
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);

        var publisher = new OrganizationId(Guid.Parse(supplier.DefaultRequestHeaders.GetValues("organization_id").Single()));
        var acting = customerParty ? scope.Customer : publisher;
        var member = await AddAcceptanceActor(acting, OrganizationRole.Member);
        await using var readerApp = new ApiApplication(fixture.ConnectionString, member);
        using var reader = AcceptanceClient(readerApp, acting);
        using var forbidden = await reader.PostAsync(url, null);
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await using (var db = new TradingRelationshipsDbContext(fixture.CreateRelationshipOptions()))
        {
            (await db.TradingRelationships.SingleAsync()).End(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        using var ended = await customer.PostAsync(url, null);
        ended.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ended.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
            .Should().Be("agreement_relationship_ended");
        JsonElement.DeepEquals(await accepted.Content.ReadFromJsonAsync<JsonElement>(),
            await reader.GetFromJsonAsync<JsonElement>(url)).Should().BeTrue();
    }

    [Fact]
    public async Task Acceptance_hides_versions_outside_the_requested_scope()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        using var published = await supplier.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(scope.List));
        published.StatusCode.Should().Be(HttpStatusCode.Created);
        var url = $"{published.Headers.Location}/acceptance";
        var owner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        await using var customerApp = new ApiApplication(fixture.ConnectionString, owner);
        using var customer = AcceptanceClient(customerApp, scope.Customer);
        var stranger = new PersonId(Guid.CreateVersion7());
        var unrelated = await SeedOrganization(stranger, "5560004615");
        await using var strangerApp = new ApiApplication(fixture.ConnectionString, stranger);
        using var outsider = AcceptanceClient(strangerApp, unrelated);

        using var unknown = await customer.PostAsync($"{scope.Agreement}/versions/{Guid.CreateVersion7()}/acceptance", null);
        using var wrongAgreement = await customer.PostAsync(
            url.Replace(scope.Agreement, $"/api/v1/agreements/{Guid.CreateVersion7()}", StringComparison.Ordinal), null);
        using var supplierWrite = await supplier.PostAsync(url, null);
        using var outsiderWrite = await outsider.PostAsync(url, null);
        using var outsiderRead = await outsider.GetAsync(url);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await ProblemWithoutTrace(unknown);
        foreach (var denied in new[] { wrongAgreement, supplierWrite, outsiderWrite, outsiderRead })
        {
            denied.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await ProblemWithoutTrace(denied)).Should().Be(problem);
        }
    }

    [Theory]
    [InlineData("1900-01-01", "1900-12-31")]
    [InlineData("2999-01-01", null)]
    public async Task Withdrawn_versions_can_be_accepted_outside_their_validity(string from, string? to)
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        var terms = Publication(scope.List);
        terms["validFrom"] = from;
        terms["validTo"] = to;
        using var published = await supplier.PostAsJsonAsync($"{scope.Agreement}/versions", terms);
        published.StatusCode.Should().Be(HttpStatusCode.Created);
        using var withdrawal = await supplier.PostAsJsonAsync($"{published.Headers.Location}/withdraw", new { reason = "Superseded" });
        withdrawal.StatusCode.Should().Be(HttpStatusCode.OK);
        var owner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        await using var customerApp = new ApiApplication(fixture.ConnectionString, owner);
        using var customer = AcceptanceClient(customerApp, scope.Customer);

        using var accepted = await customer.PostAsync($"{published.Headers.Location}/acceptance", null);
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Archived_customer_organization_cannot_accept_or_read()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        using var published = await supplier.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(scope.List));
        published.StatusCode.Should().Be(HttpStatusCode.Created);
        var owner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        await using var customerApp = new ApiApplication(fixture.ConnectionString, owner);
        using var customer = AcceptanceClient(customerApp, scope.Customer);
        await using (var db = new OrganizationAccessDbContext(fixture.CreateOptions()))
        {
            (await db.Organizations.SingleAsync(x => x.Id == scope.Customer)).Archive(TimeProvider.System);
            await db.SaveChangesAsync();
        }
        var url = $"{published.Headers.Location}/acceptance";
        using var write = await customer.PostAsync(url, null);
        using var read = await customer.GetAsync(url);
        write.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        read.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Accepting_a_newer_version_does_not_accept_its_predecessor()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        using var first = await supplier.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(scope.List));
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var terms = Publication(scope.List);
        terms["expectedLatestVersionId"] = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        terms["validFrom"] = "2026-11-01";
        using var second = await supplier.PostAsJsonAsync($"{scope.Agreement}/versions", terms);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var owner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        await using var customerApp = new ApiApplication(fixture.ConnectionString, owner);
        using var customer = AcceptanceClient(customerApp, scope.Customer);

        using var accepted = await customer.PostAsync($"{second.Headers.Location}/acceptance", null);
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
        var predecessor = await customer.GetFromJsonAsync<JsonElement>($"{first.Headers.Location}/acceptance");
        predecessor.GetProperty("accepted").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Failed_relationship_lookup_does_not_record_acceptance()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        using var published = await supplier.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(scope.List));
        published.StatusCode.Should().Be(HttpStatusCode.Created);
        var owner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        await using var customerApp = new ApiApplication(fixture.ConnectionString, owner,
            services => services.AddScoped<ITradingRelationshipReader, FailingAcceptanceRelationshipReader>());
        using var customer = AcceptanceClient(customerApp, scope.Customer);
        var url = $"{published.Headers.Location}/acceptance";

        using var failed = await customer.PostAsync(url, null);
        failed.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var status = await supplier.GetFromJsonAsync<JsonElement>(url);
        status.GetProperty("accepted").GetBoolean().Should().BeFalse();
    }

    private sealed class FailingAcceptanceRelationshipReader : ITradingRelationshipReader
    {
        public Task<TradingRelationshipDetails?> GetAsync(TradingRelationshipId relationshipId,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Trading Relationship lookup unavailable.");
    }

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> WithAcceptanceGate(
        ApiApplication app, AcceptanceInsertGate gate) =>
        app.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.ConfigureDbContext<AgreementsDbContext>(options => options.AddInterceptors(gate))));

    private sealed class AcceptanceInsertGate : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO agreements.agreement_acceptances", StringComparison.Ordinal))
            {
                if (Interlocked.Increment(ref _arrivals) == 2) _ready.TrySetResult();
                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }

    private async Task<PersonId> AddAcceptanceActor(OrganizationId organization, OrganizationRole role)
    {
        var actor = new PersonId(Guid.CreateVersion7());
        await using var db = new OrganizationAccessDbContext(fixture.CreateOptions());
        db.OrganizationMemberships.Add(OrganizationMembership.Create(new(Guid.CreateVersion7()), organization, actor,
            [role], TimeProvider.System));
        await db.SaveChangesAsync();
        return actor;
    }

    private static HttpClient AcceptanceClient(ApiApplication app, OrganizationId organization)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", organization.ToString());
        return client;
    }
}
