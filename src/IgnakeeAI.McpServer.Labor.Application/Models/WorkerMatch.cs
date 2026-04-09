using IgnakeeAI.McpServer.Labor.Domain.Entities;

namespace IgnakeeAI.McpServer.Labor.Application.Models
{
    /// <summary>Par (trabajador, motivo de coincidencia) para resultados de búsqueda.</summary>
    public record WorkerMatch(Worker Worker, string Reason);
}
