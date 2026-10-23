using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.InferenceEngine
{
    /// <summary>
    /// Represents information about the compute capabilities of the GPU.
    /// </summary>
    class ComputeInfo
    {
        /// <summary>
        /// Whether the GPU supports shared memory.
        /// </summary>
        public static bool supportsComputeSharedMemory = true;

        /// <summary>
        /// Whether the GPU supports dense 32 x 32 kernels.
        /// </summary>
        public static bool supportsDense32x32 = true;

        /// <summary>
        /// Whether the GPU supports dense 64 x 64 kernels.
        /// </summary>
        public static bool supportsDense64x64 = true;

        /// <summary>
        /// Whether the GPU supports compute.
        /// </summary>
        public static bool supportsCompute = true;

        /// <summary>
        /// The maximum compute work group size the GPU supports.
        /// </summary>
        public static uint maxComputeWorkGroupSize = 1024;

        /// <summary>
        /// The vendor of the GPU.
        /// </summary>
        public static string graphicsDeviceVendor = "";

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticsOnLoad()
        {
            supportsComputeSharedMemory = true;
            supportsDense32x32 = true;
            supportsDense64x64 = true;
            supportsCompute = true;
            maxComputeWorkGroupSize = 1024;
            graphicsDeviceVendor = "";
            m_UsingDirect3DAPI =  false;
        }
#endif
        /// <summary>
        /// Determines whether the GPU is a mobile GPU, for example Android, iPhone or Intel.
        /// </summary>
        /// <returns>Whether the GPU is a mobile GPU.</returns>
        public static bool IsMobileGPU() { return
            (Application.platform == RuntimePlatform.Android) ||
            (Application.platform == RuntimePlatform.IPhonePlayer);
        }

        /// <summary>
        /// Determines whether the GPU is a Mac GPU.
        /// </summary>
        /// <returns>Whether the GPU is a Mac GPU.</returns>
        public static bool IsMacGPU() { return
            (Application.platform == RuntimePlatform.OSXEditor) ||
            (Application.platform == RuntimePlatform.OSXPlayer);
        }

        /// <summary>
        /// Determines whether the GPU is an iPhone GPU.
        /// </summary>
        /// <returns>Whether the GPU is an iPhone GPU.</returns>
        public static bool IsiPhoneGPU() { return
            (Application.platform == RuntimePlatform.IPhonePlayer);
        }

        /// <summary>
        /// Determines whether the GPU is an Android Qualcomm GPU.
        /// </summary>
        /// <returns>Whether the GPU is an Android Qualcomm GPU.</returns>
        public static bool IsQualcommGPU() { return
            (Application.platform == RuntimePlatform.Android) && graphicsDeviceVendor.Contains("Qualcomm");
        }

        /// <summary>
        /// Determines whether the GPU is an Android ARM GPU.
        /// </summary>
        /// <returns>Whether the GPU is an Android ARM GPU.</returns>
        public static bool IsARMGPU() { return
            (Application.platform == RuntimePlatform.Android) && graphicsDeviceVendor.Contains("ARM");
        }

        static bool m_UsingDirect3DAPI = false;
        /// <summary>
        /// Determines whether we're running on the D3D API.
        /// </summary>
        /// <returns>Whether we're running on the D3D API.</returns>
        public static bool IsDirect3DAPI { get => m_UsingDirect3DAPI; }

        /// <summary>
        /// Determines whether the GPU is an Apple Mx GPU
        /// </summary>
        /// <returns>Whether the GPU is an Apple Mx GPU</returns>
        public static bool IsAppleMGPU()
        {
            var gpuName = SystemInfo.graphicsDeviceName;
            return gpuName != null && gpuName.StartsWith("Apple M");
        }

        /// <summary>
        /// Determines whether the GPU is an Intel GPU.
        /// </summary>
        /// <returns>Whether the GPU is an Intel GPU.</returns>
        public static bool IsIntelGPU()
        {
            var gpuName = SystemInfo.graphicsDeviceName;
            return gpuName != null && gpuName.StartsWith("Intel"); // "Intel(R) UHD Graphics 630"
        }

        static ComputeInfo()
        {
            supportsCompute = SystemInfo.supportsComputeShaders;

            graphicsDeviceVendor = SystemInfo.graphicsDeviceVendor;

            // TODO switch to SystemInfo.maxComputeWorkGroupSize when we bump min spec to 2019.3
            if (Application.platform == RuntimePlatform.Android)
            {
                maxComputeWorkGroupSize = (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan) ? 256u : 128u;

                var gpuName = SystemInfo.graphicsDeviceName ?? "";
                var osName = SystemInfo.operatingSystem ?? "";

                // Known issue with Adreno Vulkan drivers on Android 8.x
                if (gpuName.Contains("Adreno") && osName.StartsWith("Android OS 8") &&
                    SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan)
                    maxComputeWorkGroupSize = 128u;
            }
            else if (Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.tvOS)
            {
                var gpuName = SystemInfo.graphicsDeviceName;
                if (gpuName != null && gpuName.StartsWith("Apple A"))
                {
                    int gpuNumber = 0, idx = "Apple A".Length;
                    while (idx < gpuName.Length && '0' <= gpuName[idx] && gpuName[idx] <= '9')
                    {
                        gpuNumber = gpuNumber * 10 + gpuName[idx++] - '0';
                    }

                    // TODO check on lower end iOS devices
                    maxComputeWorkGroupSize = (gpuNumber <= 10) ? 224u : 256u;
                }
                else
                {
                    maxComputeWorkGroupSize = 256u;
                }
            }
            switch (SystemInfo.graphicsDeviceType)
            {
                case GraphicsDeviceType.Direct3D11:
                case GraphicsDeviceType.Direct3D12:
                case GraphicsDeviceType.GameCoreXboxOne:
                case GraphicsDeviceType.GameCoreXboxSeries:
                    m_UsingDirect3DAPI = true;
                    break;
            }
        }
    }
}
