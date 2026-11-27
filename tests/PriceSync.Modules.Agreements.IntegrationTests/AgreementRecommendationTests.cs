using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;
using PriceSync.Modules.TradingRelationships.Persistence;

namespace PriceSync.Modules.Agreements.IntegrationTests;

public sealed partial class AgreementApiTests
{
    [Fact]
    public async Task Recommendation_prefers_later_publication_time_over_ordinal_and_ignores_customer_choices()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        var latestTime = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
        var clock = new RecommendationTestClock(latestTime);
        await using var timed = app.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));
        using var publisher = timed.CreateClient();
        publisher.DefaultRequestHeaders.Add("organization_id", supplier.DefaultRequestHeaders.GetValues("organization_id"));

        var recommended = await PublishSelectionTerms(publisher, scope.Agreement, scope.List);
        clock.Now = latestTime.AddDays(-1);
        var active = await PublishSelectionTerms(publisher, scope.Agreement, scope.List, recommended.Id);
        var accepted = await PublishSelectionTerms(publisher, scope.Agreement, scope.List, active.Id);
        var owner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        await using var customerApp = new ApiApplication(fixture.ConnectionString, owner);
        using var customer = AcceptanceClient(customerApp, scope.Customer);
        await ChangeSelection(customer, SelectionUrl(active.RelationshipId), null, active.Id);
        using var acceptance = await customer.PostAsync($"{accepted.Location}/acceptance", null);
        acceptance.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await customer.GetFromJsonAsync<JsonElement>(
            $"{scope.Agreement}/recommendation?evaluatedDate=2026-11-10&knowledgeCutoff=2026-11-10T12:00:00Z");
        result.GetProperty("recommendation").GetProperty("id").GetGuid().Should().Be(recommended.Id);
    }

    [Fact]
    public async Task Historical_recommendation_uses_only_facts_known_at_the_cutoff()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        var firstTime = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        var clock = new RecommendationTestClock(firstTime);
        await using var timed = app.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));
        using var client = timed.CreateClient();
        client.DefaultRequestHeaders.Add("organization_id", supplier.DefaultRequestHeaders.GetValues("organization_id"));
        var request = Publication(scope.List);
        using var first = await client.PostAsJsonAsync($"{scope.Agreement}/versions", request);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        clock.Now = firstTime.AddDays(1);
        request["expectedLatestVersionId"] = firstId;
        request["validFrom"] = "2026-10-28";
        request["validTo"] = "2026-11-02";
        using var second = await client.PostAsJsonAsync($"{scope.Agreement}/versions", request);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var secondId = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        request["expectedLatestVersionId"] = secondId;
        request["coverage"]![0]!["generalDiscount"] = 5m;
        using var third = await client.PostAsJsonAsync($"{scope.Agreement}/versions", request);
        third.StatusCode.Should().Be(HttpStatusCode.Created);
        var thirdId = (await third.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        clock.Now = firstTime.AddDays(2);
        request["expectedLatestVersionId"] = thirdId;
        request["validFrom"] = "2026-10-27";
        using var laterPublication = await client.PostAsJsonAsync($"{scope.Agreement}/versions", request);
        laterPublication.StatusCode.Should().Be(HttpStatusCode.Created);
        using var withdrawn = await client.PostAsJsonAsync($"{third.Headers.Location}/withdraw", new { reason = "Superseded" });
        withdrawn.StatusCode.Should().Be(HttpStatusCode.OK);
        await using (var db = new TradingRelationshipsDbContext(fixture.CreateRelationshipOptions()))
        {
            (await db.TradingRelationships.SingleAsync()).End(clock.Now);
            await db.SaveChangesAsync();
        }
        var route = $"{scope.Agreement}/recommendation";
        async Task<JsonElement> At(string date, string? cutoff = null)
        {
            var suffix = cutoff is null ? "" : $"&knowledgeCutoff={Uri.EscapeDataString(cutoff)}";
            return (await client.GetFromJsonAsync<JsonElement>($"{route}?evaluatedDate={date}{suffix}"))
                .GetProperty("recommendation");
        }

        (await At("2026-10-28", "2026-11-01T12:00:00Z")).GetProperty("id").GetGuid().Should().Be(firstId);
        var historical = await At("2026-11-02", "2026-11-02T13:00:00+01:00");
        historical.GetProperty("id").GetGuid().Should().Be(thirdId);
        historical.GetProperty("withdrawal").ValueKind.Should().Be(JsonValueKind.Null);
        (await At("2026-11-02")).GetProperty("id").GetGuid().Should().Be(secondId);
        (await At("2026-11-03")).GetProperty("id").GetGuid().Should().Be(firstId);
        (await At("2026-10-25")).ValueKind.Should().Be(JsonValueKind.Null);
        (await client.GetFromJsonAsync<JsonElement>(route)).GetProperty("recommendation")
            .GetProperty("id").GetGuid().Should().Be(firstId);
        using var invalid = await client.GetAsync($"{route}?evaluatedDate=2026-02-30&knowledgeCutoff=2026-11-02T12:00:00");
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = (await invalid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        errors.TryGetProperty("evaluatedDate", out _).Should().BeTrue();
        errors.TryGetProperty("knowledgeCutoff", out _).Should().BeTrue();
    }

    private sealed class RecommendationTestClock(DateTimeOffset now) : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task Recommendation_returns_the_published_version()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var client = scope.Client;
        using var published = await client.PostAsJsonAsync($"{scope.Agreement}/versions", Publication(scope.List));
        published.StatusCode.Should().Be(HttpStatusCode.Created);
        var version = await published.Content.ReadFromJsonAsync<JsonElement>();
        var result = await client.GetFromJsonAsync<JsonElement>($"{scope.Agreement}/recommendation?evaluatedDate=2026-10-26");
        result.GetProperty("recommendation").GetProperty("id").GetGuid().Should().Be(version.GetProperty("id").GetGuid());

        using var sibling = await PostAgreement(client, version.GetProperty("tradingRelationshipId").GetGuid());
        sibling.StatusCode.Should().Be(HttpStatusCode.Created);
        var siblingResult = await client.GetFromJsonAsync<JsonElement>($"{sibling.Headers.Location}/recommendation?evaluatedDate=2026-10-26");
        siblingResult.GetProperty("recommendation").ValueKind.Should().Be(JsonValueKind.Null);

        var actor = new PersonId(Guid.CreateVersion7());
        var acting = await SeedOrganization(actor, "5560004615");
        await using var unrelatedApp = new ApiApplication(fixture.ConnectionString, actor);
        using var unrelated = unrelatedApp.CreateClient();
        unrelated.DefaultRequestHeaders.Add("organization_id", acting.ToString());
        using var denied = await unrelated.GetAsync($"{scope.Agreement}/recommendation");
        using var unknown = await unrelated.GetAsync($"/api/v1/agreements/{Guid.CreateVersion7()}/recommendation");
        denied.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ProblemWithoutTrace(denied)).Should().Be(await ProblemWithoutTrace(unknown));
    }
}
