namespace IgnakeeAI.McpServer.Supplier.Application.Contracts;

/// <summary>Ubicación operativa del proveedor para consumo de SmartRouting en Legio.</summary>
public sealed class SupplierLocation
{
    public string CountryCode { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string StreetNumber { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string Source { get; set; } = string.Empty;
    public bool IsValidated { get; set; }
    public DateTimeOffset? ValidatedAt { get; set; }
}
