namespace Unity.AI.Toolkit.Accounts.Services
{
    record ConnectionLimitSnapshot(
        int AllowedGatewayConnections,
        int AllowedMcpConnections,
        int? BackendGatewayConnections,
        int? BackendMcpConnections,
        int? LicensingGatewayConnections,
        int? LicensingMcpConnections,
        int? ProFallbackConnections,
        ConnectionLimitSource GatewaySource,
        ConnectionLimitSource McpSource);
}
