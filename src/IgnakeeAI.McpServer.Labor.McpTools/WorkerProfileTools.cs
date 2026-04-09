using IgnakeeAI.McpServer.Labor.Application.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace IgnakeeAI.McpServer.Labor.McpTools
{
    /// <summary>
    /// Tool MCP: getWorkerProfile.
    /// Permite al agente obtener el perfil completo de un trabajador, incluyendo
    /// su página web personal y su ubicación actual.
    /// </summary>
    [McpServerToolType]
    public class WorkerProfileTools
    {
        private readonly WorkerSearchService _search;
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public WorkerProfileTools(WorkerSearchService search) => _search = search;

        [McpServerTool, Description(
            "Gets the full profile of a worker including their personal webpage URL and current GPS location. " +
            "Useful to verify credentials, portfolio, certifications and current position of the worker.")]
        public async Task<string> GetWorkerProfile(
            [Description("Worker ID to retrieve the profile for")] string workerId,
            CancellationToken cancellationToken = default)
        {
            var result = await _search.GetWorkerRateAsync(string.Empty, workerId, cancellationToken);

            if (!result.Found)
            {
                return JsonSerializer.Serialize(new { found = false }, JsonOpts);
            }

            return JsonSerializer.Serialize(new
            {
                found = true,
                result.WorkerId,
                result.FullName,
                result.Specialty,
                result.ExperienceYears,
                result.QualityRating,
                result.ProfileUrl,
                result.LocationAddress,
                result.WorkZone,
                result.HourlyRate,
                result.DailyRate,
                result.Currency,
                result.AvailabilitySchedule,
                result.ContactPhone,
                result.ContactEmail,
                result.UpdatedAt
            }, JsonOpts);
        }
    }
}
