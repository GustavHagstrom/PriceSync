using System.Text.Json;
using Microsoft.AspNetCore.Http;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Models;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Features.AgreementSelection;

internal sealed class AgreementSelectionHandler(ITradingRelationshipReader relationships,
    ICustomerAgreementSelectionReader selections, IAgreementVersionReader versions, CustomerAgreementSelectionStore store,
    ICurrentPerson person, ICurrentOrganization organization, IOrganizationAuthorizer authorizer, TimeProvider clock)
{
    internal async Task<IResult> GetAsync(Guid relationshipId, CancellationToken cancellationToken)
    {
        if (organization.OrganizationId is not { } acting)
            return Problem(400, OrganizationContextErrors.Required);
        if (!await authorizer.HasCapabilityAsync(person.PersonId, acting, OrganizationCapability.ReadAgreements, cancellationToken))
            return Problem(403, "forbidden");
        if (relationshipId == Guid.Empty)
            return Problem(404, "trading_relationship_not_found");
        var id = TradingRelationshipId.FromGuid(relationshipId);
        var relationship = await relationships.GetAsync(id, cancellationToken);
        if (relationship is null || (relationship.CustomerOrganizationId != acting && relationship.SupplierOrganizationId != acting))
            return Problem(404, "trading_relationship_not_found");

        var active = await selections.GetActiveVersionIdAsync(id, cancellationToken);
        return await RespondAsync(active, cancellationToken);
    }

    internal async Task<IResult> ChangeAsync(Guid relationshipId, ChangeAgreementSelectionRequest request,
        CancellationToken cancellationToken)
    {
        if (organization.OrganizationId is not { } acting)
            return Problem(400, OrganizationContextErrors.Required);
        if (!await authorizer.HasCapabilityAsync(person.PersonId, acting, OrganizationCapability.ManageCustomerAgreements, cancellationToken))
            return Problem(403, "forbidden");
        if (relationshipId == Guid.Empty)
            return Problem(404, "trading_relationship_not_found");
        var id = TradingRelationshipId.FromGuid(relationshipId);
        var relationship = await relationships.GetAsync(id, cancellationToken);
        if (relationship is null || relationship.CustomerOrganizationId != acting)
            return Problem(404, "trading_relationship_not_found");

        var errors = new Dictionary<string, string[]>();
        if (!TryReadVersionId(request.FromVersionId, out var from))
            errors.Add("fromVersionId", ["An explicit null or a valid version ID is required."]);
        if (!TryReadVersionId(request.ToVersionId, out var to))
            errors.Add("toVersionId", ["An explicit null or a valid version ID is required."]);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);
        if (to is not null && relationship.Status != TradingRelationshipStatus.Active)
            return Problem(409, "agreement_relationship_ended");

        return await store.ChangeAsync(id, from, to, person.PersonId, cancellationToken) switch
        {
            SelectionChange.Changed or SelectionChange.Unchanged => await RespondAsync(to, cancellationToken),
            SelectionChange.Conflict => Problem(409, "agreement_selection_conflict",
                "Reload the current agreement selection before retrying."),
            SelectionChange.VersionNotFound => Problem(404, "agreement_version_not_found"),
            _ => throw new InvalidOperationException("Unexpected agreement selection result."),
        };
    }

    private static bool TryReadVersionId(JsonElement value, out AgreementVersionId? id)
    {
        id = null;
        if (value.ValueKind == JsonValueKind.Null)
            return true;
        if (value.ValueKind != JsonValueKind.String || !value.TryGetGuid(out var guid) || guid == Guid.Empty)
            return false;
        id = new AgreementVersionId(guid);
        return true;
    }

    private async Task<IResult> RespondAsync(AgreementVersionId? active, CancellationToken cancellationToken)
    {
        if (active is not { } versionId)
            return Results.Ok(new AgreementSelectionResponse(null, []));

        var version = await versions.GetAsync(versionId, cancellationToken)
            ?? throw new InvalidOperationException("The selected agreement version could not be read.");
        var date = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return Results.Ok(new AgreementSelectionResponse(versionId.Value, VersionResponse.GetWarnings(version, date)));
    }

    private static IResult Problem(int status, string code, string? detail = null) => Results.Problem(
        statusCode: status, detail: detail, extensions: new Dictionary<string, object?> { ["code"] = code });
}
