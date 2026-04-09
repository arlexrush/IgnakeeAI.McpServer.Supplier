using IgnakeeAI.McpServer.Labor.Domain.Entities;
using IgnakeeAI.McpServer.Labor.Domain.Enums;
using IgnakeeAI.McpServer.Labor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IgnakeeAI.McpServer.Labor.Tests.Integration
{
    /// <summary>
    /// Factory compartida para las pruebas de integración del Labor MCP Server.
    /// Sustituye la BD real por SQLite en memoria y siembra datos de prueba.
    /// </summary>
    public class LaborApiFactory : WebApplicationFactory<Api.Program>
    {
        private readonly SqliteConnection _keepAliveConnection;

        public LaborApiFactory()
        {
            _keepAliveConnection = new SqliteConnection("Data Source=:memory:");
            _keepAliveConnection.Open();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ApplyMigrationsOnStartup"] = "false",
                    ["DatabaseProvider"] = "sqlite",
                    ["LABOR_AGENCY_NAME"] = "Agencia Test",
                    ["LABOR_CONTACT_EMAIL"] = "test@agencia.local",
                    ["LABOR_CONTACT_PHONE"] = "+34 900 000 000",
                    ["LABOR_CONTACT_ADDRESS"] = "Calle Test 1, Madrid",
                    ["LABOR_BUSINESS_HOURS"] = "L-V 08:00-18:00"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<LaborDbContext>>();
                services.RemoveAll<LaborDbContext>();

                services.AddDbContext<LaborDbContext>(options =>
                    options.UseSqlite(_keepAliveConnection));
            });
        }

        public async Task<IServiceScope> SeedDatabaseAsync(
            IEnumerable<Worker>? workers = null)
        {
            var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LaborDbContext>();

            await db.Database.EnsureCreatedAsync();

            // Only seed if the database is empty to prevent duplicate key violations
            // when InitializeAsync is called before each test in a shared fixture.
            if (await db.Workers.AnyAsync())
            {
                return scope;
            }

            if (workers is not null)
            {
                db.Workers.AddRange(workers);
            }
            else
            {
                var now = DateTime.UtcNow;
                db.Workers.AddRange(
                    new Worker
                    {
                        WorkerId = "TRB-001",
                        FullName = "Juan García López",
                        Specialty = "albañil",
                        Keywords = "albañil,construcción,mampostería,hormigón",
                        HourlyRate = 18.00m,
                        DailyRate = 144.00m,
                        Currency = "EUR",
                        Latitude = 40.4168,
                        Longitude = -3.7038,
                        LocationAddress = "Madrid Centro",
                        WorkZone = "Madrid",
                        ProfileUrl = "https://perfil.ejemplo.com/trabajador-001",
                        ContactPhone = "+34 600 000 001",
                        Status = WorkerStatus.Available,
                        AvailabilitySchedule = "L-V 07:00-15:00",
                        ExperienceYears = 10,
                        QualityRating = 5,
                        UpdatedAt = now,
                        IsActive = true
                    },
                    new Worker
                    {
                        WorkerId = "TRB-002",
                        FullName = "Carlos Martínez Ruiz",
                        Specialty = "electricista",
                        Keywords = "electricista,instalación,eléctrica,cuadros,solar",
                        HourlyRate = 22.00m,
                        DailyRate = 176.00m,
                        Currency = "EUR",
                        Latitude = 40.4200,
                        Longitude = -3.7050,
                        LocationAddress = "Madrid Norte",
                        WorkZone = "Madrid Norte",
                        ProfileUrl = "https://perfil.ejemplo.com/trabajador-002",
                        ContactPhone = "+34 600 000 002",
                        Status = WorkerStatus.Available,
                        AvailabilitySchedule = "L-V 08:00-16:00",
                        ExperienceYears = 8,
                        QualityRating = 4,
                        UpdatedAt = now,
                        IsActive = true
                    },
                    new Worker
                    {
                        WorkerId = "TRB-003",
                        FullName = "Pedro Sánchez Torres",
                        Specialty = "fontanero",
                        Keywords = "fontanero,plomero,tuberías,agua,saneamiento",
                        HourlyRate = 20.00m,
                        DailyRate = 160.00m,
                        Currency = "EUR",
                        Latitude = 40.3900,
                        Longitude = -3.7200,
                        LocationAddress = "Madrid Sur",
                        WorkZone = "Madrid Sur",
                        ProfileUrl = "https://perfil.ejemplo.com/trabajador-003",
                        ContactPhone = "+34 600 000 003",
                        Status = WorkerStatus.Busy,
                        AvailabilitySchedule = "L-V 08:00-17:00",
                        ExperienceYears = 6,
                        QualityRating = 4,
                        UpdatedAt = now,
                        IsActive = true
                    });
            }

            await db.SaveChangesAsync();
            return scope;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _keepAliveConnection.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
