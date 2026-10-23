using System;
using System.IO;
using System.Threading.Tasks;
#if GLTFAST_AVAILABLE
using GLTFast;
using GLTFast.Export;
#endif
using UnityEngine;

namespace Unity.AI.Mesh.Services.Utilities
{
    static class GltfExportUtils
    {
        static readonly string[] k_NativeModelExtensions = { ".fbx", ".glb" };

        public static bool IsNativeModelFormat(string path)
        {
            var extension = Path.GetExtension(path);
            foreach (var ext in k_NativeModelExtensions)
            {
                if (string.Equals(extension, ext, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static bool IsFbxFormat(string path)
        {
            return string.Equals(Path.GetExtension(path), ".fbx", StringComparison.OrdinalIgnoreCase);
        }

        public static async Task<Stream> ExportToGlbStreamAsync(GameObject gameObject)
        {
#if GLTFAST_AVAILABLE
            var export = new GameObjectExport(
                new ExportSettings { Format = GltfFormat.Binary },
                gameObjectExportSettings: new GameObjectExportSettings { OnlyActiveInHierarchy = false });
            export.AddScene(new[] { gameObject });

            var stream = new MemoryStream();
            var success = await export.SaveToStreamAndDispose(stream);

            if (!success)
            {
                stream.Dispose();
                throw new InvalidOperationException($"Failed to export '{gameObject.name}' to GLB.");
            }

            stream.Position = 0;

            var tempPath = Path.Combine(Application.dataPath, "..", "Temp", $"{gameObject.name}_export.glb");
            using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            {
                await stream.CopyToAsync(fileStream);
            }

            stream.Position = 0;
            Debug.Log($"GLB debug export saved to: {tempPath}");

            return stream;
#else
            await Task.CompletedTask;
            throw new InvalidOperationException("glTFast is required for this operation. Open a 3D Generator window to install it.");
#endif
        }
    }
}
