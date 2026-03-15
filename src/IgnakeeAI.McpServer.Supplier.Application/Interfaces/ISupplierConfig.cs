namespace IgnakeeAI.McpServer.Supplier.Application.Interfaces
{
    public interface ISupplierConfig
    {
        public string ContactEmail { get; }
        public string ContactPhone { get; }
        public string ContactAddress { get; }
        public string VendorName { get; }
        public string BusinessHours { get; }
    }
}
