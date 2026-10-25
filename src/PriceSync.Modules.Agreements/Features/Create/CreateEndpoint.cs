using Microsoft.AspNetCore.Http;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Features.Create;

internal static class CreateEndpoint
{
    internal static async Task<IResult> HandleAsync(CreateRequest request, ICurrentPerson person,
        ICurrentOrganization organization, IOrganizationAuthorizer authorizer, ITradingRelationshipReader relationships,
        AgreementsDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (organization.OrganizationId is not { } acting)
            return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { ["code"] = OrganizationContextErrors.Required });
        if (!await authorizer.HasCapabilityAsync(person.PersonId, acting, OrganizationCapability.PublishAgreements, cancellationToken))
            return Results.Problem(statusCode: 403, extensions: new Dictionary<string, object?> { ["code"] = "forbidden" });
        if (!Guid.TryParse(request.TradingRelationshipId, out var relationshipId) || relationshipId == Guid.Empty)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tradingRelationshipId"] = ["Trading Relationship is required."] },
                extensions: new Dictionary<string, object?> { ["code"] = "validation_failed" });
        var relationship = await relationships.GetAsync(TradingRelationshipId.FromGuid(relationshipId), cancellationToken);
        if (relationship is null || relationship.SupplierOrganizationId != acting)
            return Results.Problem(statusCode: 404, extensions: new Dictionary<string, object?> { ["code"] = "agreement_not_found" });
        if (relationship.Status == TradingRelationshipStatus.Ended)
            return Results.Problem(statusCode: 409, extensions: new Dictionary<string, object?> { ["code"] = "agreement_relationship_ended" });
        var now = clock.GetUtcNow();
        // Match PostgreSQL microsecond precision in the initial response and subsequent reads.
        var createdAt = new DateTimeOffset(now.UtcTicks - now.UtcTicks % 10, TimeSpan.Zero);
        var agreement = PriceAgreement.Create(TradingRelationshipId.FromGuid(relationshipId), person.PersonId, createdAt);
        db.PriceAgreements.Add(agreement);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/v1/agreements/{agreement.Id}", AgreementResponse.From(agreement));
    }
}
