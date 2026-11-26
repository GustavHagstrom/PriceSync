using Microsoft.EntityFrameworkCore;
using Npgsql;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.Agreements.Models;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Features.WithdrawVersion;

internal sealed class WithdrawVersionHandler(AgreementsDbContext db, ICurrentPerson person,
    ICurrentOrganization organization, IOrganizationAuthorizer authorizer, ITradingRelationshipReader relationshipsReader,
    TimeProvider clock)
{
    internal async Task<WithdrawVersionResult> HandleAsync(WithdrawVersionCommand command, CancellationToken cancellationToken)
    {
        var (agreementGuid, versionGuid, reason) = command;
        if (organization.OrganizationId is not { } acting) return new WithdrawVersionResult.ActingOrganizationRequired();
        if (!await authorizer.HasCapabilityAsync(person.PersonId, acting, OrganizationCapability.PublishAgreements, cancellationToken))
            return new WithdrawVersionResult.Forbidden();
        if (agreementGuid == Guid.Empty || versionGuid == Guid.Empty) return new WithdrawVersionResult.NotFound();
        var agreementId = new AgreementId(agreementGuid);
        var versionId = new AgreementVersionId(versionGuid);
        var version = await db.Versions.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == versionId && x.AgreementId == agreementId, cancellationToken);
        if (version is null) return new WithdrawVersionResult.NotFound();
        var relationship = await relationshipsReader.GetAsync(version.TradingRelationshipId, cancellationToken);
        if (relationship is null || relationship.SupplierOrganizationId != acting)
            return new WithdrawVersionResult.NotFound();
        var existing = await db.Withdrawals.AsNoTracking().SingleOrDefaultAsync(x => x.VersionId == versionId, cancellationToken);
        if (existing is not null) return Resolve(existing, reason);

        var now = clock.GetUtcNow();
        var withdrawal = new AgreementVersionWithdrawal(versionId, reason, person.PersonId,
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
            existing = await db.Withdrawals.AsNoTracking().SingleAsync(x => x.VersionId == versionId, cancellationToken);
            return Resolve(existing, reason);
        }
        return Resolve(withdrawal, reason);
    }

    private static WithdrawVersionResult Resolve(AgreementVersionWithdrawal withdrawal, string reason) =>
        string.Equals(withdrawal.Reason, reason, StringComparison.Ordinal)
            ? new WithdrawVersionResult.Success(new WithdrawalResponse(withdrawal.Reason, withdrawal.WithdrawnByPersonId.Value, withdrawal.WithdrawnAt))
            : new WithdrawVersionResult.ReasonConflict();

}
