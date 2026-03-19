using IgnakeeAI.McpServer.Supplier.Application.Interfaces;
using IgnakeeAI.McpServer.Supplier.Application.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace IgnakeeAI.McpServer.Supplier.McpTools
{
    /// <summary>
    /// Tools MCP opcionales: checkAvailability y getBusinessHours.
    /// </summary>
    [McpServerToolType]
    public class AvailabilityTools
    {
        private readonly CatalogSearchService _search;
        private static readonly JsonSerializerOptions _JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private readonly ISupplierConfig _supplierConfig;

        public AvailabilityTools(CatalogSearchService search, ISupplierConfig supplierConfig)
        {
            _search = search;
            _supplierConfig = supplierConfig;
        }

        [McpServerTool, Description("Checks stock availability and estimated delivery time for a product.")]
        public async Task<string> CheckAvailability(
            [Description("Item code or SKU")] string itemCode,
            CancellationToken cancellationToken = default)
        {
            var result = await _search.CheckAvailabilityAsync(itemCode, cancellationToken);
            return JsonSerializer.Serialize(result, _JsonOpts);
        }

        [McpServerTool, Description("Returns business hours and contact information of this supplier.")]
        public string GetBusinessHours()
        {
            return JsonSerializer.Serialize(new
            {
                hours = _supplierConfig.BusinessHours,
                vendorName = _supplierConfig.VendorName,
                contactEmail = _supplierConfig.ContactEmail,
                contactPhone = _supplierConfig.ContactPhone,
                contactAddress = _supplierConfig.ContactAddress
            }, _JsonOpts);
        }
    }
}
