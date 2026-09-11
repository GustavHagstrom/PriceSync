using Microsoft.EntityFrameworkCore;
using Npgsql;
using PriceSync.Modules.OrganizationAccess.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace PriceSync.Modules.OrganizationAccess.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class OrganizationAccessPostgreSqlCollection : ICollectionFixture<OrganizationAccessPostgreSqlFixture>
{
    public const string Name = "OrganizationAccessPostgreSql";
}

public sealed class OrganizationAccessPostgreSqlFixture : IAsyncLifetime
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
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public async Task ResetAsync()
    {
        const string sql = """
            truncate table
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
