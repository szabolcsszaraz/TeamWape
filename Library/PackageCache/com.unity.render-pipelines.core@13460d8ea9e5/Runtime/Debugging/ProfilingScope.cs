// When defined, ProfilingSampler creates ProfilerRecorder instances to expose per-frame CPU/GPU
// timing and sample-count properties (cpuElapsedTime, gpuElapsedTime, inlineCpuElapsedTime, …).
// Undefine to strip all recorder allocations from builds where profiling overhead is unacceptable.
#define UNITY_USE_RECORDER

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Profiling;
using Unity.Profiling.LowLevel;

namespace UnityEngine.Rendering
{
    class TProfilingSampler<TEnum> : ProfilingSampler where TEnum : Enum
    {
        internal static readonly Dictionary<TEnum, TProfilingSampler<TEnum>> samples = new Dictionary<TEnum, TProfilingSampler<TEnum>>();

        static TProfilingSampler()
        {
            var names  = Enum.GetNames(typeof(TEnum));
            var values = Enum.GetValues(typeof(TEnum));

            for (int i = 0; i < names.Length; i++)
            {
                var sample = new TProfilingSampler<TEnum>(names[i]);
                samples.Add((TEnum)values.GetValue(i), sample);
            }
        }

        public TProfilingSampler(string name)
            : base(name)
        {
        }
    }

    /// <summary>
    /// Wraps a ProfilerMarker and its associated ProfilerRecorder instances
    /// to provide per-frame CPU and GPU timing for a named profiling scope.
    /// Use together with <see cref="ProfilingScope"/> to bracket code you want to measure.
    /// </summary>
    /// <remarks>
    /// Timing properties (<see cref="cpuElapsedTime"/>, <see cref="gpuElapsedTime"/>,
    /// <see cref="inlineCpuElapsedTime"/>, and their sample-count counterparts) are only
    /// populated after <see cref="enableRecording"/> is set to <c>true</c> and at least one
    /// frame has elapsed.
    /// ProfilerRecorder instances are allocated lazily on the first <see cref="enableRecording"/> = <c>true</c> call;
    /// samplers that never enable recording hold no native resources.
    /// Recording support requires the <c>UNITY_USE_RECORDER</c> define (enabled by default);
    /// without it all timing properties return zero and no ProfilerRecorder is ever allocated.
    /// </remarks>
    [IgnoredByDeepProfiler]
    public class ProfilingSampler : IDisposable
    {
        /// <summary>
        /// Get the sampler for the corresponding enumeration value.
        /// </summary>
        /// <typeparam name="TEnum">Type of the enumeration.</typeparam>
        /// <param name="marker">Enumeration value.</param>
        /// <returns>The <see cref="ProfilingSampler"/> for the given enumeration value,
        /// or <c>null</c> in non-development Player builds.</returns>
        public static ProfilingSampler Get<TEnum>(TEnum marker)
            where TEnum : Enum
        {
#if ENABLE_PROFILER
            TProfilingSampler<TEnum>.samples.TryGetValue(marker, out var sampler);
            return sampler;
#else
            return null;
#endif
        }

