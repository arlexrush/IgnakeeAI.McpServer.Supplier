using IgnakeeAI.McpServer.Labor.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IgnakeeAI.McpServer.Labor.Infrastructure.Persistence
{
    /// <summary>
    /// DbContext de la plataforma de mano de obra.
    /// Soporta SQLite (por defecto), PostgreSQL, SQL Server y MySQL.
    /// </summary>
    public class LaborDbContext : DbContext
    {
        public DbSet<Worker> Workers => Set<Worker>();

        public LaborDbContext(DbContextOptions<LaborDbContext> options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(LaborDbContext).Assembly);
        }
    }
}
