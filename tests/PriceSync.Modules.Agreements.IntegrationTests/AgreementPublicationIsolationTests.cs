using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.TradingRelationships.Persistence;

namespace PriceSync.Modules.Agreements.IntegrationTests;

public sealed partial class AgreementApiTests
{
    [Fact]
    public async Task Paused_publication_allows_another_agreement_to_publish_and_does_not_hold_external_locks()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var client = scope.Client;
        await using var relationships = new TradingRelationshipsDbContext(fixture.CreateRelationshipOptions());
        var relationship = await relationships.TradingRelationships.SingleAsync();
        var other = await PostAgreement(client, relationship.Id.Value);
        other.StatusCode.Should().Be(HttpStatusCode.Created);

        var gate = new CommitGate(false);
        await using var guarded = app.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.ConfigureDbContext<AgreementsDbContext>(options => options.AddInterceptors(gate))));
        using var publisher = guarded.CreateClient();
        publisher.DefaultRequestHeaders.Add("organization_id", client.DefaultRequestHeaders.GetValues("organization_id"));
        var request = Publication(scope.List);
        var pending = publisher.PostAsJsonAsync($"{scope.Agreement}/versions", request);
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await using (var connection = new NpgsqlConnection(fixture.ConnectionString))
            {
                await connection.OpenAsync(timeout.Token);
                await using var transaction = await connection.BeginTransactionAsync(timeout.Token);
                await using var probe = new NpgsqlCommand("""
                    LOCK TABLE price_publication.price_lists, trading_relationships."TradingRelationships"
                    IN ACCESS EXCLUSIVE MODE NOWAIT
                    """, connection, transaction);
                await probe.ExecuteNonQueryAsync(timeout.Token);
            }

            // Both agreements use the same relationship and logical Price List.
            var independent = await publisher.PostAsJsonAsync($"{other.Headers.Location}/versions", request, timeout.Token);
            independent.StatusCode.Should().Be(HttpStatusCode.Created);
            pending.IsCompleted.Should().BeFalse();

            relationship.End(DateTimeOffset.UtcNow);
            await relationships.SaveChangesAsync(timeout.Token);
        }
        finally
        {
            gate.Release.TrySetResult();
        }

        var published = await pending;
        published.StatusCode.Should().Be(HttpStatusCode.Created);
        var facts = await published.Content.ReadFromJsonAsync<JsonElement>();
        var retry = await client.PostAsJsonAsync($"{scope.Agreement}/versions", request);
        request["expectedLatestVersionId"] = facts.GetProperty("id").GetGuid();
        var next = await client.PostAsJsonAsync($"{scope.Agreement}/versions", request);
        foreach (var rejected in new[] { retry, next })
        {
            rejected.StatusCode.Should().Be(HttpStatusCode.Conflict);
            var problem = await rejected.Content.ReadFromJsonAsync<JsonElement>();
            problem.GetProperty("code").GetString().Should().Be("agreement_relationship_ended");
        }
        var historical = await client.GetAsync(published.Headers.Location);
        historical.StatusCode.Should().Be(HttpStatusCode.OK);
        (await historical.Content.ReadAsStringAsync()).Should().Be(await published.Content.ReadAsStringAsync());
    }
}
