using Microsoft.EntityFrameworkCore;
using Npgsql;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Domain;
using PriceSync.Modules.TradingRelationships.Persistence;
using Testcontainers.PostgreSql;

namespace PriceSync.Modules.TradingRelationships.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TradingRelationshipsPostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "TradingRelationshipsPostgreSql";
}

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("pricesync")
        .WithUsername("pricesync")
        .WithPassword("pricesync")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    internal DbContextOptions<TradingRelationshipsDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<TradingRelationshipsDbContext>()
            .UseNpgsql(
                ConnectionString,
                options => options.MigrationsHistoryTable(
                    TradingRelationshipsDbContext.MigrationsHistoryTableName,
                    TradingRelationshipsDbContext.SchemaName))
            .Options;
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = new TradingRelationshipsDbContext(CreateOptions());
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public async Task ResetAsync()
    {
        const string sql = """
            truncate table trading_relationships."TradingRelationships";
            """;

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
    internal async Task<TradingRelationship> CreateTradingRelationshipAsync(DateTimeOffset time)
    {
        var tradingRelationship = TradingRelationship.Create(OrganizationId.Create(), OrganizationId.Create(), time);
        await using var context = new TradingRelationshipsDbContext(CreateOptions());
        context.TradingRelationships.Add(tradingRelationship);
        await context.SaveChangesAsync();
        return tradingRelationship;
    }
    internal async Task<TradingRelationship> CreateTradingRelationshipAsync(DateTimeOffset time, OrganizationId supplierId, OrganizationId customer)
    {
        var tradingRelationship = TradingRelationship.Create(supplierId, customer, time);
        await using var context = new TradingRelationshipsDbContext(CreateOptions());
        context.TradingRelationships.Add(tradingRelationship);
        await context.SaveChangesAsync();
        return tradingRelationship;
    }
}
