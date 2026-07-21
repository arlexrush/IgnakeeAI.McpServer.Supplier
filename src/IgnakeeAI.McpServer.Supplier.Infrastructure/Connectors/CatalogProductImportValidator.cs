using IgnakeeAI.McpServer.Supplier.Domain.Entities;

namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors;

internal static class CatalogProductImportValidator
{
    public static bool TryValidate(CatalogProduct product, out string? reason)
    {
        if (string.IsNullOrWhiteSpace(product.ItemCode))
            return Fail("ItemCode vacío.", out reason);
        if (string.IsNullOrWhiteSpace(product.Description))
            return Fail("Description vacía.", out reason);
        if (product.UnitPrice < 0)
            return Fail("UnitPrice no puede ser negativo.", out reason);
        if (product.AvailableStock is < 0)
            return Fail("AvailableStock no puede ser negativo.", out reason);
        if (product.LeadTimeDays is < 0)
            return Fail("LeadTimeDays no puede ser negativo.", out reason);
        if (string.IsNullOrWhiteSpace(product.Currency) ||
            product.Currency.Length != 3 ||
            product.Currency.Any(c => !char.IsAsciiLetter(c)))
            return Fail("Currency debe contener tres letras.", out reason);

        reason = null;
        return true;
    }

    private static bool Fail(string message, out string? reason)
    {
        reason = message;
        return false;
    }
}
