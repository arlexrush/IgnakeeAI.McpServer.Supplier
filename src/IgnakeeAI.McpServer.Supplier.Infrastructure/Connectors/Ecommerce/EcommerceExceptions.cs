using IgnakeeAI.McpServer.Supplier.Application.Contracts;

namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors.Ecommerce
{
    /// <summary>Fallo técnico de red o timeout al comunicarse con el ecommerce.</summary>
    public sealed class EcommerceCommunicationException : Exception, IEcommerceFailure
    {
        public EcommerceFailureKind FailureKind => EcommerceFailureKind.Transient;

        public EcommerceCommunicationException(string message, Exception? inner = null)
            : base(message, inner) { }
    }

    /// <summary>La API del ecommerce rechazó la autenticación (401/403).</summary>
    public sealed class EcommerceAuthException : Exception, IEcommerceFailure
    {
        public EcommerceFailureKind FailureKind => EcommerceFailureKind.Authentication;

        public EcommerceAuthException(string message, Exception? inner = null)
            : base(message, inner) { }
    }

    /// <summary>El JSON devuelto por el ecommerce no pudo ser deserializado al DTO esperado.</summary>
    public sealed class EcommerceMappingException : Exception, IEcommerceFailure
    {
        public EcommerceFailureKind FailureKind => EcommerceFailureKind.Mapping;

        public EcommerceMappingException(string message, Exception? inner = null)
            : base(message, inner) { }
    }
}
