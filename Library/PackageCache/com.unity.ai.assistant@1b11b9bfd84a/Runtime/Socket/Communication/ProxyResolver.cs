using System;

namespace Unity.AI.Assistant.Socket.Communication
{
    /// <summary>
    /// Resolves an HTTP/HTTPS proxy for a target URI from the standard proxy
    /// environment variables (HTTPS_PROXY / HTTP_PROXY / NO_PROXY, upper- or
    /// lower-case). Self-contained mirror of the UUM-146339 rules (a follow-up
    /// will consolidate the two copies).
    /// </summary>
    static class ProxyResolver
    {
        internal static bool TryResolveProxy(Uri target, out Uri proxyUri)
            => TryResolveProxy(target, System.Environment.GetEnvironmentVariable, out proxyUri);

        internal static bool TryResolveProxy(Uri target, Func<string, string> getEnv, out Uri proxyUri)
        {
            proxyUri = null;
            if (target == null)
                return false;

            if (IsBypassed(target.Host, FirstNonEmpty(getEnv("NO_PROXY"), getEnv("no_proxy"))))
                return false;

            var secure = target.Scheme == "wss" || target.Scheme == "https";
            var url = secure
                ? FirstNonEmpty(getEnv("HTTPS_PROXY"), getEnv("https_proxy"), getEnv("HTTP_PROXY"), getEnv("http_proxy"))
                : FirstNonEmpty(getEnv("HTTP_PROXY"), getEnv("http_proxy"));

            if (string.IsNullOrWhiteSpace(url))
                return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out proxyUri))
            {
                proxyUri = null;
                return false;
            }
            return true;
        }

        internal static bool IsBypassed(string host, string noProxy)
        {
            if (string.IsNullOrEmpty(host))
                return false;
            var h = host.ToLowerInvariant();
            if (h == "localhost" || h == "127.0.0.1" || h == "::1" || h == "[::1]")
                return true;
            if (string.IsNullOrWhiteSpace(noProxy))
                return false;
            foreach (var raw in noProxy.Split(','))
            {
                var entry = raw.Trim().ToLowerInvariant();
                if (entry.Length == 0)
                    continue;
                if (entry == "*")
                    return true;
                var bare = entry.StartsWith(".") ? entry.Substring(1) : entry;
                // A NO_PROXY entry may be port-qualified (e.g. "host:443"); match on
                // the host portion, treating the port as optional. Mirrors the
                // accounts-side resolver. Guarded to a single trailing ":port" so
                // IPv6 forms ("::1", "[::1]") are left untouched.
                var colon = bare.IndexOf(':');
                if (colon > 0 && bare.IndexOf(':', colon + 1) < 0 && int.TryParse(bare.Substring(colon + 1), out _))
                    bare = bare.Substring(0, colon);
                if (h == bare || h.EndsWith("." + bare))
                    return true;
            }
            return false;
        }

        static string FirstNonEmpty(params string[] values)
        {
            foreach (var v in values)
                if (!string.IsNullOrWhiteSpace(v))
                    return v;
            return null;
        }
    }
}
