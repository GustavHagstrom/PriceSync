using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.OrganizationAccess.Domain;

internal sealed record InitialOrganizationState(Organization Organization, OrganizationMembership FirstOwnerMembership)
{
    public static InitialOrganizationState Create(
        OrganizationId organizationId,
        OrganizationMembershipId firstOwnerMembershipId,
        string displayName,
        LegalOrganizationIdentity legalIdentity,
        ICurrentPerson currentPerson,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(currentPerson);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var organization = Organization.Create(organizationId, displayName, legalIdentity, timeProvider);
        var firstOwnerMembership = OrganizationMembership.Create(
            firstOwnerMembershipId,
            organizationId,
            currentPerson.PersonId,
            [OrganizationRole.Owner],
            timeProvider);

        return new InitialOrganizationState(organization, firstOwnerMembership);
    }
}
