using IgnakeeAI.McpServer.Labor.Domain.Enums;

namespace IgnakeeAI.McpServer.Labor.Application.Models
{
    /// <summary>Resultado de comprobación de disponibilidad de un trabajador.</summary>
    public record WorkerAvailabilityResult(
        bool Found,
        string? WorkerId = null,
        string? FullName = null,
        WorkerStatus? Status = null,
        string? AvailabilitySchedule = null,
        string? WorkZone = null
    );
}
