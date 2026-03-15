using IgnakeeAI.McpServer.Supplier.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence.Configurations
{
    public class CatalogProductConfiguration : IEntityTypeConfiguration<CatalogProduct>
    {
        public void Configure(EntityTypeBuilder<CatalogProduct> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.ItemCode).HasMaxLength(100).IsRequired();
            builder.Property(e => e.Description).HasMaxLength(500).IsRequired();
            builder.Property(e => e.Category).HasMaxLength(100).IsRequired();
            builder.Property(e => e.Keywords).HasMaxLength(1000);
            builder.Property(e => e.Unit).HasMaxLength(20).IsRequired();
            builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            builder.Property(e => e.Specification).HasMaxLength(500);
            builder.Property(e => e.Presentation).HasMaxLength(200);
            builder.Property(e => e.ProductUrl).HasMaxLength(1000);

            builder.HasIndex(e => new { e.Category, e.IsActive })
                .HasName("ix_product_category_active");
            builder.HasIndex(e => e.ItemCode)
                .HasName("ix_product_itemcode");
        }
    }
}
