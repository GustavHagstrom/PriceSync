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
}
