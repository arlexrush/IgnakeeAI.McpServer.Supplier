using IgnakeeAI.McpServer.Labor.Application.Interfaces;
using IgnakeeAI.McpServer.Labor.Application.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace IgnakeeAI.McpServer.Labor.McpTools
{
    /// <summary>
    /// Tools MCP: checkWorkerAvailability y getContactInfo.
    /// </summary>
    [McpServerToolType]
    public class WorkerAvailabilityTools
    {
        private readonly WorkerSearchService _search;
        private readonly ILaborConfig _laborConfig;
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        public WorkerAvailabilityTools(WorkerSearchService search, ILaborConfig laborConfig)
        {
            _search = search;
            _laborConfig = laborConfig;
        }

        [McpServerTool, Description("Checks the availability status of a specific worker by their ID.")]
        public async Task<string> CheckWorkerAvailability(
            [Description("Worker ID to check availability for")] string workerId,
            CancellationToken cancellationToken = default)
        {
            var result = await _search.CheckWorkerAvailabilityAsync(workerId, cancellationToken);
            return JsonSerializer.Serialize(result, JsonOpts);
        }

        [McpServerTool, Description("Returns business hours and contact information of this labor agency.")]
        public string GetContactInfo()
        {
            return JsonSerializer.Serialize(new
            {
                agencyName = _laborConfig.AgencyName,
                businessHours = _laborConfig.BusinessHours,
                contactEmail = _laborConfig.ContactEmail,
                contactPhone = _laborConfig.ContactPhone,
                contactAddress = _laborConfig.ContactAddress
            }, JsonOpts);
        }
    }
}
