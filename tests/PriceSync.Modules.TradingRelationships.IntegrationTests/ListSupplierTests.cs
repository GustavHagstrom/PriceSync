using Microsoft.EntityFrameworkCore.Diagnostics;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.IntegrationTests.TestSupport;

namespace PriceSync.Modules.TradingRelationships.IntegrationTests;

[Collection(TradingRelationshipsPostgreSqlCollection.Name)]
public class ListSupplierTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 24, 8, 30, 0, TimeSpan.Zero);
    public Task DisposeAsync() => Task.CompletedTask;
    public Task InitializeAsync() => fixture.ResetAsync();

    private ApiApplication CreateApplication(
        IOrganizationAuthorizer? authorizer = null,
        IOrganizationStatusReader? statusReader = null,
        PersonId? personId = null,
        IReadOnlyCollection<IInterceptor>? interceptors = null)
    {
        return new ApiApplication(
            fixture.ConnectionString,
            personId ?? new PersonId(Guid.CreateVersion7()),
            authorizer ?? new StubOrganizationAuthorizer(allowed: true),
            statusReader ?? new StubOrganizationStatusReader(x => OrganizationStatusResult.Active),
            new FrozenTimeProvider(CreatedAt),
            interceptors);
    }
}
