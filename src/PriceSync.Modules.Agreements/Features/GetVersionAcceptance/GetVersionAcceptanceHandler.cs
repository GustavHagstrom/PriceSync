using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.Agreements.Acceptance;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.Agreements.Features.GetVersionAcceptance;

internal sealed class GetVersionAcceptanceHandler(AcceptanceScopeReader scopes, AgreementsDbContext db,
    ICurrentPerson person, ICurrentOrganization organization, IOrganizationAuthorizer authorizer)
{
    internal async Task<AcceptanceResult> HandleAsync(GetVersionAcceptanceQuery query, CancellationToken cancellationToken)
    {
        if (organization.OrganizationId is not { } acting)
            return new AcceptanceResult.ActingOrganizationRequired();
        if (!await authorizer.HasCapabilityAsync(person.PersonId, acting, OrganizationCapability.ReadAgreements, cancellationToken))
            return new AcceptanceResult.Forbidden();

        var scope = await scopes.FindAsync(query.AgreementId, query.VersionId, cancellationToken);
        if (scope is not { } version ||
            (version.Details.CustomerOrganizationId != acting && version.Details.SupplierOrganizationId != acting))
            return new AcceptanceResult.NotFound();

        var acceptance = await db.Set<AgreementAcceptance>().AsNoTracking().SingleOrDefaultAsync(
            x => x.TradingRelationshipId == version.RelationshipId && x.VersionId == version.VersionId, cancellationToken);
        return new AcceptanceResult.Success(acceptance);
    }
}
