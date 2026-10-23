using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Unity.AI.Toolkit.Accounts.Services.Data
{
    /// <summary>
    /// Reads settings values off the SDK's <c>SettingsResult</c> by name.
    /// <para>
    /// The backend returns <c>HasAiSubscription</c> and <c>OptedInBillingOrgId</c>, and the SDK
    /// models them, but the vendored <c>AiEditorToolsSdk.dll</c> predates both, so a direct property
    /// access would not compile. This exists purely to keep the package building until the DLL is
    /// re-vendored; replace both call sites with direct property access at that point and delete it.
    /// </para>
    /// </summary>
    static class SdkSettingsCompat
    {
        static readonly HashSet<string> k_WarnedMissing = new();

        /// <summary>
        /// Reads a nullable bool. Null means the backend could not determine the answer, and is
        /// preserved rather than collapsed to false: a failed lookup upstream must not be reported
        /// as a definite "no".
        /// </summary>
        internal static bool? ReadNullableFlag(object source, string propertyName)
        {
            var property = FindProperty(source, propertyName);
            if (property == null || (property.PropertyType != typeof(bool) && property.PropertyType != typeof(bool?)))
            {
                WarnOnce(propertyName);
                return null;
            }

            return property.GetValue(source) as bool?;
        }

        internal static string ReadString(object source, string propertyName)
        {
            var property = FindProperty(source, propertyName);
            if (property == null || property.PropertyType != typeof(string))
            {
                WarnOnce(propertyName);
                return null;
            }

            return property.GetValue(source) as string;
        }

        static PropertyInfo FindProperty(object source, string propertyName) =>
            source?.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

        static void WarnOnce(string propertyName)
        {
            // Once per name per session. Expected until the SDK is re-vendored, but it also surfaces
            // a later property rename, which would otherwise silently read as "unknown" forever.
            if (k_WarnedMissing.Add(propertyName))
                Debug.Log($"[AI Toolkit] SDK settings expose no '{propertyName}'; treating it as unknown until the SDK ships it.");
        }
    }
}