        /// <summary>
        /// Creates a new <see cref="ProfilingSampler"/> under ProfilerCategory.Render
        /// with the specified MarkerFlags.
        /// </summary>
        /// <remarks>
        /// Registers the following ProfilerMarkers:
        /// <list type="bullet">
        /// <item><description>
        ///   <b><paramref name="name"/></b> — The primary marker. Appears in the Profiler under
        ///   ProfilerCategory.Render. Drives <see cref="cpuElapsedTime"/>, <see cref="cpuSampleCount"/>,
        ///   and (when GPU sampling is active) <see cref="gpuElapsedTime"/> and <see cref="gpuSampleCount"/>.
        ///   Use the primary marker name to measure CommandBuffer execution timings on the render thread.
        /// </description></item>
        /// <item><description>
        ///   <b>"Inl_<paramref name="name"/>"</b> — The inline marker. Also under ProfilerCategory.Render,
        ///   but without SampleGPU. Sampled directly by <see cref="Begin"/> and <see cref="End"/> on the
        ///   calling thread. Drives <see cref="inlineCpuElapsedTime"/> and <see cref="inlineCpuSampleCount"/>.
        /// </description></item>
        /// </list>
        /// When no verbosity bits (bits 10–12) are set in <paramref name="flags"/>,
        /// MarkerFlags.SampleGPU is added to the primary marker automatically so that GPU timing is
        /// captured by default for user-visible markers.
        /// Pass any <c>Verbosity*</c> flag (e.g. MarkerFlags.VerbosityAdvanced) to suppress GPU recording.
        /// ProfilerRecorder instances (which hold native handles) are not allocated in the constructor;
        /// they are created lazily the first time <see cref="enableRecording"/> is set to <c>true</c>.
        /// Samplers that never enable recording add no native-resource overhead.
        /// </remarks>
        /// <param name="name">Name of the profiling sampler.</param>
        /// <param name="flags">Verbosity flags (e.g. MarkerFlags.Default,
        /// MarkerFlags.VerbosityAdvanced).</param>
        /// <returns>A new <see cref="ProfilingSampler"/>, or <c>null</c> in non-development Player builds.</returns>
        public static ProfilingSampler Create(string name, MarkerFlags flags)
        {
#if ENABLE_PROFILER
            return new ProfilingSampler(name, flags);
#else
            return null;
#endif
        }

        /// <summary>
        /// Creates a <see cref="ProfilingSampler"/> with <see cref="MarkerFlags.Default"/> under
        /// ProfilerCategory.Render. MarkerFlags.SampleGPU is added
        /// automatically because no verbosity level is set.
        /// </summary>
        /// <param name="name">Name shown in the Unity Profiler for this marker.</param>
        public ProfilingSampler(string name)
            : this(name, MarkerFlags.Default)
        {
        }

        /// <summary>
        /// Creates a <see cref="ProfilingSampler"/> with the specified name and flags under ProfilerCategory.Render.
        /// Private to ensure we only create ProfilingSamplers with the Create() method,
        /// which handles the ENABLE_PROFILER conditional and ensures no overhead in non-development Players.
        /// </summary>
        /// <param name="name">Name shown in the Unity Profiler for this marker.</param>
        /// <param name="flags">Marker flags controlling verbosity and GPU sampling.</param>
        private ProfilingSampler(string name, MarkerFlags flags)
        {
#if ENABLE_PROFILER
#if UNITY_USE_RECORDER
            const MarkerFlags k_VerbosityMask = (MarkerFlags)0x1C00;
            var renderFlags = (flags & k_VerbosityMask) == 0 ? flags | MarkerFlags.SampleGPU : flags;
            m_SampleGpu = (renderFlags & MarkerFlags.SampleGPU) != 0;
#else
            var renderFlags = flags;
#endif
            m_Marker       = new ProfilerMarker(ProfilerCategory.Render, name, renderFlags);
            m_InlineMarker = new ProfilerMarker(ProfilerCategory.Render, $"Inl_{name}", flags);
#endif
            this.name = name;
            // ProfilerRecorder allocation deferred to first enableRecording = true.
        }

        /// <summary>
        /// Begins the profiling block. Records both a command-buffer marker (if
        /// <paramref name="cmd"/> is non-null) and an inline CPU marker.
        /// </summary>
        /// <param name="cmd">Command buffer to receive the GPU-visible marker.
        /// Pass <c>null</c> for CPU-only inline profiling.</param>
        [Conditional("ENABLE_PROFILER")]
        [MethodImpl(MethodImplOptions.AggressiveInlining | (MethodImplOptions)512)]
        public void Begin(CommandBuffer cmd)
        {
#if ENABLE_PROFILER
            cmd?.BeginSample(m_Marker);
            m_InlineMarker.Begin();
#endif
        }

