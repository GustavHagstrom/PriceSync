using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Models;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Features.ListVersions;

internal sealed class ListVersionsHandler(AgreementsDbContext db, AgreementVersionReader reader, ICurrentPerson person,
    ICurrentOrganization organization, IOrganizationAuthorizer authorizer, ITradingRelationshipReader relationships)
{
    internal async Task<IResult> HandleAsync(Guid agreementId, string? pageSize, string? cursor, CancellationToken cancellationToken)
    {
        if (organization.OrganizationId is not { } acting) return Problem(400, OrganizationContextErrors.Required);
        if (!await authorizer.HasCapabilityAsync(person.PersonId, acting, OrganizationCapability.ReadAgreements, cancellationToken))
            return Problem(403, "forbidden");
        if (agreementId == Guid.Empty) return Problem(404, "agreement_not_found");

        var id = new AgreementId(agreementId);
        var agreement = await db.PriceAgreements.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (agreement is null) return Problem(404, "agreement_not_found");
        var relationship = await relationships.GetAsync(agreement.TradingRelationshipId, cancellationToken);
        if (relationship is null || (relationship.SupplierOrganizationId != acting && relationship.CustomerOrganizationId != acting))
            return Problem(404, "agreement_not_found");

        var limit = 50;
        if (pageSize is not null && (!int.TryParse(pageSize, NumberStyles.None, CultureInfo.InvariantCulture, out limit) || limit is < 1 or > 100))
            return Invalid("pageSize", "Page size must be an integer from 1 to 100.");
        long? before = null;
        if (cursor is not null)
        {
            if (!VersionCursor.TryParse(cursor, id, out var ordinal) || ordinal > agreement.LatestOrdinal)
                return Invalid("cursor", "Cursor is invalid for this Agreement.");
            before = ordinal;
        }

        var page = await reader.ReadPageAsync(id, before, limit, cancellationToken);
        return Results.Ok(new
        {
            items = page.Items.Select(VersionResponse.From).ToArray(),
            nextCursor = page.HasMore ? VersionCursor.Encode(id, page.Items[^1].Ordinal) : null,
        });
    }

    private static IResult Invalid(string field, string message) => Results.ValidationProblem(
        new Dictionary<string, string[]> { [field] = [message] },
        extensions: new Dictionary<string, object?> { ["code"] = "validation_failed" });

    private static IResult Problem(int status, string code) => Results.Problem(statusCode: status,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
