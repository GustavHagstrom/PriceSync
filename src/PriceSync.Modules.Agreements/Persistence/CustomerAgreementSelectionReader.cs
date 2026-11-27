using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Persistence;

internal sealed class CustomerAgreementSelectionReader(AgreementsDbContext db) : ICustomerAgreementSelectionReader
{
    public Task<AgreementVersionId?> GetActiveVersionIdAsync(TradingRelationshipId relationshipId,
        CancellationToken cancellationToken = default) =>
        db.Set<CustomerAgreementState>()
            .Where(state => state.TradingRelationshipId == relationshipId)
            .Select(state => state.ActiveVersionId)
            .SingleOrDefaultAsync(cancellationToken);

    internal Task<AgreementVersionId?> GetActiveVersionIdAsync(TradingRelationshipId relationshipId,
        DateTimeOffset? asOfUtc, long? throughSequence, CancellationToken cancellationToken = default)
    {
        if (asOfUtc is not null && throughSequence is not null)
            throw new ArgumentException("Specify either an as-of timestamp or a through-sequence boundary.", nameof(throughSequence));
        ArgumentOutOfRangeException.ThrowIfNegative(throughSequence.GetValueOrDefault(), nameof(throughSequence));
        if (asOfUtc is null && throughSequence is null)
            return GetActiveVersionIdAsync(relationshipId, cancellationToken);

        var transitions = Transitions(relationshipId);
        if (asOfUtc is { } timestamp)
        {
            var utc = timestamp.ToUniversalTime();
            transitions = transitions.Where(transition => transition.DecidedAt <= utc);
        }
        if (throughSequence is { } sequence)
            transitions = transitions.Where(transition => transition.Sequence <= sequence);

        return transitions.OrderByDescending(transition => transition.Sequence)
            .Select(transition => transition.ToVersionId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    internal async Task<CustomerAgreementHistoryPage> GetHistoryAsync(TradingRelationshipId relationshipId,
        long? afterSequence = null, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var after = afterSequence.GetValueOrDefault();
        ArgumentOutOfRangeException.ThrowIfNegative(after, nameof(afterSequence));
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);

        var items = await Transitions(relationshipId)
            .Where(transition => transition.Sequence > after)
            .OrderBy(transition => transition.Sequence)
            .Take(pageSize + 1)
            .Select(transition => new CustomerAgreementHistoryItem(transition.Sequence, transition.FromVersionId,
                transition.ToVersionId, transition.PersonId, transition.DecidedAt))
            .ToListAsync(cancellationToken);

        long? nextAfterSequence = null;
        if (items.Count > pageSize)
        {
            items.RemoveAt(pageSize);
            nextAfterSequence = items[^1].Sequence;
        }
        return new CustomerAgreementHistoryPage(items, nextAfterSequence);
    }

    private IQueryable<CustomerAgreementTransition> Transitions(TradingRelationshipId relationshipId) =>
        db.Set<CustomerAgreementTransition>().AsNoTracking()
            .Where(transition => transition.TradingRelationshipId == relationshipId);
}

internal sealed record CustomerAgreementHistoryPage(IReadOnlyList<CustomerAgreementHistoryItem> Items,
    long? NextAfterSequence);

internal sealed record CustomerAgreementHistoryItem(long Sequence, AgreementVersionId? FromVersionId,
    AgreementVersionId? ToVersionId, PersonId PersonId, DateTimeOffset DecidedAt);