        /// <summary>
        /// Begins the profiling block with a Unity Object context associated with the sample.
        /// Records both a command-buffer marker (if <paramref name="cmd"/> is non-null) and an
        /// inline CPU marker.
        /// </summary>
        /// <param name="cmd">Command buffer to receive the GPU-visible marker.
        /// Pass <c>null</c> for CPU-only inline profiling.</param>
        /// <param name="contextObject">Unity Object (e.g. Texture, Mesh, Material) to associate
        /// with this sample. The Profiler displays it in the sample hierarchy.</param>
        [Conditional("ENABLE_PROFILER")]
        [MethodImpl(MethodImplOptions.AggressiveInlining | (MethodImplOptions)512)]
        public void Begin(CommandBuffer cmd, Object contextObject)
        {
#if ENABLE_PROFILER
            cmd?.BeginSample(m_Marker, contextObject);
            m_InlineMarker.Begin(contextObject);
#endif
        }

        /// <summary>
        /// Ends the profiling block started by <see cref="Begin"/> call.
        /// </summary>
        /// <param name="cmd">The same command buffer passed to <see cref="Begin"/> call,
        /// or <c>null</c> if no command buffer was used.</param>
        [Conditional("ENABLE_PROFILER")]
        [MethodImpl(MethodImplOptions.AggressiveInlining | (MethodImplOptions)512)]
        public void End(CommandBuffer cmd)
        {
#if ENABLE_PROFILER
            cmd?.EndSample(m_Marker);
            m_InlineMarker.End();
#endif
        }

        internal bool IsValid()
        {
#if ENABLE_PROFILER
            return m_Marker.Handle != IntPtr.Zero;
#else
            return false;
#endif
        }

#if ENABLE_PROFILER
        internal ProfilerMarker m_Marker;
        internal ProfilerMarker m_InlineMarker;
#if UNITY_USE_RECORDER
        internal ProfilerRecorder m_Recorder;
        internal ProfilerRecorder m_GpuRecorder;
        internal ProfilerRecorder m_InlineRecorder;
        readonly bool m_SampleGpu;
#endif
#endif
        /// <summary>
        /// Name of this sampler as it appears in the Unity Profiler.
        /// </summary>
        public string name { get; private set; }

        /// <summary>
        /// Enables or disables ProfilerRecorder data collection for this sampler.
        /// </summary>
        /// <remarks>
        /// Setting to <c>true</c> for the first time allocates the underlying ProfilerRecorder instances
        /// and starts collection; timing properties reflect the <em>previous</em> frame's data after
        /// at least one frame has elapsed.
        /// Setting back to <c>false</c> stops collection but keeps the recorders allocated for reuse.
        /// Has no effect when <c>UNITY_USE_RECORDER</c> is not defined.
        /// </remarks>
        public bool enableRecording
        {
            set
            {
#if UNITY_USE_RECORDER && ENABLE_PROFILER
                if (value)
                {
                    const ProfilerRecorderOptions recorderDefaultOptions =
                        ProfilerRecorderOptions.WrapAroundWhenCapacityReached |
                        ProfilerRecorderOptions.SumAllSamplesInFrame |
                        // TODO: Shared recorder flag. Remove once we transition fully from Enums to static ProfilerSamplers.
                        // Allows to reuse the same underlying recorder for multiple ProfilingSampler instances with the same marker,
                        // which is important to avoid disruptions in Graphics Performance Tests during transition.
                        (ProfilerRecorderOptions)(1 << 7);
                    if (!m_Recorder.Valid)
                    {
                        m_Recorder = new ProfilerRecorder(m_Marker, 1, recorderDefaultOptions);
                        if (m_SampleGpu)
                            m_GpuRecorder = new ProfilerRecorder(m_Marker, 1, recorderDefaultOptions | ProfilerRecorderOptions.GpuRecorder);
                        m_InlineRecorder = new ProfilerRecorder(m_InlineMarker, 1, recorderDefaultOptions);
                    }
                    m_Recorder.Start();
                    if (m_GpuRecorder.Valid)
                        m_GpuRecorder.Start();
                    m_InlineRecorder.Start();
                }
                else
                {
                    if (m_Recorder.Valid)
                        m_Recorder.Stop();
                    if (m_GpuRecorder.Valid)
                        m_GpuRecorder.Stop();
                    if (m_InlineRecorder.Valid)
                        m_InlineRecorder.Stop();
                }
#endif
            }
        }

