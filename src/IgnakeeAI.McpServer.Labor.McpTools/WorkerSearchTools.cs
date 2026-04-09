using IgnakeeAI.McpServer.Labor.Application.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace IgnakeeAI.McpServer.Labor.McpTools
{
    /// <summary>
    /// Tool MCP CLAVE: searchWorkers.
    /// Permite al agente descubrir trabajadores disponibles filtrando por especialidad,
    /// ubicación GPS y criterio de selección.
    /// </summary>
    [McpServerToolType]
    public class WorkerSearchTools
    {
        private readonly WorkerSearchService _search;
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public WorkerSearchTools(WorkerSearchService search) => _search = search;

        [McpServerTool, Description(
            "Searches for available workers by specialty and/or proximity to a location. " +
            "Criteria: 'cheaper' | 'topRated' | 'nearby' | 'any'. " +
            "Provide latitude/longitude to enable proximity search. " +
            "Returns a list of matching workers with their profileUrl and current location.")]
        public async Task<string> SearchWorkers(
            [Description("Specialty or description (e.g. 'pintor fachadas', 'fontanero')")] string specialtyDescription,
            [Description("Specialty keyword to filter (e.g. 'albañil', 'electricista')")] string? specialty = null,
            [Description("Latitude of the work site for proximity search")] double? latitude = null,
            [Description("Longitude of the work site for proximity search")] double? longitude = null,
            [Description("Search radius in kilometers (default: 25)")] double? radiusKm = null,
            [Description("Selection criteria: 'cheaper', 'topRated', 'nearby', 'any'")] string criteria = "any",
            [Description("Maximum workers to return")] int maxResults = 5,
            CancellationToken cancellationToken = default)
        {
            var matches = await _search.SearchWorkersAsync(
                specialtyDescription, specialty,
                latitude, longitude, radiusKm,
                criteria, maxResults, cancellationToken);

            var results = matches.Select(m => new
            {
                m.Worker.WorkerId,
                m.Worker.FullName,
                m.Worker.Specialty,
                m.Worker.HourlyRate,
                m.Worker.DailyRate,
                m.Worker.Currency,
                m.Worker.ExperienceYears,
                m.Worker.QualityRating,
                m.Worker.Status,
                m.Worker.ProfileUrl,
                m.Worker.LocationAddress,
                m.Worker.WorkZone,
                m.Worker.Latitude,
                m.Worker.Longitude,
                m.Worker.ContactPhone,
                m.Worker.ContactEmail,
                m.Worker.AvailabilitySchedule,
                reason = m.Reason
            });

            return JsonSerializer.Serialize(
                new { found = matches.Any(), count = matches.Count, workers = results }, JsonOpts);
        }
    }
}
