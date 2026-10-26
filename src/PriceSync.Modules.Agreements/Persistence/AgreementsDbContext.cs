using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;
using PriceSync.Modules.PricePublication.Contracts;

namespace PriceSync.Modules.Agreements.Persistence;

internal sealed class AgreementsDbContext(DbContextOptions<AgreementsDbContext> options) : DbContext(options)
{
    internal const string SchemaName = "agreements";
    internal const string MigrationsHistoryTableName = "__EFMigrationsHistory";
    internal DbSet<PriceAgreement> PriceAgreements => Set<PriceAgreement>();
    internal DbSet<PriceAgreementVersion> Versions => Set<PriceAgreementVersion>();
    internal DbSet<AgreementVersionCoverage> Coverage => Set<AgreementVersionCoverage>();
    internal DbSet<CreationIdempotencyRecord> CreationIdempotencyRecords => Set<CreationIdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        var agreement = modelBuilder.Entity<PriceAgreement>();
        agreement.ToTable("price_agreements", table => table.HasCheckConstraint("ck_agreement_latest",
            "\"LatestVersionId\" IS NULL AND \"LatestOrdinal\" = 0 OR \"LatestVersionId\" IS NOT NULL AND \"LatestOrdinal\" > 0"));
        agreement.HasKey(x => x.Id).HasName("pk_price_agreements");
        agreement.Property(x => x.Id).HasConversion(x => x.Value, x => new AgreementId(x)).ValueGeneratedNever();
        agreement.Property(x => x.TradingRelationshipId).HasConversion(x => x.Value, x => TradingRelationshipId.FromGuid(x));
        agreement.Property(x => x.CreatedByPersonId).HasConversion(x => x.Value, x => new PersonId(x));
        agreement.Property(x => x.LatestVersionId).HasConversion(x => x!.Value.Value, x => new AgreementVersionId(x));
        agreement.HasAlternateKey(x => new { x.Id, x.TradingRelationshipId });

        var version = modelBuilder.Entity<PriceAgreementVersion>();
        version.ToTable("agreement_versions");
        version.HasKey(x => x.Id);
        version.Property(x => x.Id).HasConversion(x => x.Value, x => new AgreementVersionId(x)).ValueGeneratedNever();
        version.Property(x => x.AgreementId).HasConversion(x => x.Value, x => new AgreementId(x));
        version.Property(x => x.TradingRelationshipId).HasConversion(x => x.Value, x => TradingRelationshipId.FromGuid(x));
        version.Property(x => x.PublishedByPersonId).HasConversion(x => x.Value, x => new PersonId(x));
        version.HasIndex(x => new { x.AgreementId, x.Ordinal }).IsUnique();
        version.HasAlternateKey(x => new { x.AgreementId, x.TradingRelationshipId, x.Id, x.Ordinal });
        version.HasOne<PriceAgreement>().WithMany().HasForeignKey(x => new { x.AgreementId, x.TradingRelationshipId })
            .HasPrincipalKey(x => new { x.Id, x.TradingRelationshipId }).OnDelete(DeleteBehavior.Restrict);
        agreement.HasOne<PriceAgreementVersion>().WithMany()
            .HasForeignKey(x => new { x.Id, x.TradingRelationshipId, x.LatestVersionId, x.LatestOrdinal })
            .HasPrincipalKey(x => new { x.AgreementId, x.TradingRelationshipId, x.Id, x.Ordinal }).OnDelete(DeleteBehavior.Restrict);

        var coverage = modelBuilder.Entity<AgreementVersionCoverage>();
        coverage.ToTable("agreement_version_coverage");
        coverage.HasKey(x => new { x.VersionId, x.PriceListId });
        coverage.Property(x => x.VersionId).HasConversion(x => x.Value, x => new AgreementVersionId(x));
        coverage.Property(x => x.PriceListId).HasConversion(x => x.Value, x => new PriceListId(x));
        coverage.Property(x => x.GeneralDiscount).HasColumnType("numeric");
        coverage.HasOne<PriceAgreementVersion>().WithMany().HasForeignKey(x => x.VersionId).OnDelete(DeleteBehavior.Restrict);
        agreement.HasIndex(x => new { x.TradingRelationshipId, x.CreatedAt, x.Id })
            .HasDatabaseName("ix_price_agreements_relationship_created_id");

        var idempotencyRecord = modelBuilder.Entity<CreationIdempotencyRecord>();
        idempotencyRecord.ToTable("creation_idempotency_records");
        idempotencyRecord.HasKey(x => new { x.PersonId, x.ActingOrganizationId, x.Operation, x.Key })
            .HasName("pk_creation_idempotency_records");
        idempotencyRecord.Property(x => x.PersonId).HasConversion(x => x.Value, x => new PersonId(x));
        idempotencyRecord.Property(x => x.ActingOrganizationId).HasConversion(x => x.Value, x => new OrganizationId(x));
        idempotencyRecord.Property(x => x.TradingRelationshipId).HasConversion(x => x.Value, x => TradingRelationshipId.FromGuid(x));
        idempotencyRecord.Property(x => x.Operation).HasMaxLength(100).UseCollation("C");
        idempotencyRecord.Property(x => x.Key).HasMaxLength(200).UseCollation("C");
    }
}