        /// <summary>
        /// Releases the native ProfilerRecorder resources held by this sampler.
        /// This is a no-op if <see cref="enableRecording"/> was never set to <c>true</c>.
        /// </summary>
        public void Dispose()
        {
#if ENABLE_PROFILER
            Dispose(true);
            GC.SuppressFinalize(this);
#endif            
        }

#if ENABLE_PROFILER
        /// <summary>Finalizer — releases recorders when no explicit <see cref="Dispose"/> call was made.</summary>
        ~ProfilingSampler() => Dispose(false);
#endif
        void Dispose(bool disposing)
        {
#if UNITY_USE_RECORDER && ENABLE_PROFILER
            if (m_Recorder.Valid)
                m_Recorder.Dispose();
            if (m_GpuRecorder.Valid)
                m_GpuRecorder.Dispose();
            if (m_InlineRecorder.Valid)
                m_InlineRecorder.Dispose();
#endif
        }

#if UNITY_USE_RECORDER && ENABLE_PROFILER
        const float k_NanosecondsToMilliseconds = 1.0f / 1000000.0f;

        /// <summary>
        /// GPU elapsed time in milliseconds for the previous frame.
        /// </summary>
        /// <remarks>
        /// Only non-zero for user-visible markers (those created without verbosity flags), because
        /// MarkerFlags.SampleGPU is only added automatically in that case.
        /// Always returns <c>0</c> for markers created with a <c>Verbosity*</c> flag.
        /// </remarks>
        public float gpuElapsedTime => (m_GpuRecorder.Valid && m_GpuRecorder.IsRunning) ? m_GpuRecorder.LastValue * k_NanosecondsToMilliseconds : 0.0f;
        /// <summary>
        /// Number of times this sampler was hit on the GPU in the previous frame.
        /// </summary>
        /// <remarks>Subject to the same MarkerFlags.SampleGPU condition as <see cref="gpuElapsedTime"/>.</remarks>
        public int gpuSampleCount => (m_GpuRecorder.Valid && m_GpuRecorder.IsRunning) ? (int)m_GpuRecorder.GetSample(0).Count : 0;
        /// <summary>
        /// CPU elapsed time in milliseconds for command-buffer execution in the previous frame.
        /// </summary>
        /// <remarks>
        /// Measures time spent replaying the command buffer on the CPU render thread.
        /// For inline (non-command-buffer) CPU time use <see cref="inlineCpuElapsedTime"/>.
        /// </remarks>
        public float cpuElapsedTime => (m_Recorder.Valid && m_Recorder.IsRunning) ? m_Recorder.LastValue * k_NanosecondsToMilliseconds : 0.0f;
        /// <summary>
        /// Number of times this sampler was hit via a command buffer on the CPU in the previous frame.
        /// </summary>
        public int cpuSampleCount => (m_Recorder.Valid && m_Recorder.IsRunning) ? (int)m_Recorder.GetSample(0).Count : 0;
        /// <summary>
        /// CPU elapsed time in milliseconds for direct (inline) <see cref="Begin"/>/<see cref="End"/>
        /// calls in the previous frame.
        /// </summary>
        /// <remarks>
        /// Reflects time recorded by <see cref="Begin"/> and <see cref="End"/> called directly on the
        /// calling thread, not via a command buffer. For command-buffer time use <see cref="cpuElapsedTime"/>.
        /// </remarks>
        public float inlineCpuElapsedTime => (m_InlineRecorder.Valid && m_InlineRecorder.IsRunning) ? m_InlineRecorder.LastValue * k_NanosecondsToMilliseconds : 0.0f;
        /// <summary>
        /// Number of times this sampler was hit via direct inline calls in the previous frame.
        /// </summary>
        public int inlineCpuSampleCount => (m_InlineRecorder.Valid && m_InlineRecorder.IsRunning) ? (int)m_InlineRecorder.GetSample(0).Count : 0;
#else
        /// <summary>GPU Elapsed time in milliseconds.</summary>
        public float gpuElapsedTime => 0.0f;
        /// <summary>Number of times the Profiling Sampler has hit on the GPU</summary>
        public int gpuSampleCount => 0;
        /// <summary>CPU Elapsed time in milliseconds (Command Buffer execution).</summary>
        public float cpuElapsedTime => 0.0f;
        /// <summary>Number of times the Profiling Sampler has hit on the CPU in the command buffer.</summary>
        public int cpuSampleCount => 0;
        /// <summary>CPU Elapsed time in milliseconds (Direct execution).</summary>
        public float inlineCpuElapsedTime => 0.0f;
        /// <summary>Number of times the Profiling Sampler has hit on the CPU.</summary>
        public int inlineCpuSampleCount => 0;
#endif
        // Keep the constructor private
        ProfilingSampler() { }
    }

