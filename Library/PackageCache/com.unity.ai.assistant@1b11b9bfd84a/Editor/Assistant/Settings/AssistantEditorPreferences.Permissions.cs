using UnityEditor;
using System;
using System.Collections.Generic;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.FunctionCalling;

namespace Unity.AI.Assistant.Editor
{
    /// <summary>
    /// Partial class containing permission-related settings
    /// </summary>
    static partial class AssistantEditorPreferences
    {
        /// <summary>
        /// Permission-related settings for the AI Assistant
        /// </summary>
        public static class Permissions
        {
            const string k_FirstPartyToolPolicy = k_SettingsPrefix + "FirstPartyToolPolicy";
            const string k_ThirdPartyToolPolicy = k_SettingsPrefix + "ThirdPartyToolPolicy";
            const string k_ReadExternalFilesPolicy = k_SettingsPrefix + "ReadExternalFilesPolicy";
            const string k_ReadProjectPolicy = k_SettingsPrefix + "ReadProjectPolicy";
            const string k_ModifyProjectPolicy = k_SettingsPrefix + "ModifyProjectPolicy";
            const string k_PlayModePolicy = k_SettingsPrefix + "PlayModePolicy";
            const string k_ScreenCapturePolicy = k_SettingsPrefix + "ScreenCapturePolicy";
            const string k_CodeExecutionPolicy = k_SettingsPrefix + "CodeExecutionPolicy";
            const string k_AssetGenerationPolicy = k_SettingsPrefix + "AssetGenerationPolicy";

            public static event Action<IPermissionsPolicyProvider.PermissionPolicy> FirstPartyToolPolicyChanged;
            public static event Action<IPermissionsPolicyProvider.PermissionPolicy> ThirdPartyToolPolicyChanged;
            public static event Action<IPermissionsPolicyProvider.PermissionPolicy> ReadExternalFilesPolicyChanged;
            public static event Action<IPermissionsPolicyProvider.PermissionPolicy> ReadProjectPolicyChanged;
            public static event Action<IPermissionsPolicyProvider.PermissionPolicy> ModifyProjectPolicyChanged;
            public static event Action<IPermissionsPolicyProvider.PermissionPolicy> PlayModePolicyChanged;
            public static event Action<IPermissionsPolicyProvider.PermissionPolicy> ScreenCapturePolicyChanged;
            public static event Action<IPermissionsPolicyProvider.PermissionPolicy> CodeExecutionPolicyChanged;
            public static event Action<IPermissionsPolicyProvider.PermissionPolicy> AssetGenerationPolicyChanged;

            /// <summary>
            /// Permission policy for first-party tool execution (Unity AI Assistant tools)
            /// </summary>
            public static IPermissionsPolicyProvider.PermissionPolicy FirstPartyToolPolicy
            {
                get => (IPermissionsPolicyProvider.PermissionPolicy)EditorPrefs.GetInt(k_FirstPartyToolPolicy, (int)IPermissionsPolicyProvider.PermissionPolicy.Allow);
                set
                {
                    if (FirstPartyToolPolicy != value)
                    {
                        EditorPrefs.SetInt(k_FirstPartyToolPolicy, (int)value);
                        FirstPartyToolPolicyChanged?.Invoke(value);
                    }
                }
            }

            /// <summary>
            /// Permission policy for third-party tool execution (MCP Servers)
            /// </summary>
            public static IPermissionsPolicyProvider.PermissionPolicy ThirdPartyToolPolicy
            {
                get => (IPermissionsPolicyProvider.PermissionPolicy)EditorPrefs.GetInt(k_ThirdPartyToolPolicy, (int)IPermissionsPolicyProvider.PermissionPolicy.Ask);
                set
                {
                    if (ThirdPartyToolPolicy != value)
                    {
                        EditorPrefs.SetInt(k_ThirdPartyToolPolicy, (int)value);
                        ThirdPartyToolPolicyChanged?.Invoke(value);
                    }
                }
            }

            /// <summary>
            /// Permission policy for reading files outside the project path
            /// </summary>
            public static IPermissionsPolicyProvider.PermissionPolicy ReadExternalFilesPolicy
            {
                get => (IPermissionsPolicyProvider.PermissionPolicy)EditorPrefs.GetInt(k_ReadExternalFilesPolicy, (int)IPermissionsPolicyProvider.PermissionPolicy.Ask);
                set
                {
                    if (ReadExternalFilesPolicy != value)
                    {
                        EditorPrefs.SetInt(k_ReadExternalFilesPolicy, (int)value);
                        ReadExternalFilesPolicyChanged?.Invoke(value);
                    }
                }
            }

            /// <summary>
            /// Permission policy for reading project files and Unity objects
            /// </summary>
            public static IPermissionsPolicyProvider.PermissionPolicy ReadProjectPolicy
            {
                get => (IPermissionsPolicyProvider.PermissionPolicy)EditorPrefs.GetInt(k_ReadProjectPolicy, (int)IPermissionsPolicyProvider.PermissionPolicy.Allow);
                set
                {
                    if (ReadProjectPolicy != value)
                    {
                        EditorPrefs.SetInt(k_ReadProjectPolicy, (int)value);
                        ReadProjectPolicyChanged?.Invoke(value);
                    }
                }
            }

