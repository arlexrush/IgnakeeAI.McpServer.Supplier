using IgnakeeAI.McpServer.Labor.Application.Interfaces;
using IgnakeeAI.McpServer.Labor.Domain.Entities;
using IgnakeeAI.McpServer.Labor.Domain.Enums;
using IgnakeeAI.McpServer.Labor.Domain.Utils;
using Microsoft.EntityFrameworkCore;

namespace IgnakeeAI.McpServer.Labor.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Implementación de IWorkerRepository con EF Core.
    /// Funciona con cualquier provider configurado (SQLite, PostgreSQL, SQL Server, MySQL).
    /// </summary>
    public class EfWorkerRepository : IWorkerRepository
    {
        private readonly LaborDbContext _db;

        public EfWorkerRepository(LaborDbContext db) => _db = db;

        public async Task<Worker?> FindByWorkerIdAsync(string workerId, CancellationToken ct) =>
            await _db.Workers
                .Where(w => w.IsActive && w.WorkerId == workerId)
                .FirstOrDefaultAsync(ct);

        public async Task<IReadOnlyList<Worker>> FindBySpecialtyAsync(
            IReadOnlyList<string> searchTerms, CancellationToken ct)
        {
            var allWorkers = await _db.Workers
                .Where(w => w.IsActive)
                .ToListAsync(ct);

            return allWorkers
                .Select(w => new
                {
                    Worker = w,
                    Score = searchTerms.Count(t =>
                        w.Specialty.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                        (w.Keywords?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        w.FullName.Contains(t, StringComparison.OrdinalIgnoreCase))
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Worker.HourlyRate)
                .Select(x => x.Worker)
                .ToList();
        }

        public async Task<IReadOnlyList<Worker>> FindAvailableBySpecialtyAsync(
            string specialty, int max, CancellationToken ct) =>
            await _db.Workers
                .Where(w => w.IsActive
                    && w.Specialty == specialty
                    && w.Status == WorkerStatus.Available)
                .OrderBy(w => w.HourlyRate)
                .Take(max)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<Worker>> FindByProximityAsync(
            double latitude, double longitude, double radiusKm, int max, CancellationToken ct)
        {
            // Cargamos los trabajadores con coordenadas en memoria para calcular Haversine
            var candidates = await _db.Workers
                .Where(w => w.IsActive && w.Latitude.HasValue && w.Longitude.HasValue)
                .ToListAsync(ct);

            return candidates
                .Select(w => new
                {
                    Worker = w,
                    DistKm = GeoUtils.CalculateDistanceKm(latitude, longitude, w.Latitude!.Value, w.Longitude!.Value)
                })
                .Where(x => x.DistKm <= radiusKm)
                .OrderBy(x => x.DistKm)
                .Take(max)
                .Select(x => x.Worker)
                .ToList();
        }

        public async Task<IReadOnlyList<Worker>> FindCheaperInSpecialtyAsync(
            string specialty, decimal referenceRate, int max, CancellationToken ct)
        {
            var workers = await _db.Workers
                .Where(w => w.IsActive && w.Specialty == specialty && w.HourlyRate < referenceRate)
                .ToListAsync(ct);

            return workers
                .OrderBy(w => w.HourlyRate)
                .Take(max)
                .ToList();
        }

        public async Task<IReadOnlyList<Worker>> FindTopRatedAsync(
            string specialty, int minRating, int max, CancellationToken ct)
        {
            var workers = await _db.Workers
                .Where(w => w.IsActive && w.Specialty == specialty && w.QualityRating >= minRating)
                .ToListAsync(ct);

            return workers
                .OrderByDescending(w => w.QualityRating)
                .ThenByDescending(w => w.ExperienceYears)
                .Take(max)
                .ToList();
        }

        public async Task<string?> InferSpecialtyAsync(
            IReadOnlyList<string> searchTerms, CancellationToken ct)
        {
            var allWorkers = await _db.Workers.Where(w => w.IsActive).ToListAsync(ct);
            return allWorkers
                .Select(w => new
                {
                    w.Specialty,
                    Score = searchTerms.Count(t =>
                        w.Specialty.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                        (w.Keywords?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false))
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .FirstOrDefault()?.Specialty;
        }

    }
}

