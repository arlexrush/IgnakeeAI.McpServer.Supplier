namespace IgnakeeAI.McpServer.Labor.Domain.Enums
{
    /// <summary>Estado de disponibilidad del trabajador.</summary>
    public enum WorkerStatus
    {
        /// <summary>Disponible para ser contratado.</summary>
        Available,
        /// <summary>Actualmente trabajando en un proyecto.</summary>
        Busy,
        /// <summary>No disponible temporalmente (vacaciones, baja, etc.).</summary>
        Unavailable
    }
}
