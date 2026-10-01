using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.TradingRelationships.IntegrationTests;

public sealed partial class CreateApiTests
{
    private sealed class StubOrganizationStatusReader(
        Func<OrganizationId, OrganizationStatusResult>? getStatus = null) : IOrganizationStatusReader
    {
        public List<OrganizationId> Calls { get; } = [];

        public Task<OrganizationStatusResult> GetStatusAsync(
            OrganizationId organizationId,
            CancellationToken cancellationToken)
        {
            Calls.Add(organizationId);
            return Task.FromResult(getStatus?.Invoke(organizationId) ?? OrganizationStatusResult.Active);
        }
    }
}