            /// <summary>
            /// Permission policy for creating, modifying, and deleting project files and Unity objects
            /// </summary>
            public static IPermissionsPolicyProvider.PermissionPolicy ModifyProjectPolicy
            {
                get => (IPermissionsPolicyProvider.PermissionPolicy)EditorPrefs.GetInt(k_ModifyProjectPolicy, (int)IPermissionsPolicyProvider.PermissionPolicy.Ask);
                set
                {
                    if (ModifyProjectPolicy != value)
                    {
                        EditorPrefs.SetInt(k_ModifyProjectPolicy, (int)value);
                        ModifyProjectPolicyChanged?.Invoke(value);
                    }
                }
            }

            /// <summary>
            /// Permission policy for entering and exiting Play Mode
            /// </summary>
            public static IPermissionsPolicyProvider.PermissionPolicy PlayModePolicy
            {
                get => (IPermissionsPolicyProvider.PermissionPolicy)EditorPrefs.GetInt(k_PlayModePolicy, (int)IPermissionsPolicyProvider.PermissionPolicy.Ask);
                set
                {
                    if (PlayModePolicy != value)
                    {
                        EditorPrefs.SetInt(k_PlayModePolicy, (int)value);
                        PlayModePolicyChanged?.Invoke(value);
                    }
                }
            }

            /// <summary>
            /// Permission policy for taking screen captures
            /// </summary>
            public static IPermissionsPolicyProvider.PermissionPolicy ScreenCapturePolicy
            {
                get => (IPermissionsPolicyProvider.PermissionPolicy)EditorPrefs.GetInt(k_ScreenCapturePolicy, (int)IPermissionsPolicyProvider.PermissionPolicy.Ask);
                set
                {
                    if (ScreenCapturePolicy != value)
                    {
                        EditorPrefs.SetInt(k_ScreenCapturePolicy, (int)value);
                        ScreenCapturePolicyChanged?.Invoke(value);
                    }
                }
            }

            /// <summary>
            /// Permission policy for executing generated C# code
            /// </summary>
            public static IPermissionsPolicyProvider.PermissionPolicy CodeExecutionPolicy
            {
                get => (IPermissionsPolicyProvider.PermissionPolicy)EditorPrefs.GetInt(k_CodeExecutionPolicy, (int)IPermissionsPolicyProvider.PermissionPolicy.Ask);
                set
                {
                    if (CodeExecutionPolicy != value)
                    {
                        EditorPrefs.SetInt(k_CodeExecutionPolicy, (int)value);
                        CodeExecutionPolicyChanged?.Invoke(value);
                    }
                }
            }

            /// <summary>
            /// Permission policy for generating new assets
            /// </summary>
            public static IPermissionsPolicyProvider.PermissionPolicy AssetGenerationPolicy
            {
                get => (IPermissionsPolicyProvider.PermissionPolicy)EditorPrefs.GetInt(k_AssetGenerationPolicy, (int)IPermissionsPolicyProvider.PermissionPolicy.Ask);
                set
                {
                    if (AssetGenerationPolicy != value)
                    {
                        EditorPrefs.SetInt(k_AssetGenerationPolicy, (int)value);
                        AssetGenerationPolicyChanged?.Invoke(value);
                    }
                }
            }

            static bool s_PermissionPoliciesInitialized;

            [InitializeOnLoadMethod]
            static void InitPermissionPoliciesForBackend()
            {
                if (s_PermissionPoliciesInitialized)
                    return;
                s_PermissionPoliciesInitialized = true;

                UpdatePermissionPolicies();
                FirstPartyToolPolicyChanged += _ => UpdatePermissionPolicies();
                ThirdPartyToolPolicyChanged += _ => UpdatePermissionPolicies();
                ReadExternalFilesPolicyChanged += _ => UpdatePermissionPolicies();
                ReadProjectPolicyChanged += _ => UpdatePermissionPolicies();
                ModifyProjectPolicyChanged += _ => UpdatePermissionPolicies();
                PlayModePolicyChanged += _ => UpdatePermissionPolicies();
                ScreenCapturePolicyChanged += _ => UpdatePermissionPolicies();
                CodeExecutionPolicyChanged += _ => UpdatePermissionPolicies();
                AssetGenerationPolicyChanged += _ => UpdatePermissionPolicies();
            }

            static string PolicyToString(IPermissionsPolicyProvider.PermissionPolicy policy) =>
                policy switch
                {
                    IPermissionsPolicyProvider.PermissionPolicy.Allow => "allow",
                    IPermissionsPolicyProvider.PermissionPolicy.Ask => "ask",
                    IPermissionsPolicyProvider.PermissionPolicy.Deny => "deny",
                    _ => "ask"
                };

            static void UpdatePermissionPolicies()
            {
                CapabilityRegistry.SetPermissionPolicies(new Dictionary<string, string>
                {
                    [CapabilityRegistry.PermissionFirstPartyTool] = PolicyToString(FirstPartyToolPolicy),
                    [CapabilityRegistry.PermissionThirdPartyTool] = PolicyToString(ThirdPartyToolPolicy),
                    [CapabilityRegistry.PermissionReadExternalFiles] = PolicyToString(ReadExternalFilesPolicy),
                    [CapabilityRegistry.PermissionReadProject] = PolicyToString(ReadProjectPolicy),
                    [CapabilityRegistry.PermissionModifyProject] = PolicyToString(ModifyProjectPolicy),
                    [CapabilityRegistry.PermissionPlayMode] = PolicyToString(PlayModePolicy),
                    [CapabilityRegistry.PermissionScreenCapture] = PolicyToString(ScreenCapturePolicy),
                    [CapabilityRegistry.PermissionCodeExecution] = PolicyToString(CodeExecutionPolicy),
                    [CapabilityRegistry.PermissionAssetGeneration] = PolicyToString(AssetGenerationPolicy),
                });
            }

        }
    }
}
