using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.Editor.Context
{
    static class ImageReferenceImporter
    {
        internal const string k_Folder = "Assets/AI Toolkit/Temp/AssistantImageReferences";
        internal const string k_ValueType = "AttachedImageReference";
        const string k_GenerateAssetToolName = "Unity.AssetGeneration.GenerateAsset";
        static readonly TimeSpan k_Ttl = TimeSpan.FromDays(7);

        // Per-session cache: hash -> InstanceID. Skips the File.Exists/Load round-trip on repeat sends.
        static readonly Dictionary<string, long> k_Cache = new();

        /// <summary>
        /// Lazily import an externally-attached image into a deduped, in-project asset and
        /// return the resulting Texture2D's InstanceID. Returns 0 on any failure so callers
        /// can skip emitting the hint and degrade to today's behavior.
        /// </summary>
        public static long EnsureImportedAndGetInstanceId(string base64Payload, string format)
        {
            if (string.IsNullOrEmpty(base64Payload))
                return 0;

            try
            {
                var bytes = Convert.FromBase64String(base64Payload);
                var hash = ComputeHash(bytes);

                if (k_Cache.TryGetValue(hash, out var cachedId) && ResolveTexture(cachedId) != null)
                    return cachedId;

                var ext = string.IsNullOrEmpty(format) ? "png" : format.ToLowerInvariant();
                var assetPath = $"{k_Folder}/{hash}.{ext}";

                if (!File.Exists(assetPath))
                {
                    Directory.CreateDirectory(k_Folder);
                    File.WriteAllBytes(assetPath, bytes);
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                }
                else
                {
                    // Refresh mtime so the sweeper treats this asset as recently used.
                    try { File.SetLastWriteTimeUtc(assetPath, DateTime.UtcNow); } catch { /* best-effort */ }
                }

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (texture == null)
                    return 0;

                var id = GetInstanceId(texture);
                if (id != 0)
                    k_Cache[hash] = id;
                return id;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ImageReferenceImporter] Failed to import attachment: {ex.Message}");
                return 0;
            }
        }

        public static string BuildHint(long instanceId) =>
            $"[Image reference] An image attached to this conversation is also available as an in-project Texture2D with InstanceID={instanceId}. " +
            $"If a tool you call accepts a project image reference (for example, `referenceImageInstanceId(s)` on `{k_GenerateAssetToolName}`), you can pass {instanceId}. " +
            $"For any other use of the image (visual analysis, answering questions about its contents, etc.), treat it as the attached image — this hint is informational only.";

        static long GetInstanceId(Texture2D texture)
        {
#if UNITY_6000_5_OR_NEWER
            return (long)EntityId.ToULong(texture.GetEntityId());
#else
            return texture.GetInstanceID();
#endif
        }

        static Texture2D ResolveTexture(long id)
        {
#if UNITY_6000_5_OR_NEWER
            return EditorUtility.EntityIdToObject(EntityId.FromULong((ulong)id)) as Texture2D;
#elif UNITY_6000_3_OR_NEWER
            return EditorUtility.EntityIdToObject((int)id) as Texture2D;
#else
            return EditorUtility.InstanceIDToObject((int)id) as Texture2D;
#endif
        }

        static string ComputeHash(byte[] bytes)
        {
            using var sha = SHA1.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        [InitializeOnLoadMethod]
        static void SweepStaleEntries()
        {
            try
            {
                if (!Directory.Exists(k_Folder))
                    return;

                var cutoff = DateTime.UtcNow - k_Ttl;
                foreach (var path in Directory.EnumerateFiles(k_Folder))
                {
                    if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                        continue;
                    try
                    {
                        if (File.GetLastWriteTimeUtc(path) >= cutoff)
                            continue;
                        AssetDatabase.DeleteAsset(path.Replace('\\', '/'));
                    }
                    catch { /* Per-file failures are not fatal */ }
                }
            }
            catch { /* Best-effort cleanup; never block editor startup. */ }
        }
    }
}
