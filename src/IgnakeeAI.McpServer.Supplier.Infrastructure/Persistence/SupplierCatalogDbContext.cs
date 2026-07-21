using IgnakeeAI.McpServer.Supplier.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence
{

    /// <summary>
    /// DbContext del catálogo del proveedor.
    /// Soporta SQLite (por defecto), PostgreSQL, SQL Server y MySQL.
    /// El proveedor elige el provider vía appsettings.json.
    /// </summary>
    public class SupplierCatalogDbContext : DbContext
    {
        public DbSet<CatalogProduct> Products => Set<CatalogProduct>();
        public DbSet<CatalogSyncAuditEntity> SyncAudits => Set<CatalogSyncAuditEntity>();

        public SupplierCatalogDbContext(DbContextOptions<SupplierCatalogDbContext> options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(SupplierCatalogDbContext).Assembly);
        }
    }
}
