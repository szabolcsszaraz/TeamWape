using Unity.AI.Toolkit.Accounts.Services.Data;

namespace Unity.AI.Toolkit.Accounts.Services
{
    static class ConnectionLimitResolver
    {
        public static ConnectionLimitSnapshot Resolve(SettingsRecord settings, IConnectionLimitProvider provider)
        {
            int? backendGateway = GetBackendGatewayConnections(settings);
            int? backendMcp = GetBackendMcpConnections(settings);
            var licensing = provider?.LicensingConnectionLimits;
            int? licensingGateway = licensing.HasValue ? NormalizeCap(licensing.Value.GatewayConnections) : null;
            int? licensingMcp = licensing.HasValue ? NormalizeCap(licensing.Value.McpConnections) : null;
            int? proFallback = licensing == null && (provider?.HasProLicense ?? false) ? 3 : null;

            var gateway = ResolvePool(backendGateway, licensingGateway, proFallback);
            var mcp = ResolvePool(backendMcp, licensingMcp, proFallback);

            return new ConnectionLimitSnapshot(
                gateway.Value,
                mcp.Value,
                backendGateway,
                backendMcp,
                licensingGateway,
                licensingMcp,
                proFallback,
                gateway.Source,
                mcp.Source);
        }

        static int? GetBackendGatewayConnections(SettingsRecord settings)
        {
            if (settings == null)
                return null;
            return NormalizeCap(settings.AllowedGatewayConnections);
        }

        static int? GetBackendMcpConnections(SettingsRecord settings)
        {
            if (settings == null)
                return null;
            return NormalizeCap(settings.AllowedMcpConnections);
        }

        static (int Value, ConnectionLimitSource Source) ResolvePool(int? backend, int? licensing, int? proFallback)
        {
            int value = 0;
            var source = ConnectionLimitSource.None;

            Consider(backend, ConnectionLimitSource.Backend, ref value, ref source);
            Consider(licensing, ConnectionLimitSource.Licensing, ref value, ref source);
            Consider(proFallback, ConnectionLimitSource.ProFallback, ref value, ref source);

            return (value, source);
        }

        static void Consider(int? candidate, ConnectionLimitSource candidateSource, ref int value, ref ConnectionLimitSource source)
        {
            if (!candidate.HasValue)
                return;

            if (candidate.Value == -1)
            {
                if (value == -1)
                    source |= candidateSource;
                else
                {
                    value = -1;
                    source = candidateSource;
                }
                return;
            }

            if (value == -1)
                return;

            if (candidate.Value > value)
            {
                value = candidate.Value;
                source = candidateSource;
            }
            else if (candidate.Value == value)
            {
                source |= candidateSource;
            }
        }

        static int? NormalizeCap(int? value)
        {
            if (!value.HasValue)
                return null;
            return NormalizeCap(value.Value);
        }

        static int? NormalizeCap(int value)
        {
            return value == -1 || value >= 0 ? value : null;
        }
    }
}
