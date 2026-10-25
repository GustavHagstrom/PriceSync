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
    internal DbSet<AgreementCreationReplay> CreationReplays => Set<AgreementCreationReplay>();

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

        var replay = modelBuilder.Entity<AgreementCreationReplay>();
        replay.ToTable("agreement_creation_replays");
        replay.HasKey(x => new { x.PersonId, x.ActingOrganizationId, x.Operation, x.Key })
            .HasName("pk_agreement_creation_replays");
        replay.Property(x => x.PersonId).HasConversion(x => x.Value, x => new PersonId(x));
        replay.Property(x => x.ActingOrganizationId).HasConversion(x => x.Value, x => new OrganizationId(x));
        replay.Property(x => x.TradingRelationshipId).HasConversion(x => x.Value, x => TradingRelationshipId.FromGuid(x));
        replay.Property(x => x.Operation).HasMaxLength(100).UseCollation("C");
        replay.Property(x => x.Key).HasMaxLength(200).UseCollation("C");
    }
}
