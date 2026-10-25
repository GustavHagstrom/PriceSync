using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Features.List;

internal sealed class ListHandler(AgreementsDbContext db, ICurrentPerson person, ICurrentOrganization organization,
    IOrganizationAuthorizer authorizer, ITradingRelationshipReader relationships, ListValidator validator)
{
    internal async Task<ListResult> HandleAsync(ListQuery query, CancellationToken cancellationToken)
    {
        if (organization.OrganizationId is not { } acting) return new ListResult.ActingOrganizationRequired();
        if (!await authorizer.HasCapabilityAsync(person.PersonId, acting, OrganizationCapability.ReadAgreements, cancellationToken))
            return new ListResult.Forbidden();
        if (!Guid.TryParse(query.TradingRelationshipId, out var id) || id == Guid.Empty)
            return new ListResult.ValidationFailed(new Dictionary<string, string[]> { ["tradingRelationshipId"] = ["Trading Relationship is required."] });
        var relationshipId = TradingRelationshipId.FromGuid(id);
        var relationship = await relationships.GetAsync(relationshipId, cancellationToken);
        if (relationship is null || (relationship.SupplierOrganizationId != acting && relationship.CustomerOrganizationId != acting))
            return new ListResult.NotFound();
        var errors = validator.Validate(query, out var pageSize, out var cursor);
        if (errors.Count != 0) return new ListResult.ValidationFailed(errors);
        var agreementsQuery = db.PriceAgreements.AsNoTracking().Where(x => x.TradingRelationshipId == relationshipId);
        if (cursor is not null)
        {
            var createdAt = cursor.CreatedAt;
            var agreementId = cursor.Id;
            agreementsQuery = agreementsQuery.Where(x => EF.Functions.LessThan(
                ValueTuple.Create(x.CreatedAt, x.Id), ValueTuple.Create(createdAt, agreementId)));
        }
        var agreements = await agreementsQuery.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Take(pageSize + 1).ToListAsync(cancellationToken);
        string? nextCursor = null;
        if (agreements.Count > pageSize)
        {
            agreements.RemoveAt(pageSize);
            nextCursor = new ListCursor(agreements[^1].CreatedAt, agreements[^1].Id).Encode();
        }
        return new ListResult.Success(new ListResponse(agreements.Select(AgreementResponse.From).ToArray(), nextCursor));
    }
}
