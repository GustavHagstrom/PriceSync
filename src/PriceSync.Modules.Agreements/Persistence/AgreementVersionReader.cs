using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.Agreements.Models;

namespace PriceSync.Modules.Agreements.Persistence;

internal sealed class AgreementVersionReader(AgreementsDbContext db) : IAgreementVersionReader
{
    public async Task<AgreementVersionFacts?> GetAsync(AgreementVersionId versionId, CancellationToken cancellationToken = default)
    {
        var version = await db.Versions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == versionId, cancellationToken);
        return version is null ? null : await ReadFactsAsync(version, cancellationToken);
    }

    internal async Task<AgreementVersionFacts> ReadFactsAsync(PriceAgreementVersion version, CancellationToken cancellationToken)
    {
        var coverage = await db.Coverage.AsNoTracking().Where(x => x.VersionId == version.Id).ToArrayAsync(cancellationToken);
        var groups = await db.GroupDiscounts.AsNoTracking().Where(x => x.VersionId == version.Id).ToArrayAsync(cancellationToken);
        var withdrawal = await db.Withdrawals.AsNoTracking().SingleOrDefaultAsync(x => x.VersionId == version.Id, cancellationToken);
        return WithWithdrawal(AgreementVersionFactsMapper.From(version, coverage, groups), withdrawal);
    }

    internal async Task<(AgreementVersionFacts[] Items, bool HasMore)> ReadPageAsync(
        AgreementId agreement, long? before, int limit, CancellationToken cancellationToken)
    {
        var query = db.Versions.AsNoTracking().Where(x => x.AgreementId == agreement);
        if (before is { } ordinal) query = query.Where(x => x.Ordinal < ordinal);
        var versions = await query.OrderByDescending(x => x.Ordinal).Take(limit + 1).ToListAsync(cancellationToken);
        var hasMore = versions.Count > limit;
        if (hasMore) versions.RemoveAt(limit);
        if (versions.Count == 0) return ([], false);

        var ids = versions.Select(version => version.Id).ToArray();
        var coverage = (await db.Coverage.AsNoTracking().Where(x => ids.Contains(x.VersionId))
            .ToArrayAsync(cancellationToken)).ToLookup(x => x.VersionId);
        var groups = (await db.GroupDiscounts.AsNoTracking().Where(x => ids.Contains(x.VersionId))
            .ToArrayAsync(cancellationToken)).ToLookup(x => x.VersionId);
        var withdrawals = await db.Withdrawals.AsNoTracking().Where(x => ids.Contains(x.VersionId))
            .ToDictionaryAsync(x => x.VersionId, cancellationToken);
        return (versions.Select(version => WithWithdrawal(AgreementVersionFactsMapper.From(version, coverage[version.Id], groups[version.Id]),
                withdrawals.GetValueOrDefault(version.Id)))
            .ToArray(), hasMore);
    }

    private static AgreementVersionFacts WithWithdrawal(AgreementVersionFacts facts, AgreementVersionWithdrawal? withdrawal) =>
        withdrawal is null ? facts : facts with
        {
            Withdrawal = new(withdrawal.Reason, withdrawal.WithdrawnByPersonId, withdrawal.WithdrawnAt),
        };
}
