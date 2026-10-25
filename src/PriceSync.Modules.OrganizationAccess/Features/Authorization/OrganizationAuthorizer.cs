using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.OrganizationAccess.Domain;
using PriceSync.Modules.OrganizationAccess.Persistence;

namespace PriceSync.Modules.OrganizationAccess.Features.Authorization;

internal class OrganizationAuthorizer(
    OrganizationAccessDbContext dbContext,
    ILogger<OrganizationAuthorizer> logger) : IOrganizationAuthorizer
{
    public async Task<bool> HasCapabilityAsync(
        PersonId personId,
        OrganizationId organizationId,
        OrganizationCapability capability,
        CancellationToken cancellationToken = default)
    {
        if (personId.Value == Guid.Empty)
            return Deny("InvalidPersonId");
        if (organizationId.Value == Guid.Empty)
            return Deny("InvalidOrganizationId");

        var organization = await dbContext.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);
        if (organization is null)
            return Deny("OrganizationNotFound");
        if (organization.Status != OrganizationStatus.Active)
            return Deny("OrganizationArchived");

        if (!Enum.IsDefined(capability))
            throw new ArgumentOutOfRangeException(nameof(capability), capability, "Invalid capability value.");

        var membership = await dbContext.OrganizationMemberships.FirstOrDefaultAsync(
            m => m.PersonId == personId && m.OrganizationId == organizationId &&
                 m.Status == OrganizationMembershipStatus.Active, cancellationToken);
        if (membership is null)
            return Deny("NoActiveMembership");
        if (capability is (OrganizationCapability.ManageTradingRelationships or OrganizationCapability.CreatePriceLists
            or OrganizationCapability.PublishAgreements or OrganizationCapability.ManageCustomerAgreements) &&
            !membership.Roles.Contains(OrganizationRole.Owner))
            return Deny("RequiredRoleMissing");

        return true;

        bool Deny(string reason)
        {
            logger.LogInformation(
                "Organization authorization completed with {Outcome}, reason {Reason}, " +
                "person {PersonId}, organization {OrganizationId}, capability {Capability}, trace {TraceId}",
                "Denied", reason, personId.Value, organizationId.Value, capability,
                Activity.Current?.TraceId.ToString());
            return false;
        }
    }
}
