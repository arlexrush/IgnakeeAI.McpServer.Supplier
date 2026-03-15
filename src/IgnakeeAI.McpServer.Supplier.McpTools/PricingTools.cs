using IgnakeeAI.McpServer.Supplier.Application.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace IgnakeeAI.McpServer.Supplier.McpTools
{
    /// <summary>
    /// Tool MCP OBLIGATORIA: getPrice.
    /// El agente Aristóteles llama a esta tool como primera acción en cada servidor MCP.
    /// Delega toda la lógica a CatalogSearchService (Application layer).
    /// </summary>
    [McpServerToolType]
    public class PricingTools
    {
        private readonly CatalogSearchService _search;
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public PricingTools(CatalogSearchService search) => _search = search;

        [McpServerTool, Description(
            "Gets the price for a construction material or resource. " +
            "Searches by item code (exact) or description (fuzzy). " +
            "Returns unitPrice, currency, unit, packSize, packPrice, validUntil, and contact info.")]
        public async Task<string> GetPrice(
            [Description("Full description of the item to quote")] string itemDescription,
            [Description("Item code or SKU for exact lookup (optional)")] string? itemCode = null,
            [Description("ISO 4217 currency code (e.g. EUR, USD)")] string currency = "EUR")
        {
            var result = await _search.GetPriceAsync(itemDescription, itemCode, currency, CancellationToken.None);
            return JsonSerializer.Serialize(result, JsonOpts);
        }
    }
}
