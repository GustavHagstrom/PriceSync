using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Contracts;

/// <summary>Reads agreement selections for trusted module consumers, scoped to one trading relationship.</summary>
public interface ICustomerAgreementSelectionReader
{
    /// <summary>Returns the exact active version ID, or null when there is no selection. Technical failures propagate.</summary>
    Task<AgreementVersionId?> GetActiveVersionIdAsync(TradingRelationshipId relationshipId,
        CancellationToken cancellationToken = default);

    /// <summary>Reconstructs the active version at an inclusive instant or sequence. Supply at most one boundary.
    /// A sequence of zero precedes the first transition. Omitting both boundaries reads the current selection.</summary>
    Task<AgreementVersionId?> GetActiveVersionIdAsync(TradingRelationshipId relationshipId,
        DateTimeOffset? asOfUtc, long? throughSequence, CancellationToken cancellationToken = default);

    /// <summary>Reads transitions in ascending sequence order after an exclusive nonnegative cursor.
    /// The page size defaults to 50 and must be between 1 and 100.</summary>
    Task<CustomerAgreementHistoryPage> GetHistoryAsync(TradingRelationshipId relationshipId,
        long? afterSequence = null, int pageSize = 50, CancellationToken cancellationToken = default);
}

public sealed record CustomerAgreementHistoryPage(IReadOnlyList<CustomerAgreementHistoryItem> Items,
    long? NextAfterSequence);

public sealed record CustomerAgreementHistoryItem(long Sequence, AgreementVersionId? FromVersionId,
    AgreementVersionId? ToVersionId, PersonId PersonId, DateTimeOffset DecidedAt);
