using IgnakeeAI.McpServer.Labor.Application.Interfaces;

namespace IgnakeeAI.McpServer.Labor.Tests.Fakes
{
    public class TestLaborConfig : ILaborConfig
    {
        public string AgencyName { get; init; } = "Agencia Test";
        public string ContactEmail { get; init; } = "contacto@agencia-test.local";
        public string ContactPhone { get; init; } = "+34 900 000 000";
        public string ContactAddress { get; init; } = "Calle Test 123, Madrid";
        public string BusinessHours { get; init; } = "L-V 08:00-18:00";
    }
}
