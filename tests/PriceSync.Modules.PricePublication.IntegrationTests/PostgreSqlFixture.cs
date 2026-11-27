using Microsoft.EntityFrameworkCore;
using Npgsql;
using PriceSync.Modules.OrganizationAccess.Persistence;
using PriceSync.Modules.PricePublication.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace PriceSync.Modules.PricePublication.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PricePublicationPostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "PricePublicationPostgreSql";
}

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("pricesync")
        .WithUsername("pricesync")
        .WithPassword("pricesync")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    internal DbContextOptions<OrganizationAccessDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<OrganizationAccessDbContext>()
            .UseNpgsql(
                ConnectionString,
                npgsqlOptions => npgsqlOptions.MigrationsHistoryTable(
                    OrganizationAccessDbContext.MigrationsHistoryTableName,
                    OrganizationAccessDbContext.SchemaName))
            .Options;
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = new OrganizationAccessDbContext(CreateOptions());
        await context.Database.MigrateAsync();
        await using var publication = new PricePublicationDbContext(CreatePublicationOptions());
        await publication.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    internal DbContextOptions<PricePublicationDbContext> CreatePublicationOptions() =>
        new DbContextOptionsBuilder<PricePublicationDbContext>().UseNpgsql(ConnectionString,
            options => options.MigrationsHistoryTable(PricePublicationDbContext.MigrationsHistoryTableName,
                PricePublicationDbContext.SchemaName)).Options;

    public async Task ResetAsync()
    {
        const string sql = """
            truncate table
                price_publication.price_lists,
                organization_access.organization_membership_roles,
                organization_access.organization_memberships,
                organization_access.organizations,
                organization_access.idempotency_records
            restart identity cascade;
            """;

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
