using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;
using PriceSync.Modules.OrganizationAccess.Persistence;
using PriceSync.Modules.TradingRelationships.Persistence;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.Agreements.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using FluentAssertions;

namespace PriceSync.Modules.Agreements.IntegrationTests;

public sealed partial class AgreementApiTests
{
    [Fact]
    public async Task Exact_version_read_cannot_observe_a_publication_rolled_back_before_commit()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var publisher = scope.Client;
        var failure = new FailFirstPublicationCommit();
        await using var guarded = app.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.ConfigureDbContext<AgreementsDbContext>(options => options.AddInterceptors(failure))));
        using var client = guarded.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", publisher.DefaultRequestHeaders.GetValues("organization_id"));
        var publication = await client.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(scope.List));
        publication.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        failure.VersionId.Should().NotBe(Guid.Empty);
        var failedVersionUrl = $"{scope.Agreement}/versions/{failure.VersionId}";
        var read = await publisher.GetAsync(failedVersionUrl);
        read.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("agreement_version_not_found");

        var retry = await client.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(scope.List));
        retry.StatusCode.Should().Be(HttpStatusCode.Created);
        var committed = await publisher.GetAsync(retry.Headers.Location);
        committed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await committed.Content.ReadAsStringAsync()).Should().Be(await retry.Content.ReadAsStringAsync());
        (await publisher.GetAsync(failedVersionUrl)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed class FailFirstPublicationCommit : DbTransactionInterceptor
    {
        private int _commits;
        internal Guid VersionId { get; private set; }

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _commits) == 1)
            {
                VersionId = eventData.Context!.ChangeTracker.Entries<PriceAgreementVersion>().Single().Entity.Id.Value;
                throw new InvalidOperationException("Injected publication failure immediately before commit.");
            }
            return ValueTask.FromResult(result);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Exact_version_read_remains_available_to_both_parties_members_after_relationship_ended(bool customer)
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var publisher = scope.Client;
        var published = await publisher.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(scope.List));
        published.StatusCode.Should().Be(HttpStatusCode.Created);
        var supplier = new OrganizationId(Guid.Parse(publisher.DefaultRequestHeaders.GetValues("organization_id").Single()));
        var acting = customer ? scope.Customer : supplier;
        var member = new PersonId(Guid.CreateVersion7());
        await using (var db = new OrganizationAccessDbContext(fixture.CreateOptions()))
        {
            db.OrganizationMemberships.Add(OrganizationMembership.Create(new(Guid.CreateVersion7()), acting, member,
                [OrganizationRole.Member], TimeProvider.System));
            await db.SaveChangesAsync();
        }
        await using (var db = new TradingRelationshipsDbContext(fixture.CreateRelationshipOptions()))
        {
            (await db.TradingRelationships.SingleAsync()).End(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        await using var readerApp = new ApiApplication(fixture.ConnectionString, member);
        using var reader = readerApp.CreateClient();
        reader.DefaultRequestHeaders.Add("organization_id", acting.ToString());
        var response = await reader.GetAsync(published.Headers.Location);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be(await published.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Exact_version_read_hides_unknown_versions_and_incorrect_agreement_version_combinations()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var client = scope.Client;
        var published = await client.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(scope.List));
        published.StatusCode.Should().Be(HttpStatusCode.Created);
        var version = (await published.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await using var db = new AgreementsDbContext(fixture.CreateAgreementOptions());
        var agreement = await db.PriceAgreements.SingleAsync();
        var other = await PostAgreement(client, agreement.TradingRelationshipId.Value);
        other.StatusCode.Should().Be(HttpStatusCode.Created);
        var unknown = await client.GetAsync($"{scope.Agreement}/versions/{Guid.CreateVersion7()}");
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await unknown.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("agreement_version_not_found");
        foreach (var url in new[]
        {
            $"{other.Headers.Location}/versions/{version}",
            $"/api/v1/agreements/{Guid.CreateVersion7()}/versions/{version}",
            $"/api/v1/agreements/{Guid.Empty}/versions/{version}",
            $"{scope.Agreement}/versions/{Guid.Empty}",
        })
        {
            var response = await client.GetAsync(url);
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await ProblemWithoutTrace(response)).Should().Be(await ProblemWithoutTrace(unknown));
        }
    }

    [Theory]
    [InlineData("supplierMember", 200, null)]
    [InlineData("customerMember", 200, null)]
    [InlineData("customerOwner", 200, null)]
    [InlineData("unrelatedOwner", 404, "agreement_version_not_found")]
    [InlineData("noMembership", 403, "forbidden")]
    [InlineData("endedMembership", 403, "forbidden")]
    [InlineData("archived", 403, "forbidden")]
    [InlineData("anonymous", 401, null)]
    [InlineData("noOrganization", 400, OrganizationContextErrors.Required)]
    public async Task Exact_version_read_requires_current_member_authority_in_either_active_party(
        string scenario, int expectedStatus, string? expectedCode)
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var publisher = scope.Client;
        var published = await publisher.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(scope.List));
        published.StatusCode.Should().Be(HttpStatusCode.Created);
        var actor = new PersonId(Guid.CreateVersion7());
        var supplier = new OrganizationId(Guid.Parse(publisher.DefaultRequestHeaders.GetValues("organization_id").Single()));
        var acting = scenario.StartsWith("customer", StringComparison.Ordinal) ? scope.Customer : supplier;
        if (scenario == "unrelatedOwner") acting = await SeedOrganization(actor, "5560004615");
        else if (scenario is "supplierMember" or "customerMember" or "customerOwner" or "endedMembership" or "archived")
        {
            await using var db = new OrganizationAccessDbContext(fixture.CreateOptions());
            var membership = OrganizationMembership.Create(new(Guid.CreateVersion7()), acting, actor,
                [scenario == "customerOwner" ? OrganizationRole.Owner : OrganizationRole.Member], TimeProvider.System);
            if (scenario == "endedMembership") membership.End(TimeProvider.System);
            db.OrganizationMemberships.Add(membership);
            if (scenario == "archived") (await db.Organizations.FindAsync(acting))!.Archive(TimeProvider.System);
            await db.SaveChangesAsync();
        }
        await using var readerApp = new ApiApplication(fixture.ConnectionString, scenario == "anonymous" ? null : actor);
        using var reader = readerApp.CreateClient();
        if (scenario != "noOrganization") reader.DefaultRequestHeaders.Add("organization_id", acting.ToString());
        var response = await reader.GetAsync(published.Headers.Location);
        ((int)response.StatusCode).Should().Be(expectedStatus, await response.Content.ReadAsStringAsync());
        if (expectedCode is not null)
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be(expectedCode);
        if (expectedStatus == 200)
            (await response.Content.ReadAsStringAsync()).Should().Be(await published.Content.ReadAsStringAsync());
        if (expectedStatus == 404)
        {
            var unknown = await reader.GetAsync($"{scope.Agreement}/versions/{Guid.CreateVersion7()}");
            (await ProblemWithoutTrace(response)).Should().Be(await ProblemWithoutTrace(unknown));
        }
    }

    [Fact]
    public async Task Exact_version_read_returns_all_published_facts_from_a_new_request()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var client = scope.Client;
        var request = Publication(scope.List);
        request["validTo"] = "2027-01-31";
        request["coverage"]![0]!["generalDiscount"] = 12.5m;
        var publication = await client.PostAsJsonAsync($"{scope.Agreement}/versions", request);
        publication.StatusCode.Should().Be(HttpStatusCode.Created);

        using var reader = app.CreateClient();
        reader.DefaultRequestHeaders.Add("organization_id", client.DefaultRequestHeaders.GetValues("organization_id"));
        var response = await reader.GetAsync(publication.Headers.Location);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsStringAsync()).Should().Be(await publication.Content.ReadAsStringAsync());
    }
}
