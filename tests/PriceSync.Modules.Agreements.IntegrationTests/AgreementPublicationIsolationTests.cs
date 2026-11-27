using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.OrganizationAccess.Domain;
using PriceSync.Modules.PricePublication.Persistence;
using PriceSync.Modules.TradingRelationships.Persistence;

namespace PriceSync.Modules.Agreements.IntegrationTests;

public sealed partial class AgreementApiTests
{
    [Fact]
    public async Task Price_lists_and_agreements_use_owned_migration_histories_native_ids_and_local_foreign_keys()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        await using var services = app.Services.CreateAsyncScope();
        var agreements = services.ServiceProvider.GetRequiredService<AgreementsDbContext>();
        var lists = services.ServiceProvider.GetRequiredService<PricePublicationDbContext>();
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        await AssertMigrationHistory("agreements", agreements.Database.GetMigrations());
        await AssertMigrationHistory("price_publication", lists.Database.GetMigrations());

        await using var violations = new NpgsqlCommand("""
            SELECT count(*)
            FROM pg_constraint AS fk
            JOIN pg_class AS source ON source.oid = fk.conrelid
            JOIN pg_class AS target ON target.oid = fk.confrelid
            JOIN pg_namespace AS source_schema ON source_schema.oid = source.relnamespace
            JOIN pg_namespace AS target_schema ON target_schema.oid = target.relnamespace
            WHERE fk.contype = 'f'
              AND source_schema.oid <> target_schema.oid
              AND (source_schema.nspname IN ('agreements', 'price_publication')
                OR target_schema.nspname IN ('agreements', 'price_publication'))
            """, connection);
        (await violations.ExecuteScalarAsync()).Should().Be(0L);

        await using var columns = new NpgsqlCommand("""
            SELECT count(*)
            FROM information_schema.columns
            WHERE data_type = 'uuid'
              AND (
                (table_schema = 'price_publication' AND table_name = 'price_lists'
                    AND column_name IN ('Id', 'OwnerOrganizationId', 'CustomerOrganizationId'))
                OR (table_schema = 'agreements' AND table_name IN ('price_agreements', 'agreement_versions')
                    AND column_name = 'Id')
              )
            """, connection);
        (await columns.ExecuteScalarAsync()).Should().Be(5L);

        async Task AssertMigrationHistory(string schema, IEnumerable<string> expected)
        {
            await using var command = new NpgsqlCommand($"""SELECT "MigrationId" FROM {schema}."__EFMigrationsHistory" ORDER BY "MigrationId" """,
                connection);
            await using var reader = await command.ExecuteReaderAsync();
            var applied = new List<string>();
            while (await reader.ReadAsync())
                applied.Add(reader.GetString(0));
            applied.Should().NotBeEmpty().And.Equal(expected);
        }
    }

    [Fact]
    public async Task Customer_can_activate_an_existing_version_while_its_successor_is_waiting_to_commit()
    {
        var scope = await PublicationScope();
        await using var app = scope.App;
        using var supplier = scope.Client;
        var version = await PublishSelectionTerms(supplier, scope.Agreement, scope.List);
        var owner = await AddAcceptanceActor(scope.Customer, OrganizationRole.Owner);
        await using var customerApp = new ApiApplication(fixture.ConnectionString, owner);
        using var customer = AcceptanceClient(customerApp, scope.Customer);
        var url = SelectionUrl(version.RelationshipId);
        var gate = new CommitGate(false);
        await using var guarded = app.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.ConfigureDbContext<AgreementsDbContext>(options => options.AddInterceptors(gate))));
        using var publisher = guarded.CreateClient();
        publisher.DefaultRequestHeaders.Add("organization_id", supplier.DefaultRequestHeaders.GetValues("organization_id"));
        var request = Publication(scope.List);
        request["expectedLatestVersionId"] = version.Id;
        var pending = publisher.PostAsJsonAsync($"{scope.Agreement}/versions", request);
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            using var selection = await customer.PutAsJsonAsync(url,
                new { fromVersionId = (Guid?)null, toVersionId = version.Id }, timeout.Token);
            selection.StatusCode.Should().Be(HttpStatusCode.OK);
            pending.IsCompleted.Should().BeFalse();
        }
        finally
        {
            gate.Release.TrySetResult();
        }

        using var published = await pending;
        published.StatusCode.Should().Be(HttpStatusCode.Created);

        (await ReadActiveSelection(customer, url)).Should().Be(version.Id);
    }

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
