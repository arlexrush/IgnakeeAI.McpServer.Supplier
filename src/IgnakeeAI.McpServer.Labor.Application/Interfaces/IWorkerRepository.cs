using IgnakeeAI.McpServer.Labor.Domain.Entities;
using IgnakeeAI.McpServer.Labor.Domain.Enums;

namespace IgnakeeAI.McpServer.Labor.Application.Interfaces
{
    /// <summary>
    /// Puerto de acceso al registro de trabajadores.
    /// Infrastructure implementa este contrato con EF Core.
    /// Las tools MCP consumen este servicio sin conocer la fuente de datos.
    /// </summary>
    public interface IWorkerRepository
    {
        /// <summary>Busca un trabajador por su código único.</summary>
        Task<Worker?> FindByWorkerIdAsync(string workerId, CancellationToken ct = default);

        /// <summary>Busca trabajadores por especialidad y términos de búsqueda.</summary>
        Task<IReadOnlyList<Worker>> FindBySpecialtyAsync(
            IReadOnlyList<string> searchTerms, CancellationToken ct = default);

        /// <summary>Busca trabajadores disponibles por especialidad.</summary>
        Task<IReadOnlyList<Worker>> FindAvailableBySpecialtyAsync(
            string specialty, int max, CancellationToken ct = default);

        /// <summary>Busca trabajadores dentro de un radio de distancia de una ubicación.</summary>
        Task<IReadOnlyList<Worker>> FindByProximityAsync(
            double latitude, double longitude, double radiusKm, int max, CancellationToken ct = default);

        /// <summary>Busca trabajadores con menor tarifa horaria en la especialidad.</summary>
        Task<IReadOnlyList<Worker>> FindCheaperInSpecialtyAsync(
            string specialty, decimal referenceRate, int max, CancellationToken ct = default);

        /// <summary>Busca trabajadores con mayor valoración de calidad en la especialidad.</summary>
        Task<IReadOnlyList<Worker>> FindTopRatedAsync(
            string specialty, int minRating, int max, CancellationToken ct = default);

        /// <summary>Infiere la especialidad a partir de términos de búsqueda.</summary>
        Task<string?> InferSpecialtyAsync(
            IReadOnlyList<string> searchTerms, CancellationToken ct = default);
    }
}
