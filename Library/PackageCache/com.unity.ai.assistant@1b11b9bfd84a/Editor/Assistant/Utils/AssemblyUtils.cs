using System;
using System.IO;
using System.Reflection;
using System.Linq;
#if UNITY_6000_5_OR_NEWER
using UnityEngine;
using UnityEngine.Assemblies;
#endif

namespace Unity.AI.Assistant.Utils
{
    /// <summary>
    /// Utility methods for working with assemblies in a CoreCLR-compatible way.
    /// NOTE: This class can be removed once Unity 6000.5+ is the minimum supported version.
    /// At that point, call the CoreCLR-compatible APIs directly instead.
    /// </summary>
    static class AssemblyUtils
    {
        /// <summary>
        /// Gets all loaded assemblies in a CoreCLR-compatible way.
        /// This method centralizes the UNITY_6000_5_OR_NEWER check to avoid
        /// preprocessor directives scattered throughout the codebase.
        /// </summary>
        /// <returns>Array of all loaded assemblies.</returns>
        public static Assembly[] GetLoadedAssemblies()
        {
#if UNITY_6000_5_OR_NEWER
            return CurrentAssemblies.GetLoadedAssemblies().ToArray();
#else
            return AppDomain.CurrentDomain.GetAssemblies();
#endif
        }

        /// <summary>
        /// Gets the assembly path for the assembly containing the specified type.
        /// This method centralizes the UNITY_6000_5_OR_NEWER check to avoid
        /// preprocessor directives scattered throughout the codebase.
        /// </summary>
        /// <param name="type">The type whose assembly path should be retrieved.</param>
        /// <returns>The path to the assembly containing the specified type.</returns>
        public static string GetAssemblyPath(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

#if UNITY_6000_5_OR_NEWER
            return type.Assembly.GetLoadedAssemblyPath();
#else
            return type.Assembly.Location;
#endif
        }

        /// <summary>
        /// Gets the assembly path for the specified assembly.
        /// This method centralizes the UNITY_6000_5_OR_NEWER check to avoid
        /// preprocessor directives scattered throughout the codebase.
        /// </summary>
        /// <param name="assembly">The assembly whose path should be retrieved.</param>
        /// <returns>The path to the specified assembly.</returns>
        public static string GetAssemblyPath(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

#if UNITY_6000_5_OR_NEWER
            return assembly.GetLoadedAssemblyPath();
#else
            return assembly.Location;
#endif
        }

        static string[] s_FrameworkReferencePaths;

        /// <summary>
        /// Gets the framework assemblies a Roslyn compilation must reference in addition to <c>typeof(object).Assembly</c>.
        /// On CoreCLR the base library is split between System.Private.CoreLib and the System.Runtime, mscorlib and
        /// netstandard forwarders that Unity's assemblies are compiled against; on Mono, mscorlib holds all of it.
        /// </summary>
        /// <returns>Paths of the framework assemblies to reference; empty on Mono.</returns>
        public static string[] GetFrameworkReferencePaths()
        {
            if (s_FrameworkReferencePaths != null)
                return s_FrameworkReferencePaths;

            var coreLibrary = typeof(object).Assembly;
            var coreLibraryPath = GetAssemblyPath(coreLibrary);
            if (coreLibrary.GetName().Name != "System.Private.CoreLib" || string.IsNullOrEmpty(coreLibraryPath))
                return s_FrameworkReferencePaths = Array.Empty<string>();

            return s_FrameworkReferencePaths = Directory.GetFiles(Path.GetDirectoryName(coreLibraryPath), "*.dll")
                .Where(path => IsFrameworkAssemblyName(Path.GetFileNameWithoutExtension(path)) && HasManagedMetadata(path))
                .ToArray();
        }

        static bool IsFrameworkAssemblyName(string name) =>
            name == "mscorlib" || name == "netstandard" || name.StartsWith("System.", StringComparison.Ordinal);

        const ushort k_DosSignature = 0x5A4D;
        const uint k_PeSignature = 0x00004550;
        const ushort k_Pe32PlusMagic = 0x20B;
        const int k_Pe32CliHeaderDirectoryOffset = 208;
        const int k_Pe32PlusCliHeaderDirectoryOffset = 224;

        // Native libraries such as System.IO.Compression.Native.dll share the System.* naming but cannot be Roslyn references.
        static bool HasManagedMetadata(string path)
        {
            try
            {
                using var reader = new BinaryReader(File.OpenRead(path));
                if (reader.ReadUInt16() != k_DosSignature)
                    return false;

                reader.BaseStream.Position = 0x3C;
                var peHeaderOffset = reader.ReadInt32();
                reader.BaseStream.Position = peHeaderOffset;
                if (reader.ReadUInt32() != k_PeSignature)
                    return false;

                var optionalHeaderOffset = peHeaderOffset + 24;
                reader.BaseStream.Position = optionalHeaderOffset;
                var cliHeaderDirectoryOffset = reader.ReadUInt16() == k_Pe32PlusMagic
                    ? k_Pe32PlusCliHeaderDirectoryOffset
                    : k_Pe32CliHeaderDirectoryOffset;
                reader.BaseStream.Position = optionalHeaderOffset + cliHeaderDirectoryOffset;
                return reader.ReadUInt32() != 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Loads an assembly from a byte array in a CoreCLR-compatible way.
        /// This method centralizes the UNITY_6000_5_OR_NEWER check to avoid
        /// preprocessor directives scattered throughout the codebase.
        /// </summary>
        /// <param name="assemblyBytes">The byte array containing the assembly data.</param>
        /// <returns>The loaded assembly.</returns>
        public static Assembly LoadFromBytes(byte[] assemblyBytes)
        {
            if (assemblyBytes == null)
                throw new ArgumentNullException(nameof(assemblyBytes));

#if UNITY_6000_5_OR_NEWER
            return CurrentAssemblies.LoadFromBytes(assemblyBytes);
#else
            return Assembly.Load(assemblyBytes);
#endif
        }

        /// <summary>
        /// Loads an assembly from a file path in a CoreCLR-compatible way.
        /// This method centralizes the UNITY_6000_5_OR_NEWER check to avoid
        /// preprocessor directives scattered throughout the codebase.
        /// </summary>
        /// <param name="assemblyPath">The path to the assembly file.</param>
        /// <returns>The loaded assembly.</returns>
        public static Assembly LoadFromPath(string assemblyPath)
        {
            if (string.IsNullOrEmpty(assemblyPath))
                throw new ArgumentNullException(nameof(assemblyPath));

#if UNITY_6000_5_OR_NEWER
            return CurrentAssemblies.LoadFromPath(assemblyPath);
#else
            var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
            return Assembly.Load(assemblyName);
#endif
        }
    }
}
