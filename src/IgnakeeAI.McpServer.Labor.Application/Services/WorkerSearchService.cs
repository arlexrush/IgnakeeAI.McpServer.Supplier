using IgnakeeAI.McpServer.Labor.Application.Interfaces;
using IgnakeeAI.McpServer.Labor.Application.Models;
using IgnakeeAI.McpServer.Labor.Domain.Entities;
using IgnakeeAI.McpServer.Labor.Domain.Enums;
using IgnakeeAI.McpServer.Labor.Domain.Utils;

namespace IgnakeeAI.McpServer.Labor.Application.Services
{
    /// <summary>
    /// Servicio de aplicación que orquesta las búsquedas de trabajadores.
    /// Encapsula la lógica de búsqueda por especialidad, ubicación y tarifa.
    /// Las tools MCP delegan aquí toda la lógica; no acceden directamente al repositorio.
    /// </summary>
    public class WorkerSearchService
    {
        private readonly IWorkerRepository _workers;
        private readonly ILaborConfig _laborConfig;

        public WorkerSearchService(IWorkerRepository workers, ILaborConfig laborConfig)
        {
            _workers = workers;
            _laborConfig = laborConfig;
        }

        /// <summary>Busca un trabajador por código o especialidad y devuelve su tarifa.</summary>
        public async Task<WorkerRateResult> GetWorkerRateAsync(
            string specialtyDescription, string? workerId, CancellationToken ct)
        {
            Worker? worker = null;

            // 1. Búsqueda exacta por código
            if (!string.IsNullOrWhiteSpace(workerId))
            {
                worker = await _workers.FindByWorkerIdAsync(workerId, ct);
            }

            // 2. Búsqueda por descripción de especialidad
            if (worker is null && !string.IsNullOrWhiteSpace(specialtyDescription))
            {
                var terms = ExtractSearchTerms(specialtyDescription);
                var matches = await _workers.FindBySpecialtyAsync(terms, ct);
                worker = matches.FirstOrDefault();
            }

            if (worker is null)
            {
                return new WorkerRateResult(Found: false);
            }

            return new WorkerRateResult(
                Found: true,
                WorkerId: worker.WorkerId,
                FullName: worker.FullName,
                Specialty: worker.Specialty,
                HourlyRate: worker.HourlyRate,
                DailyRate: worker.DailyRate,
                Currency: worker.Currency,
                ExperienceYears: worker.ExperienceYears,
                QualityRating: worker.QualityRating,
                ProfileUrl: worker.ProfileUrl,
                LocationAddress: worker.LocationAddress,
                WorkZone: worker.WorkZone,
                ContactPhone: worker.ContactPhone,
                ContactEmail: worker.ContactEmail,
                AvailabilitySchedule: worker.AvailabilitySchedule,
                UpdatedAt: worker.UpdatedAt,
                AgencyContactEmail: _laborConfig.ContactEmail,
                AgencyContactPhone: _laborConfig.ContactPhone,
                AgencyContactAddress: _laborConfig.ContactAddress);
        }

        /// <summary>Busca trabajadores disponibles por especialidad y/o proximidad.</summary>
        public async Task<IReadOnlyList<WorkerMatch>> SearchWorkersAsync(
            string specialtyDescription, string? specialty,
            double? latitude, double? longitude, double? radiusKm,
            string criteria, int maxResults, CancellationToken ct)
        {
            // Inferir especialidad si no se proporcionó
            if (string.IsNullOrWhiteSpace(specialty))
            {
                var terms = ExtractSearchTerms(specialtyDescription);
                specialty = await _workers.InferSpecialtyAsync(terms, ct);
            }

            if (string.IsNullOrWhiteSpace(specialty) && (latitude is null || longitude is null))
            {
                return Array.Empty<WorkerMatch>();
            }

            return criteria.ToLowerInvariant() switch
            {
                "cheaper" or "economico" => await FindCheaperAsync(specialty, specialtyDescription, maxResults, ct),
                "toprated" or "mejor" => await FindTopRatedAsync(specialty, maxResults, ct),
                "nearby" or "cercano" when latitude.HasValue && longitude.HasValue
                    => await FindNearbyAsync(latitude.Value, longitude.Value, radiusKm ?? 25.0, maxResults, ct),
                _ => await FindAllAsync(specialty, specialtyDescription, latitude, longitude, radiusKm, maxResults, ct)
            };
        }

