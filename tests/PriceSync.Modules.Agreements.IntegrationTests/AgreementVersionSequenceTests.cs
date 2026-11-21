using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.Agreements.Persistence;

namespace PriceSync.Modules.Agreements.IntegrationTests;

public sealed partial class AgreementApiTests
{
    [Fact]
    public async Task Version_sequence_appends_complete_terms_and_preserves_the_previous_version()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var client = scope.Client;
        var url = $"{scope.Agreement}/versions";
        var request = Publication(scope.List);
        var secondList = await client.PostAsJsonAsync("/api/v1/price-lists", new
        { name = "Second", type = "general", currency = "SEK", market = "SE" });
        secondList.StatusCode.Should().Be(HttpStatusCode.Created);
        var secondListId = (await secondList.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        request["coverage"]!.AsArray().Add(Publication(secondListId)["coverage"]![0]!.DeepClone());
        var first = await client.PostAsJsonAsync(url, request);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstBody = await first.Content.ReadAsStringAsync();
        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        request["coverage"]!.AsArray().RemoveAt(1);
        request["validFrom"] = "2026-10-27";

        request["expectedLatestVersionId"] = firstId;
        request["coverage"]![0]!["generalDiscount"] = 12.5m;
        var next = await client.PostAsJsonAsync(url, request);
        next.StatusCode.Should().Be(HttpStatusCode.Created, await next.Content.ReadAsStringAsync());
        var version = await next.Content.ReadFromJsonAsync<JsonElement>();
        version.GetProperty("ordinal").GetInt64().Should().Be(2);
        version.GetProperty("predecessorVersionId").GetGuid().Should().Be(firstId);
        version.GetProperty("coverage").GetArrayLength().Should().Be(1);
        version.GetProperty("coverage")[0].GetProperty("generalDiscount").GetDecimal().Should().Be(12.5m);
        (await client.GetStringAsync(first.Headers.Location)).Should().Be(firstBody);
        (await client.GetStringAsync(next.Headers.Location)).Should().Be(await next.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task Version_sequence_replays_only_the_matching_immediate_successor()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var client = scope.Client;
        var url = $"{scope.Agreement}/versions";
        var request = Publication(scope.List);
        request["groupDiscounts"] = JsonNode.Parse(""" [{"code":"01","discount":1.0},{"code":"A","discount":0}] """);
        var secondList = await client.PostAsJsonAsync("/api/v1/price-lists", new
        { name = "Second", type = "general", currency = "SEK", market = "SE" });
        secondList.StatusCode.Should().Be(HttpStatusCode.Created);
        var secondListId = (await secondList.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var coverage = request["coverage"]!.AsArray();
        coverage.Add(Publication(secondListId)["coverage"]![0]!.DeepClone());
        var first = await client.PostAsJsonAsync(url, request);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstBody = await first.Content.ReadAsStringAsync();
        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        request["groupDiscounts"] = JsonNode.Parse(""" [{"code":"A","discount":0.0},{"code":"01","discount":1}] """);
        var leadingCoverage = coverage[0];
        coverage.RemoveAt(0);
        coverage.Add(leadingCoverage);
        var replay = await client.PostAsJsonAsync(url, request);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        (await replay.Content.ReadAsStringAsync()).Should().Be(firstBody);

        request["expectedLatestVersionId"] = firstId;
        var next = await client.PostAsJsonAsync(url, request);
        next.StatusCode.Should().Be(HttpStatusCode.Created);
        var nextBody = await next.Content.ReadAsStringAsync();
        var nextReplay = await client.PostAsJsonAsync(url, request);
        nextReplay.StatusCode.Should().Be(HttpStatusCode.OK);
        (await nextReplay.Content.ReadAsStringAsync()).Should().Be(nextBody);

        var alternate = await client.PostAsJsonAsync("/api/v1/price-lists", new
        { name = "Alternate", type = "general", currency = "SEK", market = "SE" });
        alternate.StatusCode.Should().Be(HttpStatusCode.Created);
        var alternateId = (await alternate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Action<JsonNode>[] changes =
        [
            body => body["validFrom"] = "2026-10-27",
            body => body["validTo"] = "2026-10-27",
            body => body["coverage"]![0]!["generalDiscount"] = 0.1m,
            body => body["coverage"]![0]!["priceListId"] = alternateId,
            body => body["groupDiscounts"]![1]!["code"] = "1",
            body => body["groupDiscounts"]![1]!["discount"] = 1.1m,
        ];
        foreach (var change in changes)
        {
            var altered = request.DeepClone();
            change(altered);
            var conflict = await client.PostAsJsonAsync(url, altered);
            conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await conflict.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
                .Should().Be("agreement_publication_conflict");
        }
        (await client.GetFromJsonAsync<JsonElement>(scope.Agreement)).GetProperty("latestVersionId").GetGuid()
            .Should().Be((await next.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        await using var db = new AgreementsDbContext(fixture.CreateAgreementOptions());
        (await db.Versions.CountAsync()).Should().Be(2);
        request["expectedLatestVersionId"] = null;
        (await client.PostAsJsonAsync(url, request)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.GetStringAsync(first.Headers.Location)).Should().Be(firstBody);
        (await client.GetStringAsync(next.Headers.Location)).Should().Be(nextBody);
    }
    [Fact]
    public async Task Version_sequence_replay_after_host_restart_preserves_original_rounding()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var client = scope.Client;
        var url = $"{scope.Agreement}/versions";
        var request = Publication(scope.List);
        var first = await client.PostAsJsonAsync(url, request);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await using var restarted = app.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton(AgreementRoundingPolicy.Default with { DecimalPlaces = 6 })));
        using var reader = restarted.CreateClient();
        reader.DefaultRequestHeaders.Add("organization_id", client.DefaultRequestHeaders.GetValues("organization_id"));
        var replay = await reader.PostAsJsonAsync(url, request);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        (await replay.Content.ReadAsStringAsync()).Should().Be(await first.Content.ReadAsStringAsync());

        request["expectedLatestVersionId"] = firstId;
        var next = await reader.PostAsJsonAsync(url, request);
        next.StatusCode.Should().Be(HttpStatusCode.Created);
        (await next.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rounding").GetProperty("decimalPlaces").GetInt32().Should().Be(6);
        (await reader.GetStringAsync(first.Headers.Location)).Should().Be(await first.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Version_sequence_can_replay_a_version_published_before_request_storage()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var client = scope.Client;
        var url = $"{scope.Agreement}/versions";
        var request = Publication(scope.List);
        var first = await client.PostAsJsonAsync(url, request);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        await using (var db = new AgreementsDbContext(fixture.CreateAgreementOptions()))
        {
            // The migration leaves existing rows without a normalized request.
            (await db.Versions.ExecuteUpdateAsync(update => update.SetProperty(version => version.NormalizedRequest, (string?)null)))
                .Should().Be(1);
        }

        var replay = await client.PostAsJsonAsync(url, request);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        (await replay.Content.ReadAsStringAsync()).Should().Be(await first.Content.ReadAsStringAsync());
        request["validFrom"] = "2026-10-27";
        (await client.PostAsJsonAsync(url, request)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Version_sequence_failed_commit_keeps_previous_version_and_latest()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var client = scope.Client;
        var url = $"{scope.Agreement}/versions";
        var request = Publication(scope.List);
        var first = await client.PostAsJsonAsync(url, request);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var stored = await first.Content.ReadFromJsonAsync<JsonElement>();
        request["expectedLatestVersionId"] = stored.GetProperty("id").GetGuid();
        request["coverage"]![0]!["generalDiscount"] = 35m;
        var gate = new CommitGate(true);
        gate.Release.SetResult();
        await using var guarded = app.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.ConfigureDbContext<AgreementsDbContext>(options => options.AddInterceptors(gate))));
        using var writer = guarded.CreateClient();
        writer.DefaultRequestHeaders.Add("organization_id", client.DefaultRequestHeaders.GetValues("organization_id"));
        (await writer.PostAsJsonAsync(url, request)).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await client.GetStringAsync(first.Headers.Location)).Should().Be(await first.Content.ReadAsStringAsync());
        (await client.GetFromJsonAsync<JsonElement>(scope.Agreement)).GetProperty("latestVersionId").GetGuid()
            .Should().Be(stored.GetProperty("id").GetGuid());
        await using var db = new AgreementsDbContext(fixture.CreateAgreementOptions());
        (await db.Versions.CountAsync()).Should().Be(1);
        (await db.Coverage.CountAsync()).Should().Be(1);
        (await writer.PostAsJsonAsync(url, request)).StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
