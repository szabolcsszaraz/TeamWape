#if UNITY_EDITOR
using System;
using System.IO;
using Newtonsoft.Json;

namespace Unity.AI.Tracing
{
    /// <summary>
    /// Writes trace-config.json for the relay process to read on startup.
    /// </summary>
    static class TraceConfigFileWriter
    {
        /// <summary>
        /// Write trace-config.json for the relay to read on startup.
        /// Always writes a config so the relay knows to activate file output.
        /// </summary>
        internal static void WriteTraceConfigFile(string logDir)
        {
            var configPath = Path.Combine(logDir, "trace-config.json");

            try
            {
                var (fileConfig, consoleConfig) = TraceSinkConfigManager.GetRelayConfigs();
                var config = new
                {
                    file = new
                    {
                        defaultLevel = fileConfig.DefaultLevel,
                        filterRecurring = fileConfig.FilterRecurring,
                        categories = fileConfig.Categories,
                        sessions = fileConfig.Sessions,
                        components = fileConfig.Components,
                    },
                    console = new
                    {
                        defaultLevel = consoleConfig.DefaultLevel,
                        filterRecurring = consoleConfig.FilterRecurring,
                        categories = consoleConfig.Categories,
                        sessions = consoleConfig.Sessions,
                        components = consoleConfig.Components,
                    }
                };

                var json = TraceWriter.SerializeWithLocalSerializer(config, Formatting.Indented);

                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);

                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                Trace.Warn($"Failed to write trace config: {ex.Message}");
            }
        }
    }
}
#endif
