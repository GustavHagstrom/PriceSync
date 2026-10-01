using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.TradingRelationships.IntegrationTests;

public sealed partial class CreateApiTests
{
    private sealed class AllowingOrganizationAuthorizer : IOrganizationAuthorizer
    {
        public Task<bool> HasCapabilityAsync(
            PersonId personId,
            OrganizationId organizationId,
            OrganizationCapability capability,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
