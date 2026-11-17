using ECommerce.Domain.Entities.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs");
        b.Property(x => x.Action).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityId).HasMaxLength(100);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.Metadata).HasMaxLength(2000);
        b.HasIndex(x => new { x.TenantId, x.CreatedAtUtc });
        b.HasIndex(x => x.ActorId);
    }
}

public class QueuedEmailConfiguration : IEntityTypeConfiguration<QueuedEmail>
{
    public void Configure(EntityTypeBuilder<QueuedEmail> b)
    {
        b.ToTable("EmailQueue");
        b.Property(x => x.ToEmail).HasMaxLength(256).IsRequired();
        b.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        b.Property(x => x.Body).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Category).HasMaxLength(60);
        b.Property(x => x.LastError).HasMaxLength(1000);
        b.HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
    }
}
