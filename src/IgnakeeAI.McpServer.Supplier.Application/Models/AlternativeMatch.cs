using IgnakeeAI.McpServer.Supplier.Domain.Entities;

namespace IgnakeeAI.McpServer.Supplier.Application.Models
{
    /// <summary>
    /// Alternativa encontrada con su razón de sustitución.
    /// El campo Reason es fundamental: el agente lo usa para decidir
    /// si la sustitución es conveniente para la partida.
    /// </summary>
    public record AlternativeMatch(CatalogProduct Product, string Reason);
}
