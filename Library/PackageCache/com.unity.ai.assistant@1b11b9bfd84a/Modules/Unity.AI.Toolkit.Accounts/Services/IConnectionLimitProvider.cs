namespace Unity.AI.Toolkit.Accounts.Services
{
    interface IConnectionLimitProvider
    {
        (int? GatewayConnections, int? McpConnections)? LicensingConnectionLimits { get; }
        bool HasProLicense { get; }
    }
}
