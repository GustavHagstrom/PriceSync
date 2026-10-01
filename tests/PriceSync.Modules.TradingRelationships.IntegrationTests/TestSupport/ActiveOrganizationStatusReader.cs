using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.TradingRelationships.IntegrationTests;

public sealed partial class CreateApiTests
{
    private sealed class ActiveOrganizationStatusReader : IOrganizationStatusReader
    {
        public Task<OrganizationStatusResult> GetStatusAsync(
            OrganizationId organizationId,
            CancellationToken cancellationToken) => Task.FromResult(OrganizationStatusResult.Active);
    }
}
