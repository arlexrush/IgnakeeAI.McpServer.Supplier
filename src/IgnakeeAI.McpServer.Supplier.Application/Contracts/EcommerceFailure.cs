namespace IgnakeeAI.McpServer.Supplier.Application.Contracts
{
    /// <summary>Clasifica el motivo por el que falló una llamada al ecommerce.</summary>
    public enum EcommerceFailureKind
    {
        Transient,
        Authentication,
        Mapping,
        Unknown
    }

    /// <summary>Expone la clasificación de un fallo devuelto por el adaptador ecommerce.</summary>
    public interface IEcommerceFailure
    {
        EcommerceFailureKind FailureKind { get; }
    }
}