        /// <summary>Comprueba la disponibilidad de un trabajador por su código.</summary>
        public async Task<WorkerAvailabilityResult> CheckWorkerAvailabilityAsync(
            string workerId, CancellationToken ct)
        {
            var worker = await _workers.FindByWorkerIdAsync(workerId, ct);
            if (worker is null)
            {
                return new WorkerAvailabilityResult(Found: false);
            }

            return new WorkerAvailabilityResult(
                Found: true,
                WorkerId: worker.WorkerId,
                FullName: worker.FullName,
                Status: worker.Status,
                AvailabilitySchedule: worker.AvailabilitySchedule,
                WorkZone: worker.WorkZone);
        }

        // ── Estrategias de búsqueda ─────────────────────────────────

        private async Task<IReadOnlyList<WorkerMatch>> FindCheaperAsync(
            string? specialty, string description, int max, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(specialty))
            {
                return Array.Empty<WorkerMatch>();
            }

            var terms = ExtractSearchTerms(description);
            var refWorkers = await _workers.FindBySpecialtyAsync(terms, ct);
            var refRate = refWorkers.FirstOrDefault()?.HourlyRate ?? decimal.MaxValue;

            var workers = await _workers.FindCheaperInSpecialtyAsync(specialty, refRate, max, ct);

            return workers.Select(w => new WorkerMatch(w,
                $"Más económico: {w.HourlyRate} {w.Currency}/h " +
                $"(ahorro {(refRate > 0 ? Math.Round((1 - w.HourlyRate / refRate) * 100, 1) : 0)}%)"))
                .ToList();
        }

        private async Task<IReadOnlyList<WorkerMatch>> FindTopRatedAsync(
            string? specialty, int max, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(specialty))
            {
                return Array.Empty<WorkerMatch>();
            }

            var workers = await _workers.FindTopRatedAsync(specialty, 4, max, ct);

            return workers.Select(w => new WorkerMatch(w,
                $"Alta valoración: {w.QualityRating}/5. Experiencia: {w.ExperienceYears ?? 0} años."))
                .ToList();
        }

        private async Task<IReadOnlyList<WorkerMatch>> FindNearbyAsync(
            double lat, double lng, double radiusKm, int max, CancellationToken ct)
        {
            var workers = await _workers.FindByProximityAsync(lat, lng, radiusKm, max, ct);

            return workers.Select(w =>
            {
                var dist = w.Latitude.HasValue && w.Longitude.HasValue
                    ? Math.Round(GeoUtils.CalculateDistanceKm(lat, lng, w.Latitude.Value, w.Longitude.Value), 1)
                    : (double?)null;
                return new WorkerMatch(w,
                    $"Cercano: {(dist.HasValue ? $"{dist} km" : "ubicación desconocida")} desde tu posición. " +
                    $"Zona: {w.WorkZone ?? w.LocationAddress ?? "N/A"}");
            }).ToList();
        }

        private async Task<IReadOnlyList<WorkerMatch>> FindAllAsync(
            string? specialty, string description,
            double? lat, double? lng, double? radiusKm, int max, CancellationToken ct)
        {
            var all = new List<WorkerMatch>();

            if (!string.IsNullOrWhiteSpace(specialty))
            {
                all.AddRange(await FindCheaperAsync(specialty, description, 2, ct));
                all.AddRange(await FindTopRatedAsync(specialty, 1, ct));
            }

            if (lat.HasValue && lng.HasValue)
            {
                all.AddRange(await FindNearbyAsync(lat.Value, lng.Value, radiusKm ?? 25.0, 2, ct));
            }

            return all
                .GroupBy(w => w.Worker.WorkerId)
                .Select(g => g.First())
                .Take(max)
                .ToList();
        }

        // ── Helpers ──────────────────────────────────────────────────

        private static readonly HashSet<string> _shortTermWhitelist = new(StringComparer.OrdinalIgnoreCase)
        { "ac", "oc", "id" };

        private static IReadOnlyList<string> ExtractSearchTerms(string text) =>
            text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => t.Length > 2 || _shortTermWhitelist.Contains(t))
                .Take(7)
                .ToList();
    }
}
