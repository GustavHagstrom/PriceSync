using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.PricePublication.Contracts;

namespace PriceSync.Modules.Agreements.IntegrationTests;

public sealed partial class AgreementApiTests
{
    [Fact]
    public async Task Consumer_reads_original_facts_after_a_successor_is_published()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var client = scope.Client;
        var url = $"{scope.Agreement}/versions";
        var request = Publication(scope.List);
        request["validTo"] = "2026-11-20";
        request["coverage"]![0]!["generalDiscount"] = 12.5m;
        request["groupDiscounts"] = JsonNode.Parse(""" [{"code":"01","discount":1},{"code":"a","discount":0}] """);
        var first = await client.PostAsJsonAsync(url, request);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var version = await first.Content.ReadFromJsonAsync<JsonElement>();
        var id = new AgreementVersionId(version.GetProperty("id").GetGuid());
        request["expectedLatestVersionId"] = id.Value;
        request["coverage"]![0]!["generalDiscount"] = 35m;
        request["validTo"] = null;
        request["groupDiscounts"] = new JsonArray();
        var next = await client.PostAsJsonAsync(url, request);
        next.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var consumerScope = app.Services.CreateAsyncScope();
        var reader = consumerScope.ServiceProvider.GetRequiredService<IAgreementVersionReader>();
        var facts = await reader.GetAsync(id);
        facts.Should().NotBeNull();
        facts!.Id.Should().Be(id);
        facts.AgreementId.Value.Should().Be(version.GetProperty("agreementId").GetGuid());
        facts.TradingRelationshipId.Value.Should().Be(version.GetProperty("tradingRelationshipId").GetGuid());
        facts.PredecessorVersionId.Should().BeNull();
        facts.Ordinal.Should().Be(1);
        facts.ValidFrom.Should().Be(new DateOnly(2026, 10, 26));
        facts.ValidTo.Should().Be(new DateOnly(2026, 11, 20));
        facts.PublishedAt.Should().Be(version.GetProperty("publishedAt").GetDateTimeOffset());
        facts.PublishedByPersonId.Value.Should().Be(version.GetProperty("publishedByPersonId").GetGuid());
        facts.Rounding.Should().Be(new AgreementVersionRounding(4, "AwayFromZero", "OnceAfterDiscountWithoutIntermediateRounding", true));
        facts.Coverage.Should().Equal(new AgreementVersionListCoverage(new(scope.List), PriceListType.General, 12.5m));
        facts.GroupDiscounts.Should().Equal(new AgreementVersionGroupTerms("01", 1), new AgreementVersionGroupTerms("a", 0));
        (await client.GetStringAsync(first.Headers.Location)).Should().Be(await first.Content.ReadAsStringAsync());
        var nextId = (await next.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await reader.GetAsync(new(nextId)))!.PredecessorVersionId.Should().Be(id);
    }
    [Fact]
    public async Task Consumer_returns_null_for_an_unknown_exact_version()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var client = scope.Client;
        await using var consumerScope = app.Services.CreateAsyncScope();
        var reader = consumerScope.ServiceProvider.GetRequiredService<IAgreementVersionReader>();
        (await reader.GetAsync(AgreementVersionId.Create())).Should().BeNull();
    }

    [Fact]
    public async Task Consumer_propagates_database_failures()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var client = scope.Client;
        var fault = new NpgsqlException("Injected exact version read failure.");
        await using var faultyApp = app.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.ConfigureDbContext<AgreementsDbContext>(options => options.AddInterceptors(new FailVersionRead(fault)))));
        await using var consumerScope = faultyApp.Services.CreateAsyncScope();
        var reader = consumerScope.ServiceProvider.GetRequiredService<IAgreementVersionReader>();
        var read = async () => await reader.GetAsync(AgreementVersionId.Create());
        (await read.Should().ThrowAsync<NpgsqlException>()).Which.Should().BeSameAs(fault);
    }

    private sealed class FailVersionRead(NpgsqlException fault) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            => throw fault;
    }
}
