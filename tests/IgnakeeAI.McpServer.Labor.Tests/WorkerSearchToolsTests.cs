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
    public class WorkerSearchToolsTests : IDisposable
    {
        private readonly LaborDbContext _db;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public WorkerSearchToolsTests()
        {
            var options = new DbContextOptionsBuilder<LaborDbContext>()
                .UseInMemoryDatabase(databaseName: $"WorkerSearchTest_{Guid.NewGuid()}")
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
        public async Task SearchWorkers_ByProximity_ReturnsNearbyWorkers()
        {
            // Arrange
            var tools = CreateTools();

            // Act — búsqueda cerca de Madrid Centro (40.4168, -3.7038), radio 10 km
            var json = await tools.SearchWorkers(
                specialtyDescription: "albañil",
                latitude: 40.4168,
                longitude: -3.7038,
                radiusKm: 10.0,
                criteria: "nearby");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Assert
            Assert.True(root.GetProperty("found").GetBoolean());
            Assert.True(root.GetProperty("count").GetInt32() > 0);
        }

        [Fact]
        public async Task SearchWorkers_BySpecialty_ReturnsMatchingWorkers()
        {
            // Arrange
            var tools = CreateTools();

            // Act
            var json = await tools.SearchWorkers(
                specialtyDescription: "electricista",
                criteria: "any");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Assert
            Assert.True(root.GetProperty("found").GetBoolean());
            var workers = root.GetProperty("workers");
            Assert.True(workers.GetArrayLength() > 0);
            // Verificar que el resultado tiene profileUrl
            var first = workers[0];
            Assert.True(first.TryGetProperty("profileUrl", out _));
        }

        [Fact]
        public async Task SearchWorkers_WithNoMatches_ReturnsFoundFalse()
        {
            // Arrange
            var tools = CreateTools();

            // Act
            var json = await tools.SearchWorkers(
                specialtyDescription: "especialidad_inexistente_xyz",
                criteria: "any");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Assert
            Assert.False(root.GetProperty("found").GetBoolean());
            Assert.Equal(0, root.GetProperty("count").GetInt32());
        }

        private WorkerSearchTools CreateTools()
        {
            var repository = new EfWorkerRepository(_db);
            var service = new WorkerSearchService(repository, new TestLaborConfig());
            return new WorkerSearchTools(service);
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
                    Keywords = "albañil,construcción,mampostería",
                    HourlyRate = 18.00m,
                    Currency = "EUR",
                    Latitude = 40.4168,
                    Longitude = -3.7038,
                    LocationAddress = "Madrid Centro",
                    WorkZone = "Madrid",
                    ProfileUrl = "https://perfil.ejemplo.com/trabajador-001",
                    Status = WorkerStatus.Available,
                    QualityRating = 5,
                    ExperienceYears = 10,
                    UpdatedAt = now,
                    IsActive = true
                },
                new Worker
                {
                    WorkerId = "TRB-002",
                    FullName = "Carlos Martínez Ruiz",
                    Specialty = "electricista",
                    Keywords = "electricista,instalación,eléctrica",
                    HourlyRate = 22.00m,
                    Currency = "EUR",
                    Latitude = 40.4200,
                    Longitude = -3.7050,
                    LocationAddress = "Madrid Norte",
                    WorkZone = "Madrid Norte",
                    ProfileUrl = "https://perfil.ejemplo.com/trabajador-002",
                    Status = WorkerStatus.Available,
                    QualityRating = 4,
                    ExperienceYears = 8,
                    UpdatedAt = now,
                    IsActive = true
                });

            _db.SaveChanges();
        }
    }
}
