using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.TradingRelationships.Persistence;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.TradingRelationships.IntegrationTests;

[Collection(TradingRelationshipsPostgreSqlCollection.Name)]
public sealed class AgreementScopeTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Registered_reader_returns_the_relationship_parties_and_active_status()
    {
        var relationship = await fixture.CreateTradingRelationshipAsync(DateTimeOffset.UtcNow);
        var services = new ServiceCollection();
        services.AddTradingRelationshipsModule(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:TradingRelationships"] = fixture.ConnectionString }).Build());
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAgreementScopeReader>();

        var result = await reader.GetAsync(relationship.Id, CancellationToken.None);

        result.Should().NotBeNull();
        result!.SupplierOrganizationId.Should().Be(relationship.SupplierOrganizationId);
        result.CustomerOrganizationId.Should().Be(relationship.CustomerOrganizationId);
        result.Status.Should().Be(AgreementScopeStatus.Active);

        await using (var db = new TradingRelationshipsDbContext(fixture.CreateOptions()))
        {
            var persisted = await db.TradingRelationships.SingleAsync(x => x.Id == relationship.Id);
            persisted.End(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        var ended = await reader.GetAsync(relationship.Id, CancellationToken.None);
        ended!.Status.Should().Be(AgreementScopeStatus.Ended);
        result.Status.Should().Be(AgreementScopeStatus.Active, "the earlier observation is a point-in-time fact");
        (await reader.GetAsync(TradingRelationshipId.Create(), CancellationToken.None)).Should().BeNull();

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var failedLookup = () => reader.GetAsync(relationship.Id, cancellation.Token);
        await failedLookup.Should().ThrowAsync<OperationCanceledException>();
    }
}
