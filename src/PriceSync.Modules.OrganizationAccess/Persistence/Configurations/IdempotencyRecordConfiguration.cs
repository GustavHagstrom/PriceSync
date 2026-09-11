using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.Modules.OrganizationAccess.Persistence.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records", OrganizationAccessDbContext.SchemaName);

        builder.HasKey(record => new { record.PersonId, record.Operation, record.IdempotencyKey })
            .HasName("pk_idempotency_records");

        builder.Property(record => record.PersonId)
            .HasColumnName("person_id")
            .HasConversion(id => id.Value, value => new PersonId(value))
            .ValueGeneratedNever();

        builder.Property(record => record.Operation)
            .HasColumnName("operation")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(record => record.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(record => record.RequestHash)
            .HasColumnName("request_hash")
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(record => record.ResponseStatus)
            .HasColumnName("response_status")
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(record => record.ResponseBody)
            .HasColumnName("response_body")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(record => record.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
    }
}
