using System;
using System.IO;
using Unity.AI.Assistant.Editor.Checkpoint.Git;
using Unity.AI.Assistant.Utils;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.Editor
{
    [Serializable]
    class AssistantProjectSettings
    {
        public string CustomInstructionsGUID;
        public bool CheckpointEnabled;
        public int GitInstanceTypeValue;
        public string CustomGitPath;
        public int CheckpointRetentionWeeks = 2;

        /// <summary>Was the hub's plan prompt banner dismissed by the user</summary>
        public bool PlanExecutionPromptDismissed;

        /// <summary>Was the checkpoint discovery banner dismissed by the user</summary>
        public bool CheckpointDiscoveryBannerDismissed;

        /// <summary>Did the user explicitly disable checkpoints via the discovery banner's Disable button</summary>
        public bool CheckpointDiscoveryUserDisabled;

        /// <summary>Did the discovery auto-init flow actually run for this project. Gates the banner's
        /// terminal states so users who already had checkpoints enabled don't see an uninvited banner.</summary>
        public bool CheckpointDiscoveryFlowStarted;
    }

    static class AssistantProjectPreferences
    {
        static readonly string k_SettingsPath =
            Path.Combine("ProjectSettings", "Packages", AssistantConstants.PackageName, "Settings.json");

        internal static Action CustomInstructionsFilePathChanged;
        internal static Action CheckpointEnabledChanged;
        internal static Action GitInstanceTypeChanged;

        static AssistantProjectSettings s_Settings;

        internal static AssistantProjectSettings Settings
        {
            get
            {
                if (s_Settings == null)
                {
                    if (!File.Exists(k_SettingsPath))
                    {
                        s_Settings = new AssistantProjectSettings();
                        Save();
                    }
                    else
                    {
                        try
                        {
                            var json = File.ReadAllText(k_SettingsPath);
                            s_Settings = JsonUtility.FromJson<AssistantProjectSettings>(json);
                        }
                        catch (Exception ex)
                        {
                            InternalLog.LogException(ex);
                            s_Settings = new AssistantProjectSettings();
                        }
                    }
                }

                return s_Settings;
            }
        }

        static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(k_SettingsPath));

                var json = JsonUtility.ToJson(s_Settings, true);
                File.WriteAllText(k_SettingsPath, json);
            }
            catch (Exception ex)
            {
                InternalLog.LogException(ex);
            }
        }

        public static string CustomInstructionsFilePath
        {
            get
            {
                var guidString = Settings.CustomInstructionsGUID;
                if (GUID.TryParse(guidString, out var guid))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    return path;
                }

                return null;
            }
            set
            {
                var guid = AssetDatabase.AssetPathToGUID(value);

                Settings.CustomInstructionsGUID = guid;
                Save();

                CustomInstructionsFilePathChanged?.Invoke();
            }
        }

        public static bool CheckpointEnabled
        {
            get => Settings.CheckpointEnabled;
            set
            {
                if (Settings.CheckpointEnabled == value)
                    return;

                Settings.CheckpointEnabled = value;
                Save();

                CheckpointEnabledChanged?.Invoke();
            }
        }

        public static GitInstanceType GitInstanceType
        {
            get => (GitInstanceType)Settings.GitInstanceTypeValue;
            set
            {
                var intValue = (int)value;
                if (Settings.GitInstanceTypeValue == intValue)
                    return;

                Settings.GitInstanceTypeValue = intValue;
                Save();

                GitInstanceTypeChanged?.Invoke();
            }
        }

        public static string CustomGitPath
        {
            get => Settings.CustomGitPath ?? string.Empty;
            set
            {
                if (Settings.CustomGitPath == value)
                    return;

                Settings.CustomGitPath = value;
                Save();

                GitInstanceTypeChanged?.Invoke();
            }
        }

        public static int CheckpointRetentionWeeks
        {
            get => Settings.CheckpointRetentionWeeks > 0 ? Settings.CheckpointRetentionWeeks : 2;
            set
            {
                var clampedValue = Math.Clamp(value, 1, 2);
                if (Settings.CheckpointRetentionWeeks == clampedValue)
                    return;

                Settings.CheckpointRetentionWeeks = clampedValue;
                Save();
            }
        }

        public static bool PlanExecutionPromptDismissed
        {
            get => Settings.PlanExecutionPromptDismissed;
            set
            {
                if (Settings.PlanExecutionPromptDismissed == value)
                    return;

                Settings.PlanExecutionPromptDismissed = value;
                Save();
            }
        }

        public static bool CheckpointDiscoveryBannerDismissed
        {
            get => Settings.CheckpointDiscoveryBannerDismissed;
            set
            {
                if (Settings.CheckpointDiscoveryBannerDismissed == value)
                    return;

                Settings.CheckpointDiscoveryBannerDismissed = value;
                Save();
            }
        }

        public static bool CheckpointDiscoveryUserDisabled
        {
            get => Settings.CheckpointDiscoveryUserDisabled;
            set
            {
                if (Settings.CheckpointDiscoveryUserDisabled == value)
                    return;

                Settings.CheckpointDiscoveryUserDisabled = value;
                Save();
            }
        }

        public static bool CheckpointDiscoveryFlowStarted
        {
            get => Settings.CheckpointDiscoveryFlowStarted;
            set
            {
                if (Settings.CheckpointDiscoveryFlowStarted == value)
                    return;

                Settings.CheckpointDiscoveryFlowStarted = value;
                Save();
            }
        }
    }
}
