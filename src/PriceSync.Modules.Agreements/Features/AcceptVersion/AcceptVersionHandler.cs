using PriceSync.Modules.Agreements.Acceptance;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Features.AcceptVersion;

internal sealed class AcceptVersionHandler(AcceptanceScopeReader scopes, AgreementAcceptanceStore acceptances,
    ICurrentPerson person, ICurrentOrganization organization, IOrganizationAuthorizer authorizer)
{
    internal async Task<AcceptanceResult> HandleAsync(AcceptVersionCommand command, CancellationToken cancellationToken)
    {
        if (organization.OrganizationId is not { } acting)
            return new AcceptanceResult.ActingOrganizationRequired();
        if (!await authorizer.HasCapabilityAsync(person.PersonId, acting, OrganizationCapability.ManageCustomerAgreements, cancellationToken))
            return new AcceptanceResult.Forbidden();

        var scope = await scopes.FindAsync(command.AgreementId, command.VersionId, cancellationToken);
        if (scope is not { } version || version.Details.CustomerOrganizationId != acting)
            return new AcceptanceResult.NotFound();
        if (version.Details.Status != TradingRelationshipStatus.Active)
            return new AcceptanceResult.RelationshipEnded();

        var acceptance = await acceptances.AcceptAsync(version.RelationshipId, version.VersionId,
            person.PersonId, cancellationToken);
        return new AcceptanceResult.Success(acceptance);
    }
}
