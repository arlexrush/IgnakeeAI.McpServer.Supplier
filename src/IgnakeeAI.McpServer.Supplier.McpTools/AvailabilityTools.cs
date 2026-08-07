using IgnakeeAI.McpServer.Supplier.Application.Interfaces;
using IgnakeeAI.McpServer.Supplier.Application.Contracts;
using IgnakeeAI.McpServer.Supplier.Application.Services;
using IgnakeeAI.McpServer.Supplier.Domain.Entities;
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
        private readonly IEcommerceInventoryService? _ecommerceInventory;

        public AvailabilityTools(
            CatalogSearchService search,
            ISupplierConfig supplierConfig,
            IEcommerceInventoryService? ecommerceInventory = null)
        {
            _search = search;
            _supplierConfig = supplierConfig;
            _ecommerceInventory = ecommerceInventory;
        }

        [McpServerTool(Name = SupplierMcpToolNames.CheckAvailability), Description("Checks stock availability and estimated delivery time for a product.")]
        public async Task<string> CheckAvailability(
            [Description("Item code or SKU")] string itemCode,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(itemCode))
                throw new ArgumentException("itemCode es obligatorio.", nameof(itemCode));

            if (_ecommerceInventory?.IsEnabled == true)
            {
                try
                {
                    var liveProduct = await _ecommerceInventory.FindByCodeAsync(itemCode, cancellationToken);
                    if (liveProduct is not null)
                    {
                        return JsonSerializer.Serialize(BuildAvailabilityResult(itemCode, liveProduct), JsonOptions);
                    }
                }
                catch (EcommerceInventoryException)
                {
                }
            }

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

        private static AvailabilityResult BuildAvailabilityResult(string itemCode, CatalogProduct product)
        {
            var isAvailableForSale = product.IsActive;
            var availableStock = isAvailableForSale ? Math.Max(0, product.AvailableStock ?? 0) : 0;

            return new AvailabilityResult
            {
                Found = isAvailableForSale,
                ItemCode = string.IsNullOrWhiteSpace(product.ItemCode) ? itemCode : product.ItemCode,
                AvailableStock = availableStock,
                LeadTimeDays = Math.Max(0, product.LeadTimeDays ?? 0),
                Message = !isAvailableForSale
                    ? "No disponible"
                    : availableStock > 0 ? "Disponible" : "Sin stock"
            };
        }
    }
}
