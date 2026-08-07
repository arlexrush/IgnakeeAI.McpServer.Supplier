namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors.Ecommerce
{
    /// <summary>Fallo técnico de red o timeout al comunicarse con el ecommerce.</summary>
    public sealed class EcommerceCommunicationException : Exception
    {
        public EcommerceCommunicationException(string message, Exception? inner = null)
            : base(message, inner) { }
    }

    /// <summary>La API del ecommerce rechazó la autenticación (401/403).</summary>
    public sealed class EcommerceAuthException : Exception
    {
        public EcommerceAuthException(string message, Exception? inner = null)
            : base(message, inner) { }
    }

    /// <summary>El JSON devuelto por el ecommerce no pudo ser deserializado al DTO esperado.</summary>
    public sealed class EcommerceMappingException : Exception
    {
        public EcommerceMappingException(string message, Exception? inner = null)
            : base(message, inner) { }
    }
}
