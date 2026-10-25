using PriceSync.Modules.Agreements.Models;
using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Features.Get;

internal sealed class GetHandler(AgreementsDbContext db, ICurrentPerson person, ICurrentOrganization organization,
    IOrganizationAuthorizer authorizer, ITradingRelationshipReader relationships)
{
    internal async Task<GetResult> HandleAsync(GetQuery query, CancellationToken cancellationToken)
    {
        if (organization.OrganizationId is not { } acting) return new GetResult.ActingOrganizationRequired();
        if (!await authorizer.HasCapabilityAsync(person.PersonId, acting, OrganizationCapability.ReadAgreements, cancellationToken))
            return new GetResult.Forbidden();
        var agreement = query.AgreementId == Guid.Empty ? null : await db.PriceAgreements.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == new AgreementId(query.AgreementId), cancellationToken);
        var relationship = agreement is null ? null : await relationships.GetAsync(agreement.TradingRelationshipId, cancellationToken);
        if (relationship is null || (relationship.SupplierOrganizationId != acting && relationship.CustomerOrganizationId != acting))
            return new GetResult.NotFound();
        return new GetResult.Success(AgreementResponse.From(agreement!));
    }
}
