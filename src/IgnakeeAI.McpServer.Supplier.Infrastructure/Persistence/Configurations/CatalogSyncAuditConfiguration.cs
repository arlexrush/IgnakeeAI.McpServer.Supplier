using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence.Configurations;

public sealed class CatalogSyncAuditConfiguration : IEntityTypeConfiguration<CatalogSyncAuditEntity>
{
    public void Configure(EntityTypeBuilder<CatalogSyncAuditEntity> builder)
    {
        builder.ToTable("CatalogSyncAudits");
        builder.HasKey(audit => audit.SyncId);
        builder.Property(audit => audit.Source).HasMaxLength(32).IsRequired();
        builder.Property(audit => audit.ErpProvider).HasMaxLength(64);
        builder.Property(audit => audit.Error).HasMaxLength(4000);
        builder.HasIndex(audit => audit.StartedAt);
        builder.HasIndex(audit => new { audit.Source, audit.CompletedAt });
    }
}
