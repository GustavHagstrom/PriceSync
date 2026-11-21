using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Models;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Features.GetVersion;

internal sealed class GetVersionHandler(AgreementsDbContext db, ICurrentPerson person, ICurrentOrganization organization,
    IOrganizationAuthorizer authorizer, ITradingRelationshipReader relationships)
{
    internal async Task<IResult> HandleAsync(Guid agreementId, Guid versionId, CancellationToken cancellationToken)
    {
        if (organization.OrganizationId is not { } acting) return Problem(400, OrganizationContextErrors.Required);
        if (!await authorizer.HasCapabilityAsync(person.PersonId, acting, OrganizationCapability.ReadAgreements, cancellationToken))
            return Problem(403, "forbidden");
        if (agreementId == Guid.Empty || versionId == Guid.Empty) return NotFound();
        var agreement = new AgreementId(agreementId);
        var id = new AgreementVersionId(versionId);
        var version = await db.Versions.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == id && x.AgreementId == agreement, cancellationToken);
        if (version is null) return NotFound();
        var relationship = await relationships.GetAsync(version.TradingRelationshipId, cancellationToken);
        if (relationship is null || (relationship.SupplierOrganizationId != acting && relationship.CustomerOrganizationId != acting))
            return NotFound();
        var coverage = await db.Coverage.AsNoTracking().SingleAsync(x => x.VersionId == id, cancellationToken);
        return Results.Ok(VersionResponse.From(version, coverage));
    }

    private static IResult NotFound() => Problem(404, "agreement_version_not_found");

    private static IResult Problem(int status, string code) => Results.Problem(statusCode: status,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
