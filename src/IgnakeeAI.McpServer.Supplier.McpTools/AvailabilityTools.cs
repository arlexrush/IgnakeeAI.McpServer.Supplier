using IgnakeeAI.McpServer.Supplier.Application.Interfaces;
using IgnakeeAI.McpServer.Supplier.Application.Contracts;
using IgnakeeAI.McpServer.Supplier.Application.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace IgnakeeAI.McpServer.Supplier.McpTools
{
    /// <summary>
    /// Tools MCP públicas: CheckAvailability y GetBusinessHours.
    /// </summary>
    [McpServerToolType]
    public class AvailabilityTools
    {
        private readonly CatalogSearchService _search;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        private readonly ISupplierConfig _supplierConfig;

        public AvailabilityTools(CatalogSearchService search, ISupplierConfig supplierConfig)
        {
            _search = search;
            _supplierConfig = supplierConfig;
        }

        [McpServerTool(Name = SupplierMcpToolNames.CheckAvailability), Description("Checks stock availability and estimated delivery time for a product.")]
        public async Task<string> CheckAvailability(
            [Description("Item code or SKU")] string itemCode,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(itemCode))
                throw new ArgumentException("itemCode es obligatorio.", nameof(itemCode));
            var result = await _search.CheckAvailabilityAsync(itemCode, cancellationToken);
            return JsonSerializer.Serialize(result, JsonOptions);
        }

        [McpServerTool(Name = SupplierMcpToolNames.GetBusinessHours), Description("Returns business hours and contact information of this supplier.")]
        public string GetBusinessHours()
        {
            var result = new BusinessHoursResult
            {
                Hours = _supplierConfig.BusinessHours,
                VendorName = _supplierConfig.VendorName,
                ContactEmail = _supplierConfig.ContactEmail,
                ContactPhone = _supplierConfig.ContactPhone,
                ContactAddress = _supplierConfig.ContactAddress
            };

            return JsonSerializer.Serialize(result, JsonOptions);
        }
    }
}
