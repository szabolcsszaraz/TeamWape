#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;

namespace Unity.AI.Tracing
{
    /// <summary>
    /// Manages per-sink TraceConfig persistence using EditorUserSettings.
    /// Supports sink keys: "unity.file", "unity.console", "relay.file", "relay.console".
    /// </summary>
    static partial class TraceSinkConfigManager
    {
#if !UNITY_6000_6_OR_NEWER
        [Serializable]
#endif
        class PersistedData
        {
            public Dictionary<string, TraceConfig> Configs = new();
            public int MaxFileSizeMB = k_DefaultMaxFileSizeMB;
            public int TrimFileSizeMB = k_DefaultTrimFileSizeMB;
        }

        static PersistedData s_Data;
        static readonly object s_Lock = new();

        /// <summary>
        /// Default configs for each sink (hardcoded, shared with relay).
        /// </summary>
        static readonly Dictionary<string, TraceConfig> k_Defaults = new()
        {
            { "unity.file", new TraceConfig { DefaultLevel = "debug" } },
            { "unity.console", new TraceConfig { DefaultLevel = "warn" } },
            { "relay.file", new TraceConfig { DefaultLevel = "debug" } },
            { "relay.console", new TraceConfig { DefaultLevel = "info" } },
        };

        public static int MaxFileSizeMB
        {
            get { EnsureLoaded(); lock (s_Lock) { return s_Data.MaxFileSizeMB; } }
            set
            {
                EnsureLoaded();
                lock (s_Lock)
                {
                    s_Data.MaxFileSizeMB = Math.Max(1, value);
                    s_Data.TrimFileSizeMB = Math.Max(s_Data.TrimFileSizeMB, s_Data.MaxFileSizeMB);
                    SaveToSettings();
                }
            }
        }

        public static int TrimFileSizeMB
        {
            get { EnsureLoaded(); lock (s_Lock) { return s_Data.TrimFileSizeMB; } }
            set { EnsureLoaded(); lock (s_Lock) { s_Data.TrimFileSizeMB = Math.Max(s_Data.MaxFileSizeMB, value); SaveToSettings(); } }
        }

        /// <summary>
        /// Get the TraceConfig for a specific sink.
        /// Returns a clone to prevent accidental modification.
        /// </summary>
        /// <param name="sinkKey">Sink key, e.g. "unity.file", "relay.console"</param>
        public static TraceConfig GetSinkConfig(string sinkKey)
        {
            EnsureLoaded();
            lock (s_Lock)
            {
                if (s_Data.Configs.TryGetValue(sinkKey, out var config))
                    return CloneConfig(config);

                // Return default if not stored
                if (k_Defaults.TryGetValue(sinkKey, out var defaultConfig))
                    return CloneConfig(defaultConfig);

                return new TraceConfig { DefaultLevel = "info" };
            }
        }

        /// <summary>
        /// Set the TraceConfig for a specific sink and persist to EditorUserSettings.
        /// </summary>
        /// <param name="sinkKey">Sink key, e.g. "unity.file", "relay.console"</param>
        /// <param name="config">The config to store</param>
        public static void SetSinkConfig(string sinkKey, TraceConfig config)
        {
            EnsureLoaded();
            lock (s_Lock)
            {
                s_Data.Configs[sinkKey] = CloneConfig(config);
                SaveToSettings();
            }
        }

        /// <summary>
        /// Check if a sink config differs from the default.
        /// Used to determine if relay config file should be written.
        /// </summary>
        public static bool IsDefaultConfig(string sinkKey)
        {
            EnsureLoaded();
            lock (s_Lock)
            {
                if (!s_Data.Configs.TryGetValue(sinkKey, out var config))
                    return true; // Not stored = using default

                if (!k_Defaults.TryGetValue(sinkKey, out var defaultConfig))
                    return false; // No default = not default

                return ConfigEquals(config, defaultConfig);
            }
        }

        /// <summary>
        /// Get all relay sink configs for writing to config file.
        /// Returns (fileConfig, consoleConfig).
        /// </summary>
        public static (TraceConfig file, TraceConfig console) GetRelayConfigs()
        {
            return (GetSinkConfig("relay.file"), GetSinkConfig("relay.console"));
        }

