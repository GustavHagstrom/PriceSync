using System.Data;
using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Persistence;

internal enum SelectionChange { Changed, Unchanged, Conflict, VersionNotFound }

internal sealed class CustomerAgreementSelectionStore(AgreementsDbContext db, TimeProvider clock)
{
    internal async Task<SelectionChange> ChangeAsync(TradingRelationshipId relationshipId,
        AgreementVersionId? from, AgreementVersionId? to, PersonId personId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var references = new[] { from, to }.OfType<AgreementVersionId>().Distinct().ToArray();
        if (references.Length > 0 && await db.Versions.CountAsync(
                x => x.TradingRelationshipId == relationshipId && references.Contains(x.Id),
                cancellationToken) != references.Length)
            return SelectionChange.VersionNotFound;

        // The unique key serializes concurrent inserts when this relationship has no state yet.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO agreements.customer_agreement_states ("TradingRelationshipId", "Sequence")
            VALUES ({relationshipId.Value}, 0)
            ON CONFLICT ("TradingRelationshipId") DO NOTHING
            """, cancellationToken);
        var rows = await db.Set<CustomerAgreementState>().FromSqlInterpolated($"""
            SELECT * FROM agreements.customer_agreement_states
            WHERE "TradingRelationshipId" = {relationshipId.Value}
            FOR UPDATE
            """).AsNoTracking().ToListAsync(cancellationToken);
        var current = rows.Single();

        if (current.ActiveVersionId == to)
            return SelectionChange.Unchanged;
        if (current.ActiveVersionId != from)
            return SelectionChange.Conflict;

        var now = clock.GetUtcNow();
        var decidedAt = now;
        if (current.Sequence > 0)
        {
            // Read the preceding transition while holding the relationship state lock.
            var previousDecision = await db.Set<CustomerAgreementTransition>()
                .Where(x => x.TradingRelationshipId == relationshipId && x.Sequence == current.Sequence)
                .Select(x => x.DecidedAt)
                .SingleAsync(cancellationToken);
            if (previousDecision > decidedAt)
                decidedAt = previousDecision;
        }

        if (to is { } destination)
            await new AgreementAcceptanceStore(db, clock).AcceptAsync(relationshipId, destination, personId,
                now, cancellationToken);

        var nextSequence = checked(current.Sequence + 1);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE agreements.customer_agreement_states
            SET "ActiveVersionId" = {to?.Value}, "Sequence" = {nextSequence}
            WHERE "TradingRelationshipId" = {relationshipId.Value}
            """, cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO agreements.customer_agreement_transitions
                ("TradingRelationshipId", "Sequence", "FromVersionId", "ToVersionId", "PersonId", "DecidedAt")
            VALUES ({relationshipId.Value}, {nextSequence}, {from?.Value}, {to?.Value}, {personId.Value}, {decidedAt})
            """, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SelectionChange.Changed;
    }
}
