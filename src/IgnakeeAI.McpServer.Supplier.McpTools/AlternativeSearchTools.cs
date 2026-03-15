using IgnakeeAI.McpServer.Supplier.Application.Services;
using IgnakeeAI.McpServer.Supplier.Domain.Enums;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace IgnakeeAI.McpServer.Supplier.McpTools
{
    /// <summary>
    /// Tool MCP CLAVE: searchAlternatives.
    /// Permite al agente descubrir sustitutos optimizando la partida.
    /// </summary>
    [McpServerToolType]
    public class AlternativeSearchTools
    {
        private readonly CatalogSearchService _search;
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public AlternativeSearchTools(CatalogSearchService search) => _search = search;

        [McpServerTool, Description(
            "Searches for alternative or substitute products that can replace the requested item. " +
            "Criteria: 'cheaper' | 'better' | 'onSale' | 'optimalPack' | 'any'. " +
            "Returns alternatives with reason for substitution.")]
        public async Task<string> SearchAlternatives(
            [Description("Description of the original item to find alternatives for")] string itemDescription,
            [Description("Category (e.g. 'cementos', 'aceros')")] string? category = null,
            [Description("Criteria: 'cheaper', 'better', 'onSale', 'optimalPack', 'any'")] string criteria = "any",
            [Description("Required quantity to optimize pack sizes")] decimal? requiredQuantity = null,
            [Description("Maximum alternatives to return")] int maxResults = 5,
            [Description("ISO 4217 currency code")] string currency = "EUR")
        {
            var parsedCriteria = Enum.TryParse<SubstitutionCriteria>(criteria, true, out var c)
                ? c : SubstitutionCriteria.Any;

            var matches = await _search.SearchAlternativesAsync(
                itemDescription, category, parsedCriteria, requiredQuantity, maxResults, CancellationToken.None);

            var results = matches.Select(m => new
            {
                m.Product.ItemCode,
                m.Product.Description,
                unitPrice = m.Product.EffectivePrice,
                originalPrice = m.Product.IsOnSale ? m.Product.UnitPrice : (decimal?)null,
                m.Product.Currency,
                m.Product.Unit,
                m.Product.PackSize,
                m.Product.PackPrice,
                m.Product.Specification,
                m.Product.Presentation,
                m.Product.QualityRating,
                m.Product.IsOnSale,
                m.Product.AvailableStock,
                m.Product.LeadTimeDays,
                url = m.Product.ProductUrl,
                reason = m.Reason
            });

            return JsonSerializer.Serialize(new { found = matches.Any(), count = matches.Count, alternatives = results }, JsonOpts);
        }
    }
}
