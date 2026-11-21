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
        return AgreementVersionFactsMapper.From(version, coverage, groups);
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
        return (versions.Select(version => AgreementVersionFactsMapper.From(version, coverage[version.Id], groups[version.Id]))
            .ToArray(), hasMore);
    }
}
