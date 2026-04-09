using IgnakeeAI.McpServer.Labor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IgnakeeAI.McpServer.Labor.Infrastructure.Persistence.Configurations
{
    /// <summary>Configuración de la entidad Worker para EF Core.</summary>
    public class WorkerConfiguration : IEntityTypeConfiguration<Worker>
    {
        public void Configure(EntityTypeBuilder<Worker> builder)
        {
            builder.HasKey(w => w.Id);

            builder.HasIndex(w => w.WorkerId).IsUnique();
            builder.HasIndex(w => w.Specialty);

            builder.Property(w => w.WorkerId).IsRequired().HasMaxLength(50);
            builder.Property(w => w.FullName).IsRequired().HasMaxLength(200);
            builder.Property(w => w.Specialty).IsRequired().HasMaxLength(100);
            builder.Property(w => w.Keywords).HasMaxLength(500);
            builder.Property(w => w.HourlyRate).HasColumnType("decimal(18,2)");
            builder.Property(w => w.DailyRate).HasColumnType("decimal(18,2)");
            builder.Property(w => w.Currency).HasMaxLength(3);
            builder.Property(w => w.LocationAddress).HasMaxLength(300);
            builder.Property(w => w.WorkZone).HasMaxLength(100);
            builder.Property(w => w.ProfileUrl).HasMaxLength(500);
            builder.Property(w => w.ContactPhone).HasMaxLength(50);
            builder.Property(w => w.ContactEmail).HasMaxLength(200);
            builder.Property(w => w.AvailabilitySchedule).HasMaxLength(100);
        }
    }
}
