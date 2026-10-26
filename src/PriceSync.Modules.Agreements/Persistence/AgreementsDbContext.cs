using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.Agreements.Contracts;
using PriceSync.Modules.Agreements.Domain;
using PriceSync.Modules.OrganizationAccess.Contracts;
using PriceSync.Modules.TradingRelationships.Contracts;

namespace PriceSync.Modules.Agreements.Persistence;

internal sealed class AgreementsDbContext(DbContextOptions<AgreementsDbContext> options) : DbContext(options)
{
    internal const string SchemaName = "agreements";
    internal const string MigrationsHistoryTableName = "__EFMigrationsHistory";
    internal DbSet<PriceAgreement> PriceAgreements => Set<PriceAgreement>();
    internal DbSet<CreationIdempotencyRecord> CreationIdempotencyRecords => Set<CreationIdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        var agreement = modelBuilder.Entity<PriceAgreement>();
        agreement.ToTable("price_agreements");
        agreement.HasKey(x => x.Id).HasName("pk_price_agreements");
        agreement.Property(x => x.Id).HasConversion(x => x.Value, x => new AgreementId(x)).ValueGeneratedNever();
        agreement.Property(x => x.TradingRelationshipId).HasConversion(x => x.Value, x => TradingRelationshipId.FromGuid(x));
        agreement.Property(x => x.CreatedByPersonId).HasConversion(x => x.Value, x => new PersonId(x));
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
