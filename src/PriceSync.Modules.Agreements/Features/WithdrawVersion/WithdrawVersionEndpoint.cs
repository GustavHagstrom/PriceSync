using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.Agreements.Models;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Features.WithdrawVersion;

internal sealed record WithdrawalRequest(string? Reason);

internal static class WithdrawVersionEndpoint
{
    internal static async Task<IResult> HandleAsync(Guid agreementId, Guid versionId, WithdrawalRequest request,
        AgreementsDbContext db, ICurrentPerson person, ICurrentOrganization organization,
        IOrganizationAuthorizer authorizer, ITradingRelationshipReader relationships, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (organization.OrganizationId is not { } acting) return Problem(400, OrganizationContextErrors.Required);
        if (!await authorizer.HasCapabilityAsync(person.PersonId, acting, OrganizationCapability.PublishAgreements, cancellationToken))
            return Problem(403, "forbidden");
        if (agreementId == Guid.Empty || versionId == Guid.Empty) return Problem(404, "agreement_version_not_found");
        var agreement = new AgreementId(agreementId);
        var id = new AgreementVersionId(versionId);
        var version = await db.Versions.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == id && x.AgreementId == agreement, cancellationToken);
        if (version is null) return Problem(404, "agreement_version_not_found");
        var relationship = await relationships.GetAsync(version.TradingRelationshipId, cancellationToken);
        if (relationship is null || relationship.SupplierOrganizationId != acting)
            return Problem(404, "agreement_version_not_found");
        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.EnumerateRunes().Count() > 2000 || reason.Contains('\0'))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["reason"] = ["Enter a reason of 1–2000 characters without NUL."] },
                extensions: new Dictionary<string, object?> { ["code"] = "validation_failed" });

        var existing = await db.Withdrawals.AsNoTracking().SingleOrDefaultAsync(x => x.VersionId == id, cancellationToken);
        if (existing is not null) return Resolve(existing, reason);

        var now = clock.GetUtcNow();
        var withdrawal = new AgreementVersionWithdrawal(id, reason, person.PersonId,
            new DateTimeOffset(now.UtcTicks - now.UtcTicks % 10, TimeSpan.Zero));
        db.Withdrawals.Add(withdrawal);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: AgreementsDbContext.WithdrawalKey })
        {
            db.Entry(withdrawal).State = EntityState.Detached;
            existing = await db.Withdrawals.AsNoTracking().SingleAsync(x => x.VersionId == id, cancellationToken);
            return Resolve(existing, reason);
        }
        return Resolve(withdrawal, reason);
    }

    private static IResult Resolve(AgreementVersionWithdrawal withdrawal, string reason) =>
        string.Equals(withdrawal.Reason, reason, StringComparison.Ordinal)
            ? Results.Ok(new WithdrawalResponse(withdrawal.Reason, withdrawal.WithdrawnByPersonId.Value, withdrawal.WithdrawnAt))
            : Problem(409, "agreement_withdrawal_reason_conflict");

    private static IResult Problem(int status, string code) => Results.Problem(statusCode: status,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