    /// <summary>
    /// RAII scope that calls <see cref="ProfilingSampler.Begin"/> on construction and
    /// <see cref="ProfilingSampler.End"/> on disposal, ensuring markers are always balanced.
    /// Use in a <c>using</c> statement to guarantee <see cref="Dispose"/> is called.
    /// </summary>
    /// <remarks>This struct is a no-op in non-development Players.</remarks>
    [IgnoredByDeepProfiler]
    public struct ProfilingScope : IDisposable
    {
#if ENABLE_PROFILER
        ProfilingSampler    m_Sampler;
        CommandBuffer       m_Cmd;
#endif

        /// <summary>
        /// Creates a profiling scope without a command buffer (inline CPU profiling only).
        /// </summary>
        /// <param name="sampler">The sampler that provides the underlying marker.
        /// May be <c>null</c>; the scope is a no-op in that case.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining | (MethodImplOptions)512)]
        public ProfilingScope(ProfilingSampler sampler)
            : this((CommandBuffer)null, sampler)
        {
        }

        /// <summary>
        /// Creates an inline CPU-only profiling scope with a Unity Object context associated
        /// with the sample. No command buffer is involved; the marker is emitted directly
        /// on the CPU timeline.
        /// </summary>
        /// <param name="sampler">The sampler that provides the underlying marker.
        /// May be <c>null</c>; the scope is a no-op in that case.</param>
        /// <param name="contextObject">Unity Object (e.g. Texture, Mesh, Material) to associate
        /// with this sample. The Profiler displays it in the sample hierarchy.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining | (MethodImplOptions)512)]
        public ProfilingScope(ProfilingSampler sampler, Object contextObject)
            : this((CommandBuffer)null, sampler, contextObject)
        {
        }

        /// <summary>
        /// Creates a profiling scope that records markers into <paramref name="cmd"/> as well as inline on the CPU.
        /// </summary>
        /// <remarks>
        /// Do not use with a named <see cref="CommandBuffer"/>. A named command buffer inserts its own
        /// scope marker on execution, which orphans the markers added here: the begin and end appear
        /// inside different named-buffer execution brackets and will be mismatched in the Profiler timeline.
        /// </remarks>
        /// <param name="cmd">Command buffer to receive the GPU-visible begin/end markers.</param>
        /// <param name="sampler">The sampler that provides the underlying marker.
        /// May be <c>null</c>; the scope is a no-op in that case.</param>
        public ProfilingScope(CommandBuffer cmd, ProfilingSampler sampler)
        {
#if ENABLE_PROFILER
            sampler?.Begin(cmd);
            m_Sampler = sampler;
            m_Cmd = cmd;
#endif
        }

