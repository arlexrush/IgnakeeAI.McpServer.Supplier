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
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private readonly ISupplierConfig SupplierConfig;

        public AvailabilityTools(CatalogSearchService search, ISupplierConfig supplierConfig)
        {
            _search = search;
            SupplierConfig = supplierConfig;
        }

        [McpServerTool, Description("Checks stock availability and estimated delivery time for a product.")]
        public async Task<string> CheckAvailability(
            [Description("Item code or SKU")] string itemCode)
        {
            var result = await _search.CheckAvailabilityAsync(itemCode, CancellationToken.None);
            return JsonSerializer.Serialize(result, JsonOpts);
        }

        [McpServerTool, Description("Returns business hours and contact information of this supplier.")]
        public string GetBusinessHours()
        {
            return JsonSerializer.Serialize(new
            {
                hours = SupplierConfig.BusinessHours,
                vendorName = SupplierConfig.VendorName,
                contactEmail = SupplierConfig.ContactEmail,
                contactPhone = SupplierConfig.ContactPhone,
                contactAddress = SupplierConfig.ContactAddress
            }, JsonOpts);
        }
    }
}
