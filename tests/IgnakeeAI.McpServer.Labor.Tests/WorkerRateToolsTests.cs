using IgnakeeAI.McpServer.Labor.Application.Services;
using IgnakeeAI.McpServer.Labor.Domain.Entities;
using IgnakeeAI.McpServer.Labor.Domain.Enums;
using IgnakeeAI.McpServer.Labor.Infrastructure.Persistence;
using IgnakeeAI.McpServer.Labor.Infrastructure.Persistence.Repositories;
using IgnakeeAI.McpServer.Labor.McpTools;
using IgnakeeAI.McpServer.Labor.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;

namespace IgnakeeAI.McpServer.Labor.Tests
{
    public class WorkerRateToolsTests : IDisposable
    {
        private readonly LaborDbContext _db;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public WorkerRateToolsTests()
        {
            var options = new DbContextOptionsBuilder<LaborDbContext>()
                .UseInMemoryDatabase(databaseName: $"WorkerRateToolsTest_{Guid.NewGuid()}")
                .Options;

            _db = new LaborDbContext(options);
            SeedWorkers();
        }

        public void Dispose()
        {
            _cts.Dispose();
            _db.Dispose();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task GetWorkerRate_WithWorkerId_ReturnsExpectedWorker()
        {
            // Arrange
            var tools = CreateTools();

            // Act
            var json = await tools.GetWorkerRate(specialtyDescription: "", workerId: "TRB-001");
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Assert
            Assert.True(root.GetProperty("found").GetBoolean());
            Assert.Equal("TRB-001", root.GetProperty("workerId").GetString());
            Assert.Equal(18.00m, root.GetProperty("hourlyRate").GetDecimal());
            Assert.Equal("EUR", root.GetProperty("currency").GetString());
            Assert.Equal("contacto@agencia-test.local", root.GetProperty("agencyContactEmail").GetString());
        }

        [Fact]
        public async Task GetWorkerRate_WithSpecialty_FindsByFuzzySearch()
        {
            // Arrange
            var tools = CreateTools();

            // Act
            var json = await tools.GetWorkerRate(specialtyDescription: "electricista instalación", workerId: null);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Assert
            Assert.True(root.GetProperty("found").GetBoolean());
            Assert.Equal("TRB-002", root.GetProperty("workerId").GetString());
        }

        [Fact]
        public async Task GetWorkerRate_WithProfileUrl_ReturnsProfileUrl()
        {
            // Arrange
            var tools = CreateTools();

            // Act
            var json = await tools.GetWorkerRate(specialtyDescription: "", workerId: "TRB-001");
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Assert
            Assert.True(root.GetProperty("found").GetBoolean());
            Assert.Equal("https://perfil.ejemplo.com/trabajador-001", root.GetProperty("profileUrl").GetString());
        }

        [Fact]
        public async Task GetWorkerRate_WhenNotFound_ReturnsFoundFalse()
        {
            // Arrange
            var tools = CreateTools();

            // Act
            var json = await tools.GetWorkerRate(specialtyDescription: "carpintero inexistente xyz", workerId: null);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Assert
            Assert.False(root.GetProperty("found").GetBoolean());
        }

        private WorkerRateTools CreateTools()
        {
            var repository = new EfWorkerRepository(_db);
            var service = new WorkerSearchService(repository, new TestLaborConfig());
            return new WorkerRateTools(service);
        }

        private void SeedWorkers()
        {
            var now = DateTime.UtcNow;

            _db.Workers.AddRange(
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
                    ContactEmail = "juan.garcia@ejemplo.com",
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
                    ContactEmail = "carlos.martinez@ejemplo.com",
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
                    ContactEmail = "pedro.sanchez@ejemplo.com",
                    Status = WorkerStatus.Busy,
                    AvailabilitySchedule = "L-V 08:00-17:00",
                    ExperienceYears = 6,
                    QualityRating = 4,
                    UpdatedAt = now,
                    IsActive = true
                });

            _db.SaveChanges();
        }
    }
}
