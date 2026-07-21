namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Configuration;

/// <summary>Configuración pública del contrato MCP del servidor proveedor.</summary>
public sealed class SupplierMcpOptions
{
    public const string SectionName = "Mcp";

    public string ContractVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Versión negociada por el transporte MCP. Vacía significa que no se fija
    /// una versión en metadata para no contradecir al SDK.
    /// </summary>
    public string ProtocolVersion { get; set; } = string.Empty;

    public string ServerName { get; set; } = "IgnakeeAI MCP Supplier Server";

    public string ServerVersion { get; set; } = "1.0.0";
}
