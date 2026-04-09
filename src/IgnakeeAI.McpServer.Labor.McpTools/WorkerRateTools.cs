using IgnakeeAI.McpServer.Labor.Application.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace IgnakeeAI.McpServer.Labor.McpTools
{
    /// <summary>
    /// Tool MCP OBLIGATORIA: getWorkerRate.
    /// El agente llama a esta tool para consultar la tarifa de un trabajador o especialidad.
    /// Delega toda la lógica a WorkerSearchService (Application layer).
    /// </summary>
    [McpServerToolType]
    public class WorkerRateTools
    {
        private readonly WorkerSearchService _search;
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public WorkerRateTools(WorkerSearchService search) => _search = search;

        [McpServerTool, Description(
            "Gets the hourly and daily rate for a construction worker or labor specialty. " +
            "Searches by worker ID (exact) or specialty description (fuzzy). " +
            "Returns hourlyRate, dailyRate, currency, experienceYears, profileUrl, locationAddress, and contact info.")]
        public async Task<string> GetWorkerRate(
            [Description("Specialty or description of the labor needed (e.g. 'albañil', 'electricista instalación solar')")] string specialtyDescription,
            [Description("Worker ID for exact lookup (optional)")] string? workerId = null,
            CancellationToken cancellationToken = default)
        {
            var result = await _search.GetWorkerRateAsync(specialtyDescription, workerId, cancellationToken);
            return JsonSerializer.Serialize(result, JsonOpts);
        }
    }
}