        /// <summary>
        /// Check if relay configs are at defaults (no need to write config file).
        /// </summary>
        public static bool AreRelayConfigsDefault()
        {
            return IsDefaultConfig("relay.file") && IsDefaultConfig("relay.console");
        }

        static void EnsureLoaded()
        {
            if (s_Data != null) return;

            lock (s_Lock)
            {
                if (s_Data != null) return;
                LoadFromSettings();
            }
        }

        static void LoadFromSettings()
        {
            s_Data = new PersistedData();

            var json = EditorUserSettings.GetConfigValue(k_SettingKey);
            if (string.IsNullOrEmpty(json))
                return;

            try
            {
                // Try deserializing as the new PersistedData format first
                var stored = JsonConvert.DeserializeObject<PersistedData>(json);
                if (stored != null)
                {
                    s_Data = stored;
                    s_Data.Configs ??= new Dictionary<string, TraceConfig>();

                    // Backward compat: old data stored TrimThresholdMB as a delta (e.g. 2).
                    // If the loaded value is less than MaxFileSizeMB, it's an old delta — convert
                    // to an absolute trigger size by adding MaxFileSizeMB.
                    if (s_Data.TrimFileSizeMB < s_Data.MaxFileSizeMB)
                        s_Data.TrimFileSizeMB = s_Data.MaxFileSizeMB + s_Data.TrimFileSizeMB;

                    return;
                }
            }
            catch
            {
                // Fall through to legacy format
            }

            try
            {
                // Backward compat: try the old Dictionary<string, TraceConfig> format
                var legacyConfigs = JsonConvert.DeserializeObject<Dictionary<string, TraceConfig>>(json);
                if (legacyConfigs != null)
                    s_Data.Configs = legacyConfigs;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[TraceSinkConfigManager] Failed to load config: {ex.Message}");
            }
        }

        static void SaveToSettings()
        {
            try
            {
                var json = JsonConvert.SerializeObject(s_Data, Formatting.None,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                EditorUserSettings.SetConfigValue(k_SettingKey, json);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[TraceSinkConfigManager] Failed to save config: {ex.Message}");
            }
        }

        static TraceConfig CloneConfig(TraceConfig config)
        {
            return new TraceConfig
            {
                DefaultLevel = config.DefaultLevel,
                FilterRecurring = config.FilterRecurring,
                Categories = config.Categories != null
                    ? new Dictionary<string, string>(config.Categories)
                    : null,
                Sessions = config.Sessions != null
                    ? new Dictionary<string, string>(config.Sessions)
                    : null,
                Components = config.Components != null
                    ? new Dictionary<string, string>(config.Components)
                    : null,
            };
        }

        static bool ConfigEquals(TraceConfig a, TraceConfig b)
        {
            return a.DefaultLevel == b.DefaultLevel
                && a.FilterRecurring == b.FilterRecurring
                && DictionaryEquals(a.Categories, b.Categories)
                && DictionaryEquals(a.Sessions, b.Sessions)
                && DictionaryEquals(a.Components, b.Components);
        }

        static bool DictionaryEquals(Dictionary<string, string> a, Dictionary<string, string> b)
        {
            // Both null or empty = equal
            var aEmpty = a == null || a.Count == 0;
            var bEmpty = b == null || b.Count == 0;
            if (aEmpty && bEmpty) return true;
            if (aEmpty != bEmpty) return false;

            if (a.Count != b.Count) return false;

            foreach (var kvp in a)
            {
                if (!b.TryGetValue(kvp.Key, out var bValue) || kvp.Value != bValue)
                    return false;
            }

            return true;
        }
    }
}
#endif

namespace Unity.AI.Tracing
{
    static partial class TraceSinkConfigManager
    {
        const string k_SettingKey = "Trace.SinkConfigs";
        const int k_DefaultMaxFileSizeMB = 10;
        const int k_DefaultTrimFileSizeMB = 12;
    }
}

