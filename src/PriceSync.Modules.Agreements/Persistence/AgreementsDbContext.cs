using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.Agreements.Domain;

namespace PriceSync.Modules.Agreements.Persistence;

internal sealed class AgreementsDbContext(DbContextOptions<AgreementsDbContext> options) : DbContext(options)
{
    internal const string SchemaName = "agreements";
    internal const string MigrationsHistoryTableName = "__EFMigrationsHistory";
    internal DbSet<PriceAgreement> PriceAgreements => Set<PriceAgreement>();
    internal DbSet<PriceAgreementVersion> Versions => Set<PriceAgreementVersion>();
    internal DbSet<AgreementVersionWithdrawal> Withdrawals => Set<AgreementVersionWithdrawal>();
    internal const string WithdrawalKey = "pk_agreement_version_withdrawals";
    internal DbSet<AgreementVersionCoverage> Coverage => Set<AgreementVersionCoverage>();
    internal DbSet<AgreementVersionGroupDiscount> GroupDiscounts => Set<AgreementVersionGroupDiscount>();
    internal DbSet<CreationIdempotencyRecord> CreationIdempotencyRecords => Set<CreationIdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgreementsDbContext).Assembly);
    }
}
