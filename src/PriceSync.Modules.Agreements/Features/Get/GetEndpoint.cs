using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Features.Get;

internal static class GetEndpoint
{
    internal static async Task<IResult> HandleAsync(Guid agreementId, ICurrentPerson person,
        ICurrentOrganization organization, IOrganizationAuthorizer authorizer, ITradingRelationshipReader relationships,
        AgreementsDbContext db, CancellationToken cancellationToken)
    {
        if (organization.OrganizationId is not { } acting)
            return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { ["code"] = OrganizationContextErrors.Required });
        if (!await authorizer.HasCapabilityAsync(person.PersonId, acting, OrganizationCapability.ReadAgreements, cancellationToken))
            return Results.Problem(statusCode: 403, extensions: new Dictionary<string, object?> { ["code"] = "forbidden" });
        var agreement = agreementId == Guid.Empty ? null : await db.PriceAgreements.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == new AgreementId(agreementId), cancellationToken);
        var relationship = agreement is null ? null : await relationships.GetAsync(agreement.TradingRelationshipId, cancellationToken);
        if (relationship is null || (relationship.SupplierOrganizationId != acting && relationship.CustomerOrganizationId != acting))
            return Results.Problem(statusCode: 404, extensions: new Dictionary<string, object?> { ["code"] = "agreement_not_found" });
        return Results.Ok(AgreementResponse.From(agreement!));
    }
}
