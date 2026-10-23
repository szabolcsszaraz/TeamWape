using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering
{
    /// <summary>
    /// Implement a multiple buffering for RenderTextures.
    /// </summary>
    /// <example>
    /// <code>
    /// enum BufferType
    /// {
    ///     Color,
    ///     Depth
    /// }
    ///
    /// void Render()
    /// {
    ///     var camera = GetCamera();
    ///     var buffers = GetFrameHistoryBuffersFor(camera);
    ///
    ///     // Set reference size in case the rendering size changed this frame
    ///     buffers.SetReferenceSize(
    ///         GetCameraWidth(camera), GetCameraHeight(camera),
    ///         GetCameraUseMSAA(camera), GetCameraMSAASamples(camera)
    ///     );
    ///     buffers.Swap();
    ///
    ///     var currentColor = buffer.GetFrameRT((int)BufferType.Color, 0);
    ///     if (currentColor == null) // Buffer was not allocated
    ///     {
    ///         buffer.AllocBuffer(
    ///             (int)BufferType.Color,      // Color buffer id
    ///             ColorBufferAllocator,       // Custom functor to implement allocation
    ///             2                           // Use 2 RT for this buffer for double buffering
    ///         );
    ///         currentColor = buffer.GetFrameRT((int)BufferType.Color, 0);
    ///     }
    ///
    ///     var previousColor = buffers.GetFrameRT((int)BufferType.Color, 1);
    ///
    ///     // Use previousColor and write into currentColor
    /// }
    /// </code>
    /// </example>
    public class BufferedRTHandleSystem : IDisposable
    {
        struct RTEntry
        {
            public RTHandle handle;
            public int stableIndex;
        }

        Dictionary<int, RTEntry[]> m_RTEntries = new Dictionary<int, RTEntry[]>();

        RTHandleSystem m_RTHandleSystem = new RTHandleSystem();
        bool m_DisposedValue = false;

        /// <summary>
        /// Maximum allocated width of the Buffered RTHandle System
        /// </summary>
        public int maxWidth { get { return m_RTHandleSystem.GetMaxWidth(); } }
        /// <summary>
        /// Maximum allocated height of the Buffered RTHandle System
        /// </summary>
        public int maxHeight { get { return m_RTHandleSystem.GetMaxHeight(); } }
        /// <summary>
        /// Current properties of the Buffered RTHandle System
        /// </summary>
        public RTHandleProperties rtHandleProperties { get { return m_RTHandleSystem.rtHandleProperties; } }

        bool TryGetFrameRenderTarget(int bufferId, int frameIndex, out RTEntry rt)
        {
            if (!m_RTEntries.ContainsKey(bufferId))
            {
                rt.handle = null;
                rt.stableIndex = -1;
                return false;
            }

            Assert.IsTrue(frameIndex >= 0 && frameIndex < m_RTEntries[bufferId].Length);

            rt = m_RTEntries[bufferId][frameIndex];
            return true;
        }

        /// <summary>
        /// Return the frame RT or null.
        /// </summary>
        /// <param name="bufferId">Defines the buffer to use.</param>
        /// <param name="frameIndex">Defines which frame to access within the buffer.</param>
        /// <returns>The frame RT or null when the <paramref name="bufferId"/> was not previously allocated (<see cref="BufferedRTHandleSystem.AllocBuffer(int, Func{RTHandleSystem, int, RTHandle}, int)" />).</returns>
        public RTHandle GetFrameRT(int bufferId, int frameIndex)
        {
            if (TryGetFrameRenderTarget(bufferId, frameIndex, out var rt))
                return rt.handle;

            return null;
        }

        /// <summary>
        /// Return the frame RT's stable index or -1.
        /// A stable index can be used to index into an array of user data assigned to each RT.
        /// </summary>
        /// <param name="bufferId">Defines the buffer to use.</param>
        /// <param name="frameIndex">Defines which frame to access within the buffer.</param>
        /// <returns>The frame RT stable index or -1 when the <paramref name="bufferId"/> was not previously allocated (<see cref="BufferedRTHandleSystem.AllocBuffer(int, Func{RTHandleSystem, int, RTHandle}, int)" />).</returns>
        public int GetFrameRTStableIndex(int bufferId, int frameIndex)
        {
            if (TryGetFrameRenderTarget(bufferId, frameIndex, out var rt))
                return rt.stableIndex;

            return -1;
        }

        /// <summary>
        /// Clears all the previously created history buffers
        /// </summary>
        /// <param name="cmd">Defines the command buffer used for clearing.</param>

        public void ClearBuffers(CommandBuffer cmd)
        {
            foreach (var rtEntry in m_RTEntries)
            {
                for (int i = 0; i < rtEntry.Value.Length; ++i)
                {
                    CoreUtils.SetRenderTarget(cmd, rtEntry.Value[i].handle, clearFlag: ClearFlag.Color, clearColor: Color.black);
                }
            }
        }

        /// <summary>
        /// Allocate RT handles for a buffer.
        /// </summary>
        /// <param name="bufferId">The buffer to allocate.</param>
        /// <param name="allocator">The functor to use for allocation.</param>
        /// <param name="bufferCount">The number of RT handles for this buffer.</param>
        public void AllocBuffer(
            int bufferId,
            Func<RTHandleSystem, int, RTHandle> allocator,
            int bufferCount
        )
        {
            // This function should only be used when there is a non-zero number of buffers to allocate.
            // If the caller provides a value of zero, they're likely doing something unintentional in the calling code.
            Debug.Assert(bufferCount > 0);

            var buffer = new RTEntry[bufferCount];
            m_RTEntries.Add(bufferId, buffer);

            // First is autoresized
            buffer[0].handle = allocator(m_RTHandleSystem, 0);
            buffer[0].stableIndex = 0;

            // Other are resized on demand
            for (int i = 1, c = buffer.Length; i < c; ++i)
            {
                buffer[i].handle = allocator(m_RTHandleSystem, i);
                buffer[i].stableIndex = i;
                m_RTHandleSystem.SwitchResizeMode(buffer[i].handle, RTHandleSystem.ResizeMode.OnDemand);
            }
        }

        /// <summary>
        /// Allocate RT handles for a buffer using a RenderTextureDescriptor.
        /// </summary>
        /// <param name="bufferId">The buffer to allocate.</param>
        /// <param name="bufferCount">The number of RT handles for this buffer.</param>
        /// <param name="descriptor">RenderTexture descriptor of the RTHandles.</param>
        /// <param name="filterMode">Filtering mode of the RTHandles.</param>
        /// <param name="wrapMode">Addressing mode of the RTHandles.</param>
        /// <param name="isShadowMap">Set to true if the depth buffer should be used as a shadow map.</param>
        /// <param name="anisoLevel">Anisotropic filtering level.</param>
        /// <param name="mipMapBias">Bias applied to mipmaps during filtering.</param>
        /// <param name="name">Name of the RTHandle.</param>
        // NOTE: API is similar to RTHandles.Alloc.
        public void AllocBuffer(int bufferId, int bufferCount,
            ref RenderTextureDescriptor descriptor,
            FilterMode filterMode = FilterMode.Point,
            TextureWrapMode wrapMode = TextureWrapMode.Repeat,
            bool isShadowMap = false,
            int anisoLevel = 1,
            float mipMapBias = 0,
            string name = "")
        {
            // This function should only be used when there is a non-zero number of buffers to allocate.
            // If the caller provides a value of zero, they're likely doing something unintentional in the calling code.
            Debug.Assert(bufferCount > 0);

            var buffer = new RTEntry[bufferCount];
            m_RTEntries.Add(bufferId, buffer);

            RTHandleAllocInfo allocInfo = RTHandles.GetRTHandleAllocInfo(descriptor, filterMode,
                wrapMode, anisoLevel, mipMapBias, name);
            allocInfo.isShadowMap = isShadowMap;

            // First is autoresized
            buffer[0].handle = m_RTHandleSystem.Alloc(descriptor.width, descriptor.height, allocInfo);
            buffer[0].stableIndex = 0;

            // Other are resized on demand
            for (int i = 1, c = buffer.Length; i < c; ++i)
            {
                buffer[i].handle = m_RTHandleSystem.Alloc(descriptor.width, descriptor.height, allocInfo);
                buffer[i].stableIndex = i;
            }
        }

        /// <summary>
        /// Release a buffer
        /// </summary>
        /// <param name="bufferId">Id of the buffer that needs to be released.</param>
        public void ReleaseBuffer(int bufferId)
        {
            if (m_RTEntries.TryGetValue(bufferId, out var buffers))
            {
                foreach (var rt in buffers)
                    m_RTHandleSystem.Release(rt.handle);
            }

            m_RTEntries.Remove(bufferId);
        }

        /// <summary>
        /// Swap buffers Set the reference size for this RT Handle System (<see cref="RTHandleSystem.SetReferenceSize(int, int, bool)"/>)
        /// </summary>
        /// <param name="width">The width of the RTs of this buffer.</param>
        /// <param name="height">The height of the RTs of this buffer.</param>
        public void SwapAndSetReferenceSize(int width, int height)
        {
            Swap();
            m_RTHandleSystem.SetReferenceSize(width, height);
        }

        /// <summary>
        /// Reset the reference size of the system and reallocate all textures.
        /// </summary>
        /// <param name="width">New width.</param>
        /// <param name="height">New height.</param>
        public void ResetReferenceSize(int width, int height)
        {
            m_RTHandleSystem.ResetReferenceSize(width, height);
        }

        /// <summary>
        /// Queries the number of RT handle buffers allocated for a buffer ID.
        /// </summary>
        /// <param name="bufferId">The buffer ID to query.</param>
        /// <returns>The num of frames allocated</returns>
        public int GetNumFramesAllocated(int bufferId)
        {
            if (!m_RTEntries.ContainsKey(bufferId))
                return 0;

            return m_RTEntries[bufferId].Length;
        }

        /// <summary>
        /// Returns the ratio against the current target's max resolution
        /// </summary>
        /// <param name="width">width to utilize</param>
        /// <param name="height">height to utilize</param>
        /// <returns> retruns the width,height / maxTargetSize.xy ratio. </returns>
        public Vector2 CalculateRatioAgainstMaxSize(int width, int height)
        {
            return m_RTHandleSystem.CalculateRatioAgainstMaxSize(new Vector2Int(width, height));
        }

        void Swap()
        {
            foreach (var rtEntry in m_RTEntries)
            {
                // Do not index out of bounds...
                if (rtEntry.Value.Length > 1)
                {
                    var nextFirst = rtEntry.Value[rtEntry.Value.Length - 1];
                    for (int i = rtEntry.Value.Length - 1; i > 0; --i)
                        rtEntry.Value[i] = rtEntry.Value[i - 1];
                    rtEntry.Value[0] = nextFirst;

                    // First is autoresize, other are on demand
                    m_RTHandleSystem.SwitchResizeMode(rtEntry.Value[0].handle, RTHandleSystem.ResizeMode.Auto);
                    m_RTHandleSystem.SwitchResizeMode(rtEntry.Value[1].handle, RTHandleSystem.ResizeMode.OnDemand);
                }
                else
                {
                    m_RTHandleSystem.SwitchResizeMode(rtEntry.Value[0].handle, RTHandleSystem.ResizeMode.Auto);
                }
            }
        }

        void Dispose(bool disposing)
        {
            if (!m_DisposedValue)
            {
                if (disposing)
                {
                    ReleaseAll();
                    m_RTHandleSystem.Dispose();
                    m_RTHandleSystem = null;
                }

                m_DisposedValue = true;
            }
        }

        /// <summary>
        /// Dispose implementation
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Deallocate and clear all buffers.
        /// </summary>
        public void ReleaseAll()
        {
            foreach (var rtEntry in m_RTEntries)
            {
                for (int i = 0, c = rtEntry.Value.Length; i < c; ++i)
                {
                    m_RTHandleSystem.Release(rtEntry.Value[i].handle);
                }
            }
            m_RTEntries.Clear();
        }
    }
}
