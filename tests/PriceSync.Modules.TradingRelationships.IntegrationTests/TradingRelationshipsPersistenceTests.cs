using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Domain;
using PriceSync.Modules.TradingRelationships.Persistence;

namespace PriceSync.Modules.TradingRelationships.IntegrationTests;

[Collection(TradingRelationshipsPostgreSqlCollection.Name)]
public sealed class TradingRelationshipsPersistenceTests(TradingRelationshipsPostgreSqlFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync() => await fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Clean_postgresql_database_migrates_with_module_local_history()
    {
        var databaseName = $"pricesync_tr_{Guid.NewGuid():N}";
        await ExecuteAsync(fixture.ConnectionString, $"create database {databaseName}");

        var builder = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = databaseName,
        };

        try
        {
            var options = new DbContextOptionsBuilder<TradingRelationshipsDbContext>()
                .UseNpgsql(
                    builder.ConnectionString,
                    npgsqlOptions => npgsqlOptions.MigrationsHistoryTable(
                        TradingRelationshipsDbContext.MigrationsHistoryTableName,
                        TradingRelationshipsDbContext.SchemaName))
                .Options;

            await using var context = new TradingRelationshipsDbContext(options);
            await context.Database.MigrateAsync();

            var tables = await QueryStringsAsync(
                builder.ConnectionString,
                """
                select table_name
                from information_schema.tables
                where table_schema = 'trading_relationships'
                order by table_name;
                """);

            tables.Should().BeEquivalentTo("TradingRelationships", "__EFMigrationsHistory");
        }
        finally
        {
            await ExecuteAsync(fixture.ConnectionString, $"""drop database "{databaseName}" with (force)""");
        }
    }

    [Fact]
    public async Task Relationship_round_trips_with_strong_identity_direction_and_lifecycle_state()
    {
        var createdAt = new DateTimeOffset(2026, 9, 24, 8, 30, 0, TimeSpan.Zero);
        var relationship = TradingRelationship.Create(NewOrganizationId(), NewOrganizationId(), createdAt);

        await using (var context = NewContext())
        {
            context.TradingRelationships.Add(relationship);
            await context.SaveChangesAsync();
        }

        await using (var context = NewContext())
        {
            var loaded = await context.TradingRelationships.SingleAsync();

            loaded.Id.Should().Be(relationship.Id);
            loaded.SupplierOrganizationId.Should().Be(relationship.SupplierOrganizationId);
            loaded.CustomerOrganizationId.Should().Be(relationship.CustomerOrganizationId);
            loaded.Status.Should().Be(TradingRelationshipStatus.Active);
            loaded.CreatedAt.Should().Be(createdAt);
            loaded.EndedAt.Should().BeNull();
        }
    }

    [Fact]
    public async Task Partial_unique_index_allows_history_but_rejects_two_active_directed_relationships()
    {
        var supplierId = NewOrganizationId();
        var customerId = NewOrganizationId();
        var firstHistorical = EndedRelationship(supplierId, customerId, daysAgo: 4);
        var secondHistorical = EndedRelationship(supplierId, customerId, daysAgo: 2);

        await using (var context = NewContext())
        {
            context.TradingRelationships.AddRange(firstHistorical, secondHistorical);
            await context.SaveChangesAsync();
        }

        await using (var context = NewContext())
        {
            context.TradingRelationships.Add(TradingRelationship.Create(supplierId, customerId, DateTimeOffset.UtcNow));
            await context.SaveChangesAsync();
        }

        await using (var context = NewContext())
        {
            context.TradingRelationships.Add(TradingRelationship.Create(supplierId, customerId, DateTimeOffset.UtcNow));

            var save = () => context.SaveChangesAsync();

            await save.Should().ThrowAsync<DbUpdateException>();
        }
    }

    [Fact]
    public async Task Reverse_directions_can_be_active_at_the_same_time()
    {
        var firstOrganizationId = NewOrganizationId();
        var secondOrganizationId = NewOrganizationId();

        await using var context = NewContext();
        context.TradingRelationships.AddRange(
            TradingRelationship.Create(firstOrganizationId, secondOrganizationId, DateTimeOffset.UtcNow),
            TradingRelationship.Create(secondOrganizationId, firstOrganizationId, DateTimeOffset.UtcNow));

        await context.SaveChangesAsync();

        (await context.TradingRelationships.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Schema_contains_directional_indexes_and_no_cross_module_foreign_keys()
    {
        var indexes = await QueryStringsAsync(
            fixture.ConnectionString,
            """
            select indexname || ':' || indexdef
            from pg_indexes
            where schemaname = 'trading_relationships'
                and tablename = 'TradingRelationships'
            order by indexname;
            """);

        indexes.Single(index => index.StartsWith("ix_trading_relationships_supplier_status_created_at_id:", StringComparison.Ordinal))
            .Should().Contain("(supplier_organization_id, status, created_at DESC, \"Id\" DESC)");
        indexes.Single(index => index.StartsWith("ix_trading_relationships_customer_status_created_at_id:", StringComparison.Ordinal))
            .Should().Contain("(customer_organization_id, status, created_at DESC, \"Id\" DESC)");
        indexes.Single(index => index.StartsWith("ux_trading_relationships_active_supplier_customer:", StringComparison.Ordinal))
            .Should().Contain("UNIQUE INDEX");

        var foreignKeyCount = await ExecuteScalarAsync<int>(
            fixture.ConnectionString,
            """
            select count(*)
            from pg_constraint constraint_record
            join pg_class table_record on table_record.oid = constraint_record.conrelid
            join pg_namespace schema_record on schema_record.oid = table_record.relnamespace
            where schema_record.nspname = 'trading_relationships'
                and table_record.relname = 'TradingRelationships'
                and constraint_record.contype = 'f';
            """);

        foreignKeyCount.Should().Be(0);
    }

    [Theory]
    [InlineData("suspended", "null", false)]
    [InlineData("active", "now()", false)]
    [InlineData("ended", "null", false)]
    [InlineData("active", "null", true)]
    public async Task Database_constraints_reject_invalid_status_lifecycle_and_party_state(
        string status,
        string endedAt,
        bool sameParty)
    {
        var supplierId = Guid.CreateVersion7();
        var customerId = sameParty ? supplierId : Guid.CreateVersion7();
        var insert = () => ExecuteAsync(
            fixture.ConnectionString,
            $"""
            insert into trading_relationships."TradingRelationships"
                ("Id", supplier_organization_id, customer_organization_id, created_at, ended_at, status)
            values
                ('{Guid.CreateVersion7()}', '{supplierId}', '{customerId}', now(), {endedAt}, '{status}');
            """);

        await insert.Should().ThrowAsync<PostgresException>();
    }

    [Fact]
    public void Persistence_infrastructure_remains_internal_to_trading_relationships()
    {
        var exportedPersistenceTypes = typeof(TradingRelationshipsDbContext).Assembly.ExportedTypes
            .Select(type => type.FullName)
            .Where(typeName => typeName is not null && typeName.Contains(".Persistence.", StringComparison.Ordinal));

        exportedPersistenceTypes.Should().BeEmpty();
    }

    private TradingRelationshipsDbContext NewContext() => new(fixture.CreateOptions());

    private static TradingRelationship EndedRelationship(
        OrganizationId supplierId,
        OrganizationId customerId,
        int daysAgo)
    {
        var relationship = TradingRelationship.Create(
            supplierId,
            customerId,
            DateTimeOffset.UtcNow.AddDays(-daysAgo));
        relationship.End(DateTimeOffset.UtcNow.AddDays(-(daysAgo - 1)));
        return relationship;
    }

    private static OrganizationId NewOrganizationId() => new(Guid.CreateVersion7());

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ExecuteScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("The scalar query returned null.");
        return (T)Convert.ChangeType(value, typeof(T));
    }

    private static async Task<IReadOnlyCollection<string>> QueryStringsAsync(string connectionString, string sql)
    {
        var values = new List<string>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }
}
