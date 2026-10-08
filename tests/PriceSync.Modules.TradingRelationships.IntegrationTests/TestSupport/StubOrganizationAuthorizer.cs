using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.TradingRelationships.IntegrationTests.TestSupport;


public sealed class StubOrganizationAuthorizer(bool allowed = true) : IOrganizationAuthorizer
{
    public List<(OrganizationId OrganizationId, OrganizationCapability Capability)> Calls { get; } = [];

    public Task<bool> HasCapabilityAsync(
        PersonId personId,
        OrganizationId organizationId,
        OrganizationCapability capability,
        CancellationToken cancellationToken = default)
    {
        Calls.Add((organizationId, capability));
        return Task.FromResult(allowed);
    }
}

