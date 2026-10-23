using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

namespace Unity.AI.Toolkit.Accounts.Services.Core
{
    /// <summary>
    /// Resolves an HTTP/HTTPS proxy from the standard proxy environment
    /// variables (HTTPS_PROXY / HTTP_PROXY / NO_PROXY, upper- or lower-case).
    /// Returns a configured <see cref="WebProxy"/> whose bypass list reflects
    /// NO_PROXY, or null when no proxy is configured.
    /// </summary>
    static class ProxyResolver
    {
        // System.Environment is fully qualified: an in-scope Unity `Environment` type shadows it here.
        internal static WebProxy CreateProxy() => CreateProxy(System.Environment.GetEnvironmentVariable);

        internal static WebProxy CreateProxy(Func<string, string> getEnv)
        {
            var proxyUrl = FirstNonEmpty(
                getEnv("HTTPS_PROXY"), getEnv("https_proxy"),
                getEnv("HTTP_PROXY"), getEnv("http_proxy"));

            if (string.IsNullOrWhiteSpace(proxyUrl))
                return null;

            var proxy = new WebProxy(proxyUrl);

            // Always bypass loopback, mirroring the TypeScript resolver's implicit
            // localhost/loopback rule. We do NOT set BypassProxyOnLocal: it also bypasses
            // any dot-less host (e.g. "https://webserver"), which would diverge from the
            // TS side and skip the proxy for internal environments that still need it.
            var bypass = new List<string>(k_LoopbackBypass);
            bypass.AddRange(BuildBypassList(FirstNonEmpty(getEnv("NO_PROXY"), getEnv("no_proxy"))));
            proxy.BypassList = bypass.ToArray();

            return proxy;
        }

        // Matched against WebProxy's "scheme://host[:port]" string; anchored at the
        // "//" (or start) boundary through the end so only the exact loopback hosts match.
        static readonly string[] k_LoopbackBypass =
        {
            "(?:^|//)localhost(?::\\d+)?$",
            "(?:^|//)127\\.0\\.0\\.1(?::\\d+)?$",
            "(?:^|//)::1(?::\\d+)?$",
            "(?:^|//)\\[::1\\](?::\\d+)?$",
        };

        internal static List<string> BuildBypassList(string noProxy)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(noProxy))
                return result;

            foreach (var raw in noProxy.Split(','))
            {
                var entry = raw.Trim();
                if (entry.Length == 0)
                    continue;
                if (entry == "*")
                {
                    result.Add(".*");
                    continue;
                }
                var bare = entry.StartsWith(".") ? entry.Substring(1) : entry;
                var escaped = Regex.Escape(bare);
                // WebProxy matches BypassList regexes against "scheme://host[:port]".
                // Anchor the host at a label boundary (start, "//", or ".") through the
                // end, so "corp.local" bypasses "corp.local" and "host.corp.local" but
                // not "notcorp.local" or "corp.local.evil.com".
                result.Add($"(?:^|//|\\.){escaped}(?::\\d+)?$");
            }
            return result;
        }

        static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
            return null;
        }
    }
}
