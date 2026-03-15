using IgnakeeAI.McpServer.Supplier.Application.Interfaces;

namespace IgnakeeAI.McpServer.Supplier.Tests.Fakes
{
    public class TestSupplierConfig : ISupplierConfig
    {
        public string ContactEmail { get; init; } = "compras@proveedor-test.local";
        public string ContactPhone { get; init; } = "+34 900 000 000";
        public string ContactAddress { get; init; } = "Calle Test 123, Madrid";
        public string VendorName { get; init; } = "Proveedor Test";
        public string BusinessHours { get; init; } = "L-V 08:00-18:00";
    }
}