        /// <summary>
        /// Creates a profiling scope that records markers into <paramref name="cmd"/> with a
        /// Unity Object context associated with the sample.
        /// </summary>
        /// <remarks>
        /// Do not use with a named <see cref="CommandBuffer"/>. A named command buffer inserts
        /// its own scope marker on execution, which orphans the markers added here.
        /// </remarks>
        /// <param name="cmd">Command buffer to receive the GPU-visible begin/end markers.</param>
        /// <param name="sampler">The sampler that provides the underlying marker.
        /// May be <c>null</c>; the scope is a no-op in that case.</param>
        /// <param name="contextObject">Unity Object (e.g. Texture, Mesh, Material) to associate
        /// with this sample. The Profiler displays it in the sample hierarchy.</param>
        public ProfilingScope(CommandBuffer cmd, ProfilingSampler sampler, Object contextObject)
        {
#if ENABLE_PROFILER
            sampler?.Begin(cmd, contextObject);
            m_Sampler = sampler;
            m_Cmd = cmd;
#endif
        }

        /// <summary>
        /// Creates a profiling scope that records markers into <paramref name="cmd"/> as well as inline on the CPU.
        /// </summary>
        /// <remarks>
        /// Do not use with a named <see cref="CommandBuffer"/>. A named command buffer inserts its own
        /// scope marker on execution, which orphans the markers added here: the begin and end appear
        /// inside different named-buffer execution brackets and will be mismatched in the Profiler timeline.
        /// </remarks>
        /// <param name="cmd">Command buffer to receive the GPU-visible begin/end markers.</param>
        /// <param name="sampler">The sampler that provides the underlying marker.
        /// May be <c>null</c>; the scope is a no-op in that case.</param>
        public ProfilingScope(BaseCommandBuffer cmd, ProfilingSampler sampler)
        {
#if ENABLE_PROFILER
            sampler?.Begin(cmd.m_WrappedCommandBuffer);
            m_Sampler = sampler;
            m_Cmd = cmd.m_WrappedCommandBuffer;
#endif
        }

        /// <summary>
        /// Creates a profiling scope that records markers into <paramref name="cmd"/> as well as inline on the CPU,
        /// and associates a Unity Object with the sample for identification in the Profiler.
        /// </summary>
        /// <remarks>
        /// Do not use with a named <see cref="CommandBuffer"/>. A named command buffer inserts its own
        /// scope marker on execution, which orphans the markers added here: the begin and end appear
        /// inside different named-buffer execution brackets and will be mismatched in the Profiler timeline.
        /// </remarks>
        /// <param name="cmd">BaseCommandBuffer (e.g. RasterCommandBuffer, UnsafeCommandBuffer) to receive the GPU-visible begin/end markers.</param>
        /// <param name="sampler">The sampler that provides the underlying marker.
        /// May be <c>null</c>; the scope is a no-op in that case.</param>
        /// <param name="contextObject">Unity Object (e.g. Texture, Mesh, Material) to associate
        /// with this sample. The Profiler displays it in the sample hierarchy.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining | (MethodImplOptions)512)]
        public ProfilingScope(BaseCommandBuffer cmd, ProfilingSampler sampler, Object contextObject)
            : this(cmd.m_WrappedCommandBuffer, sampler, contextObject)
        {
        }

        /// <summary>
        /// Ends the profiling scope by calling <see cref="ProfilingSampler.End"/>. Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
#if ENABLE_PROFILER
            if (m_Sampler == null)
                return;

            m_Sampler.End(m_Cmd);
            m_Sampler = null;
#endif
        }
    }
}
