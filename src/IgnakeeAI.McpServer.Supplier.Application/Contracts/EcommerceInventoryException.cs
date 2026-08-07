namespace IgnakeeAI.McpServer.Supplier.Application.Contracts;

public enum EcommerceInventoryFailureKind
{
    Authentication,
    Timeout,
    Technical,
    InvalidResponse
}

public sealed class EcommerceInventoryException : Exception
{
    public EcommerceInventoryException(
        EcommerceInventoryFailureKind kind,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public EcommerceInventoryFailureKind Kind { get; }
}
