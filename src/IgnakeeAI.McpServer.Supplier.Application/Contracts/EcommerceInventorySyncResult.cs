namespace IgnakeeAI.McpServer.Supplier.Application.Contracts;

public sealed record EcommerceInventorySyncResult(
    int ProductsRead,
    int ProductsCreated,
    int ProductsUpdated,
    int ProductsRejected);
