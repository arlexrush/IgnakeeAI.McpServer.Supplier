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
    public class WorkerAvailabilityToolsTests : IDisposable
    {
        private readonly LaborDbContext _db;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public WorkerAvailabilityToolsTests()
        {
            var options = new DbContextOptionsBuilder<LaborDbContext>()
                .UseInMemoryDatabase(databaseName: $"AvailabilityTest_{Guid.NewGuid()}")
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
        public async Task CheckWorkerAvailability_AvailableWorker_ReturnsFoundTrue()
        {
            // Arrange
            var tools = CreateTools();

            // Act
            var json = await tools.CheckWorkerAvailability("TRB-001");
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Assert
            Assert.True(root.GetProperty("found").GetBoolean());
            Assert.Equal("Available", root.GetProperty("status").GetString());
        }

        [Fact]
        public async Task CheckWorkerAvailability_BusyWorker_ReturnsBusy()
        {
            // Arrange
            var tools = CreateTools();

            // Act
            var json = await tools.CheckWorkerAvailability("TRB-002");
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Assert
            Assert.True(root.GetProperty("found").GetBoolean());
            Assert.Equal("Busy", root.GetProperty("status").GetString());
        }

        [Fact]
        public async Task CheckWorkerAvailability_UnknownWorker_ReturnsFoundFalse()
        {
            // Arrange
            var tools = CreateTools();

            // Act
            var json = await tools.CheckWorkerAvailability("TRB-UNKNOWN");
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Assert
            Assert.False(root.GetProperty("found").GetBoolean());
        }

        [Fact]
        public void GetContactInfo_ReturnsAgencyInfo()
        {
            // Arrange
            var tools = CreateTools();

            // Act
            var json = tools.GetContactInfo();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Assert
            Assert.Equal("Agencia Test", root.GetProperty("agencyName").GetString());
            Assert.Equal("contacto@agencia-test.local", root.GetProperty("contactEmail").GetString());
            Assert.Equal("L-V 08:00-18:00", root.GetProperty("businessHours").GetString());
        }

        private WorkerAvailabilityTools CreateTools()
        {
            var repository = new EfWorkerRepository(_db);
            var service = new WorkerSearchService(repository, new TestLaborConfig());
            return new WorkerAvailabilityTools(service, new TestLaborConfig());
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
                    Keywords = "albañil,construcción",
                    HourlyRate = 18.00m,
                    Currency = "EUR",
                    Status = WorkerStatus.Available,
                    AvailabilitySchedule = "L-V 07:00-15:00",
                    WorkZone = "Madrid",
                    UpdatedAt = now,
                    IsActive = true
                },
                new Worker
                {
                    WorkerId = "TRB-002",
                    FullName = "Carlos Martínez Ruiz",
                    Specialty = "electricista",
                    Keywords = "electricista,instalación",
                    HourlyRate = 22.00m,
                    Currency = "EUR",
                    Status = WorkerStatus.Busy,
                    AvailabilitySchedule = "L-V 08:00-16:00",
                    WorkZone = "Madrid Norte",
                    UpdatedAt = now,
                    IsActive = true
                });

            _db.SaveChanges();
        }
    }
}
