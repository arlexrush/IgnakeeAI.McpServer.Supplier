using IgnakeeAI.McpServer.Supplier.Application.Services;
using IgnakeeAI.McpServer.Supplier.Application.Contracts;
using IgnakeeAI.McpServer.Supplier.Domain.Enums;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using System.Threading;

namespace IgnakeeAI.McpServer.Supplier.McpTools
{
    /// <summary>
    /// Tool MCP pública: SearchAlternatives.
    /// Permite al agente descubrir sustitutos optimizando la partida.
    /// </summary>
    [McpServerToolType]
    public class AlternativeSearchTools
    {
        private readonly CatalogSearchService _search;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public AlternativeSearchTools(CatalogSearchService search) => _search = search;

        [McpServerTool(Name = SupplierMcpToolNames.SearchAlternatives), Description(
            "Searches for alternative or substitute products that can replace the requested item. " +
            "Criteria: 'cheaper' | 'better' | 'onSale' | 'optimalPack' | 'any'. " +
            "Returns alternatives with reason for substitution.")]
        public async Task<string> SearchAlternatives(
            [Description("Description of the original item to find alternatives for")] string itemDescription,
            [Description("Category (e.g. 'cementos', 'aceros')")] string? category = null,
            [Description("Criteria: 'cheaper', 'better', 'onSale', 'optimalPack', 'any'")] string criteria = "any",
            [Description("Required quantity to optimize pack sizes")] decimal? requiredQuantity = null,
            [Description("Maximum alternatives to return")] int maxResults = 5,
            [Description("ISO 4217 currency code")] string currency = "EUR",
            CancellationToken cancellationToken = default)
        {
            if (!Enum.TryParse<SubstitutionCriteria>(criteria, true, out var parsedCriteria))
                throw new ArgumentException($"criteria desconocido: {criteria}.", nameof(criteria));

            var matches = await _search.SearchAlternativesAsync(
                itemDescription, category, parsedCriteria, requiredQuantity, maxResults, currency, cancellationToken);

            var results = matches.Select(m => new AlternativeResultItem
            {
                ItemCode = m.Product.ItemCode,
                Description = m.Product.Description,
                UnitPrice = m.Product.EffectivePrice,
                OriginalPrice = m.Product.IsOnSale ? m.Product.UnitPrice : null,
                Currency = m.Product.Currency,
                Unit = m.Product.Unit,
                PackSize = m.Product.PackSize,
                PackPrice = m.Product.PackPrice,
                Specification = m.Product.Specification,
                Presentation = m.Product.Presentation,
                QualityRating = m.Product.QualityRating,
                IsOnSale = m.Product.IsOnSale,
                AvailableStock = m.Product.AvailableStock,
                LeadTimeDays = m.Product.LeadTimeDays,
                Url = m.Product.ProductUrl,
                Reason = m.Reason
            });

            var response = new AlternativesResult
            {
                Found = matches.Count > 0,
                Count = matches.Count,
                Alternatives = results.ToList()
            };

            return JsonSerializer.Serialize(response, JsonOptions);
        }
    }
}
