using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Models;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Features.ListVersions;

internal sealed class ListVersionsHandler(AgreementsDbContext db, AgreementVersionReader reader, ICurrentPerson person,
    ICurrentOrganization organization, IOrganizationAuthorizer authorizer, ITradingRelationshipReader relationships, TimeProvider clock)
{
    internal async Task<ListVersionsResult> HandleAsync(ListVersionsQuery query, CancellationToken cancellationToken)
    {
        var (agreementId, pageSize, cursor) = query;
        if (organization.OrganizationId is not { } acting) return new ListVersionsResult.ActingOrganizationRequired();
        if (!await authorizer.HasCapabilityAsync(person.PersonId, acting, OrganizationCapability.ReadAgreements, cancellationToken))
            return new ListVersionsResult.Forbidden();
        if (agreementId == Guid.Empty) return new ListVersionsResult.NotFound();

        var id = new AgreementId(agreementId);
        var agreement = await db.PriceAgreements.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (agreement is null) return new ListVersionsResult.NotFound();
        var relationship = await relationships.GetAsync(agreement.TradingRelationshipId, cancellationToken);
        if (relationship is null || (relationship.SupplierOrganizationId != acting && relationship.CustomerOrganizationId != acting))
            return new ListVersionsResult.NotFound();

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
        var date = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return new ListVersionsResult.Success(new ListVersionsResponse(
            page.Items.Select(facts => VersionResponse.From(facts, date)).ToArray(),
            page.HasMore ? VersionCursor.Encode(id, page.Items[^1].Ordinal) : null));
    }

    private static ListVersionsResult Invalid(string field, string message) =>
        new ListVersionsResult.ValidationFailed(new Dictionary<string, string[]> { [field] = [message] });
}
