#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace Unity.InferenceEngine.Editor.Analytics.Import
{
    /// <summary>
    /// Manages Sentis analytics scripting define symbol.
    /// Adds/removes SENTIS_ANALYTICS_ENABLED based on:
    /// 1. Scripting define: FORCE_SENTIS_ANALYTICS (for CI/testing or local dev) - bypasses all other conditions
    /// 2. Compile-time flag: ENABLE_CLOUD_SERVICES_ANALYTICS && UNITY_2023_2_OR_NEWER
    /// 3. Runtime setting: UnityEditor.Analytics.AnalyticsSettings.enabled (user's analytics opt-out)
    ///
    /// CI pipelines inject FORCE_SENTIS_ANALYTICS into ProjectSettings.asset before Unity starts,
    /// ensuring analytics tests are compiled and discovered during initial script compilation.
    /// </summary>
    static class AnalyticsDefineManager
    {
        const string k_AnalyticsDefine = "SENTIS_ANALYTICS_ENABLED";
        const string k_ForceAnalyticsDefine = "FORCE_SENTIS_ANALYTICS";

        [InitializeOnLoadMethod]
        static void Initialize()
        {
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            var target = NamedBuildTarget.FromBuildTargetGroup(group);
            var defineStr = PlayerSettings.GetScriptingDefineSymbols(target);
            var defines = new HashSet<string>(defineStr.Split(';').Where(s => !string.IsNullOrWhiteSpace(s)));

            // CI injects FORCE_SENTIS_ANALYTICS into ProjectSettings.asset before Unity starts.
            // When detected, ensure SENTIS_ANALYTICS_ENABLED is also set and skip removal logic.
            if (defines.Contains(k_ForceAnalyticsDefine))
            {
                if (defines.Add(k_AnalyticsDefine))
                {
                    var newDefineStr = string.Join(";", defines);
                    PlayerSettings.SetScriptingDefineSymbols(target, newDefineStr);
                }
                return;
            }

#if FORCE_SENTIS_ANALYTICS
            // Compile-time check: FORCE_SENTIS_ANALYTICS present at compile time (CI or manual dev setup)
            var shouldEnable = true;
#elif UNITY_2023_2_OR_NEWER && ENABLE_CLOUD_SERVICES_ANALYTICS
            // Both compile-time conditions met, check user's editor analytics preference
            var shouldEnable = EditorAnalytics.enabled;
#else
            var shouldEnable = false;
#endif

            var changed = false;
            if (shouldEnable)
            {
                if (defines.Add(k_AnalyticsDefine))
                    changed = true;
            }
            else
            {
                if (defines.Remove(k_AnalyticsDefine))
                    changed = true;
            }

            if (changed)
            {
                var newDefineStr = string.Join(";", defines);
                PlayerSettings.SetScriptingDefineSymbols(target, newDefineStr);
            }
        }
    }
}
#endif
