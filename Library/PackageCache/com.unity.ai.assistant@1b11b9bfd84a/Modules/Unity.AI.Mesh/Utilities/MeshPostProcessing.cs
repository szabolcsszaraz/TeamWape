using System;
using Unity.AI.Mesh.Services.Stores.States;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Mesh.Services.Utilities
{
    static class MeshPostProcessing
    {
        /// <summary>
        /// Post-process the specified prefab using the settings specified in `MeshSettingsState`.
        /// The prefab must be already imported into `Assets`.
        /// </summary>
        public static void PostProcessMeshPrefab(GameObject prefab, MeshSettingsState settings)
        {
            if (prefab == null || settings == null ||
                PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.NotAPrefab)
                return;

            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                bool dirty = false;
                dirty |= ApplyPivotMode(prefabRoot, settings.pivotMode);

                if (dirty)
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        /// <summary>
        /// Shifts all direct children of the root so its origin aligns with the desired pivot point.
        /// First resets any prior pivot adjustment by re-centering children to the geometry center,
        /// then applies the new desired pivot mode.
        /// Returns whether the prefab was modified.
        /// </summary>
        static bool ApplyPivotMode(GameObject prefabRoot, MeshPivotMode pivotMode)
        {
            // Step 1: Compute current local-space bounds to find where geometry actually is
            Bounds? currentBounds = GetCombinedLocalBounds(prefabRoot);
            if (currentBounds == null)
                return false;

            // Step 2: Reset children so geometry is centered at the root origin.
            // This undoes any prior pivot adjustment.
            Vector3 geometryCenter = currentBounds.Value.center;
            if (geometryCenter.sqrMagnitude > float.Epsilon)
            {
                foreach (Transform child in prefabRoot.transform)
                    child.localPosition -= geometryCenter;
            }

            // Step 3: Recompute bounds after centering
            Bounds? centeredBounds = GetCombinedLocalBounds(prefabRoot);
            if (centeredBounds == null)
                return false;

            // Step 4: Apply the desired pivot offset
            Vector3 offset = GetPivotOffset(centeredBounds.Value, pivotMode);
            if (offset.sqrMagnitude > float.Epsilon)
            {
                foreach (Transform child in prefabRoot.transform)
                    child.localPosition -= offset;
            }

            return true;
        }

        /// <summary>
        /// Computes the combined bounds of all renderers in local space relative to the root transform.
        /// Uses mesh bounds transformed by each renderer's local-to-root matrix to avoid
        /// world-space issues when the prefab root has a non-identity transform.
        /// </summary>
        static Bounds? GetCombinedLocalBounds(GameObject root)
        {
            Bounds? combined = null;
            var rootTransform = root.transform;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Bounds localBounds;
                if (renderer is MeshRenderer)
                {
                    var meshFilter = renderer.GetComponent<MeshFilter>();
                    if (meshFilter == null || meshFilter.sharedMesh == null)
                        continue;
                    localBounds = meshFilter.sharedMesh.bounds;
                }
                else if (renderer is SkinnedMeshRenderer skinnedRenderer)
                {
                    if (skinnedRenderer.sharedMesh == null)
                        continue;
                    localBounds = skinnedRenderer.sharedMesh.bounds;
                }
                else
                {
                    continue;
                }

                // Transform mesh-local bounds into root-local space
                var localToRoot = rootTransform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                var boundsInRoot = TransformBounds(localBounds, localToRoot);

                if (combined == null)
                    combined = boundsInRoot;
                else
                {
                    Bounds b = combined.Value;
                    b.Encapsulate(boundsInRoot);
                    combined = b;
                }
            }

            return combined;
        }

        static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            var center = matrix.MultiplyPoint3x4(bounds.center);
            var extents = bounds.extents;

            // Transform extents by the absolute values of the matrix axes to get the AABB
            var axisX = new Vector3(matrix.m00, matrix.m10, matrix.m20);
            var axisY = new Vector3(matrix.m01, matrix.m11, matrix.m21);
            var axisZ = new Vector3(matrix.m02, matrix.m12, matrix.m22);

            var newExtents = new Vector3(
                Mathf.Abs(axisX.x) * extents.x + Mathf.Abs(axisY.x) * extents.y + Mathf.Abs(axisZ.x) * extents.z,
                Mathf.Abs(axisX.y) * extents.x + Mathf.Abs(axisY.y) * extents.y + Mathf.Abs(axisZ.y) * extents.z,
                Mathf.Abs(axisX.z) * extents.x + Mathf.Abs(axisY.z) * extents.y + Mathf.Abs(axisZ.z) * extents.z
            );

            return new Bounds(center, newExtents * 2f);
        }

        static Vector3 GetPivotOffset(Bounds bounds, MeshPivotMode pivotMode)
        {
            return pivotMode switch
            {
                MeshPivotMode.Center => bounds.center,
                MeshPivotMode.BottomCenter => new Vector3(bounds.center.x, bounds.min.y, bounds.center.z),
                _ => throw new ArgumentOutOfRangeException(nameof(pivotMode), pivotMode, null)
            };
        }
    }
}