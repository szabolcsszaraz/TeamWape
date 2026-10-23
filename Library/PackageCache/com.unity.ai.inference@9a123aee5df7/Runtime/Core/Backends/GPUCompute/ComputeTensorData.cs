using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using Unity.Collections;
using System.Collections.Generic;

namespace Unity.InferenceEngine
{
    /// <summary>
    /// An interface that provides methods for converting custom tensor data to `ComputeTensorData`.
    /// </summary>
    interface IConvertibleToComputeTensorData
    {
        /// <summary>
        /// Implement this method to convert to `ComputeTensorData`.
        /// </summary>
        /// <param name="dstCount">The number of elements.</param>
        /// <returns>Converted `ComputeTensorData`.</returns>
        ComputeTensorData ConvertToComputeTensorData(int dstCount);
    }

    /// <summary>
    /// This deals with asynchronous management of the disposition of compute buffers:
    /// </summary>
    internal class ComputeTensorDataReaper
    {
        // Global async pending dispose queue: to be used by Tensor.AdoptTensorData when disposing compute buffers
        // that might still be used later eg a yet-to-be-dispatched command buffer may hold references to it
        static List<ComputeTensorData> m_DisposeQueue = new List<ComputeTensorData>();
        internal static void AddToDisposeQueue(ComputeTensorData ctd) => m_DisposeQueue.Add(ctd);
        internal static bool IsDisposeQueueEmpty => (m_DisposeQueue.Count == 0);
        internal static bool IsAsyncCallbackCommandBufferEmpty => m_AsyncCallbackCommandBufferEmpty;

        // Simple async GPU event mechanism: request a small dummy readback for the ComputeTensorData
        static readonly int s_DummySize = 16;
        static ComputeBuffer m_DummyDestination = new ComputeBuffer(s_DummySize, sizeof(float));
        static CommandBuffer m_AsyncCallbackCommandBuffer = new CommandBuffer();
        static bool m_AsyncCallbackCommandBufferEmpty = true;

        static void ReInitStaticResources()
        {
            CleanupStaticResources();
            m_DummyDestination = new ComputeBuffer(s_DummySize, sizeof(float));
            m_AsyncCallbackCommandBuffer = new CommandBuffer();
            m_AsyncCallbackCommandBufferEmpty = true;
            m_DisposeQueue?.Clear();
            m_DisposeQueue = m_DisposeQueue ?? new List<ComputeTensorData>();
        }

        static void CleanupStaticResources()
        {
            m_DummyDestination?.Release();
            m_DummyDestination = null;
            m_AsyncCallbackCommandBuffer?.Release();
            m_AsyncCallbackCommandBuffer = null;
            m_AsyncCallbackCommandBufferEmpty = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterCleanup()
        {
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += CleanupStaticResources;
            // Just in case domain reload is disabled in project settings: in that case,
            // static initializers are not executed again and CleanupStaticResources()
            // will not be executed through UnityEditor.AssemblyReloadEvents.beforeAssemblyReload as it won't fire.
            ReInitStaticResources();
            // Application.quitting is for the player part when running in the editor,
            // so when quitting the editor while in playmode, the following is required to cleanup too:
            UnityEditor.EditorApplication.quitting += CleanupStaticResources;
#endif
            Application.quitting += CleanupStaticResources;
        }

        // Manual dispatch of the async disposition queue
        public static void ExecuteAsyncDisposeCommandBufferAndClear()
        {
            Graphics.ExecuteCommandBuffer(m_AsyncCallbackCommandBuffer);
            m_AsyncCallbackCommandBuffer.Clear();
            m_AsyncCallbackCommandBufferEmpty = true;
        }

        // Synchronous disposition of the dispose queue
        public static void ProcessDisposeQueue()
        {
            foreach (var ctd in m_DisposeQueue)
            {
                ctd.Dispose();
            }
            m_DisposeQueue.Clear();
        }

        // Queue to an external commandbuffer the async disposition events
        public static void MoveDisposeQueueAsAsyncGPUEvents(CommandBuffer cb)
        {
            foreach (var ctd in m_DisposeQueue)
            {
                ctd.DisposeAfterDispatchInitiation(cb);
            }
            m_DisposeQueue.Clear();
        }

        // Queue to command buffer the deferred disposition of the `ComputeTensorData` and any associated memory using the async readback mechanism as a GPU event.
        // This should only be called by ComputeTensorData.DisposeAfterDispatch
        internal static void DisposeAfterDispatch(CommandBuffer cb, ComputeTensorData ctd)
        {
            {
                if (cb == null)
                    cb = m_AsyncCallbackCommandBuffer;

                if (cb == m_AsyncCallbackCommandBuffer)
                    m_AsyncCallbackCommandBufferEmpty = false;

                var fn = ComputeFunctions.k_MemCopy;
                var numWords = ComputeHelper.IDivC(System.Math.Min(s_DummySize, ctd.buffer.count), 4);
                var wordsHeight = 1;
                var wordsWidth = numWords;
                cb.SetComputeIntParam(fn.shader, ShaderPropertyID.k_ID_offsetO, 0);
                cb.SetComputeIntParam(fn.shader, ShaderPropertyID.k_ID_offsetX, 0);
                cb.SetComputeIntParam(fn.shader, ShaderPropertyID.k_ID_count, s_DummySize);
                cb.SetComputeIntParam(fn.shader, ShaderPropertyID.k_ID_O_width, numWords * 4);
                cb.SetComputeBufferParam(fn.shader, fn.kernelIndex, ShaderPropertyID.k_ID_Xptr, ctd.buffer);
                cb.SetComputeBufferParam(fn.shader, fn.kernelIndex, ShaderPropertyID.k_ID_Optr, m_DummyDestination);
                cb.Dispatch(fn, wordsWidth, wordsHeight, 1);
            }
            cb.RequestAsyncReadback(m_DummyDestination, (request) =>
            {
                if (request.hasError)
                    D.LogWarning("DisposeAfterDispatch AsyncGPUReadback callback: request has error.");
                ctd.DisposeAsyncCompletion();
            });
        }
    }

    /// <summary>
    /// Represents data storage for a `Tensor` as a compute buffer, for GPUCompute backend.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom("Unity.Sentis")]
    public class ComputeTensorData : ITensorData, IConvertibleToCPUTensorData
    {
        // Acquire/Release barrier flag for the progress of the async disposition (completion callback can happen on a different thread)
        // Only the main thread can set it to true, only the async completion thread can set it to false.
        volatile bool m_DelayedDisposeInProgress = false;

        bool m_IsDisposed;
        ComputeBuffer m_Buffer;
        int m_Count;

        /// <inheritdoc/>
        public int maxCapacity => m_Count;

        /// <inheritdoc/>
        public BackendType backendType => BackendType.GPUCompute;

        /// <summary>
        /// The data storage as a compute buffer.
        /// </summary>
        public ComputeBuffer buffer => m_Buffer;

        /// <summary>
        /// Initializes and returns an instance of `ComputeTensorData`, and allocates storage for a tensor with the shape of `shape`.
        /// </summary>
        /// <param name="count">The number of elements.</param>
        /// <param name="clearOnInit">Whether to zero the data on allocation. The default value is `false`.</param>
        public ComputeTensorData(int count, bool clearOnInit = false)
        {
            m_DelayedDisposeInProgress = false;

            m_Count = count;
            m_IsDisposed = false;

            if (m_Count == 0)
                return;

            ProfilerMarkers.ComputeTensorDataNewEmpty.Begin();
            m_Buffer = new ComputeBuffer(count, sizeof(float));

            // @TODO: consider zero initialization only for "debug" mode
            if (clearOnInit)
            {
                var empty = new NativeArray<float>(count, Allocator.Temp, NativeArrayOptions.ClearMemory);
                m_Buffer.SetData(empty);
                empty.Dispose();
            }

            ProfilerMarkers.ComputeTensorDataNewEmpty.End();
        }

        /// <summary>
        /// Finalizes the `ComputeTensorData`.
        /// </summary>
        ~ComputeTensorData()
        {
            if (m_Buffer == null)
                return;
            if (m_IsDisposed)
                return;

            D.LogWarning($"Found unreferenced, but undisposed ComputeTensorData which might lead to GPU resource leak");
        }

        /// <summary>
        /// Disposes of the `ComputeTensorData` and any associated memory.
        /// </summary>
        public void Dispose()
        {
            if (m_DelayedDisposeInProgress)
            {
                D.LogWarning($"ComputeTensorData.Dispose called while m_DelayedDisposeInProgress already true, ignoring.");
                return;
            }
            if (!TensorDataHelper.OnMainThread)
            {
                D.LogWarning($"ComputeTensorData.Dispose called outside main thread, ignoring.");
                return;
            }

            if (!m_IsDisposed)
            {
                m_Buffer?.Dispose();
                m_Buffer = null;
            }
            m_IsDisposed = true;
            System.GC.SuppressFinalize(this);
        }

        // This should only be called by the thread running the AsyncGPUReadback callback.
        // The AsyncGPUReadback is launched and the callback lambda specified by ComputeTensorDataReaper.DisposeAfterDispatch
        internal void DisposeAsyncCompletion()
        {
            // Note: the context of the callback is not necessarily the main thread but ComputeBuffer.Release() should be thread-safe:
            if (m_Buffer != null)
            {
                //D.Log($"DisposeAsyncCompletion AsyncGPUReadback releasing for {m_Buffer?.GetNativeBufferPtr()}");
                m_Buffer.Release();
                m_Buffer = null;
                m_IsDisposed = true;
            }
            else
            {
                D.LogWarning("DisposeAsyncCompletion AsyncGPUReadback callback: m_Buffer is already null?!");
            }
            m_DelayedDisposeInProgress = false; // var is volatile, so this has release semantics
        }

        // This should only be called by ComputeTensorDataReaper.DisposeAfterDispatch
        // Queue to command buffer the deferred disposition of the `ComputeTensorData` and any associated memory using the async readback mechanism as a GPU event.
        // Called by the ComputeTensorDataReaper to allow the ComputeTensorData to flag the "in progress" barrier before.
        internal void DisposeAfterDispatchInitiation(CommandBuffer cb)
        {
            if (m_IsDisposed || (m_Buffer == null))
                return;

            if (m_DelayedDisposeInProgress)
            {
                D.LogWarning($"DisposeAfterDispatchInitiation called on ComputeTensorData while m_DelayedDisposeInProgress already true");
                return;
            }
            m_DelayedDisposeInProgress = true; // var is volatile, so this has acquire semantics

            ComputeTensorDataReaper.DisposeAfterDispatch(cb, this);
        }

        /// <summary>
        /// Deferred disposition of the `ComputeTensorData` and any associated memory using the async readback mechanism as a GPU event.
        /// Called by Tensor.AdoptTensorData.
        /// </summary>
        internal void DelayedDispose()
        {
            ComputeTensorDataReaper.AddToDisposeQueue(this);
        }

        /// <inheritdoc/>
        public void Upload<T>(NativeArray<T> data, int srcCount) where T : unmanaged
        {
            var numItemToCopy = srcCount;
            var numItemAvailableInData = data.Length;

            Assert.IsTrue(numItemToCopy <= numItemAvailableInData);
            m_Buffer.SetData(data, 0, 0, numItemToCopy);

            m_AsyncDownloadRequested = false;
        }

        bool m_AsyncDownloadRequested = false;
        AsyncGPUReadbackRequest m_AsyncDownloadRequest;

        /// <inheritdoc/>
        public bool IsReadbackRequestDone()
        {
            return m_AsyncDownloadRequest.done;
        }

        /// <inheritdoc/>
        public void ReadbackRequest()
        {
            if (m_Count == 0)
                return;
            m_AsyncDownloadRequest = AsyncGPUReadback.Request(m_Buffer, m_Buffer.count * sizeof(float), 0 * sizeof(float));
            m_AsyncDownloadRequested = true;
        }

#if UNITY_2023_2_OR_NEWER
        /// <inheritdoc/>
        public async Awaitable<NativeArray<T>> DownloadAsync<T>(int dstCount) where T : unmanaged
        {
            if (dstCount == 0)
                return new NativeArray<T>();

            int count;
            unsafe
            {
                count = ((dstCount * sizeof(T) + sizeof(int) - 1) / sizeof(int));
            }

            var request = await AsyncGPUReadback.RequestAsync(m_Buffer, count * sizeof(int), 0);
            return request.GetData<int>().Reinterpret<T>(sizeof(int)).GetSubArray(0, dstCount);
        }
#endif

        /// <inheritdoc/>
        public CPUTensorData ConvertToCPUTensorData(int dstCount)
        {
            CPUTensorData output = new CPUTensorData(dstCount);
            if (dstCount == 0)
                return output;

            ProfilerMarkers.ComputeTensorDataDownload.Begin();

            var array = output.array.GetNativeArrayHandle<int>();

            if (m_AsyncDownloadRequested)
            {
                m_AsyncDownloadRequested = false;
                if (!m_AsyncDownloadRequest.done)
                    m_AsyncDownloadRequest.WaitForCompletion();

                var reqData = m_AsyncDownloadRequest.GetData<int>();
                ProfilerMarkers.ComputeTensorDataDownload.End();
                NativeArray<int>.Copy(reqData, 0, array, 0, dstCount);
                return output;
            }

            m_AsyncDownloadRequest = AsyncGPUReadback.RequestIntoNativeArray<int>(ref array, m_Buffer, dstCount * sizeof(int), 0);
            m_AsyncDownloadRequest.WaitForCompletion();
            ProfilerMarkers.ComputeTensorDataDownload.End();
            return output;
        }

        /// <inheritdoc/>
        public NativeArray<T> Download<T>(int dstCount) where T : unmanaged
        {
            if (dstCount == 0)
                return new NativeArray<T>();

            ProfilerMarkers.ComputeTensorDataDownload.Begin();

            if (m_AsyncDownloadRequested)
            {
                m_AsyncDownloadRequested = false;
                if (!m_AsyncDownloadRequest.done)
                    m_AsyncDownloadRequest.WaitForCompletion();

                var reqData = m_AsyncDownloadRequest.GetData<T>();
                ProfilerMarkers.ComputeTensorDataDownload.End();
                return reqData;
            }

            unsafe
            {
                int count = ((dstCount * sizeof(T) + sizeof(int) - 1) / sizeof(int));
                m_AsyncDownloadRequest = AsyncGPUReadback.Request(m_Buffer, count * sizeof(int), 0);
            }
            m_AsyncDownloadRequest.WaitForCompletion();

            var data = m_AsyncDownloadRequest.GetData<int>();

            ProfilerMarkers.ComputeTensorDataDownload.End();

            return data.Reinterpret<T>(sizeof(int)).GetSubArray(0, dstCount);
        }

        /// <inheritdoc/>
        public void CompleteAllPendingOperations()
        {
            if (m_AsyncDownloadRequested)
            {
                if (!m_AsyncDownloadRequest.done)
                    m_AsyncDownloadRequest.WaitForCompletion();
                return;
            }

            m_AsyncDownloadRequest = AsyncGPUReadback.Request(m_Buffer, m_Buffer.count * sizeof(float), 0 * sizeof(float));
            m_AsyncDownloadRequest.WaitForCompletion();
            m_AsyncDownloadRequested = false;
        }

        /// <summary>
        /// Returns a string that represents the `ComputeTensorData`.
        /// </summary>
        /// <returns>The string summary of the `ComputeTensorData`.</returns>
        public override string ToString()
        {
            return string.Format("GPU<ComputeTensorData>:{0} buffer: {1}", m_Count, m_Buffer);
        }

        /// <summary>
        /// Moves the tensor into GPU memory on the GPUCompute backend device.
        /// </summary>
        /// <param name="X">The tensor to move to the compute backend.</param>
        /// <param name="clearOnInit">Whether to zero the data on pinning. The default value is `false`.</param>
        /// <returns>The pinned `ComputeTensorData`.</returns>
        public static ComputeTensorData Pin(Tensor X, bool clearOnInit = false)
        {
            var onDevice = X.dataOnBackend;
            if (onDevice == null)
            {
                X.AdoptTensorData(new ComputeTensorData(X.count, clearOnInit), disposePrevious: true, disposeIsDelayed: false);
                return X.dataOnBackend as ComputeTensorData;
            }

            if (onDevice is ComputeTensorData)
                return onDevice as ComputeTensorData;
            ComputeTensorData dataOnBackend;
            if (onDevice is IConvertibleToComputeTensorData asConvertible)
            {
                dataOnBackend = asConvertible.ConvertToComputeTensorData(X.count);
            }
            else
            {
                dataOnBackend = new ComputeTensorData(X.count, clearOnInit: false);
                dataOnBackend.Upload<int>(onDevice.Download<int>(X.count), X.count);
            }
            X.AdoptTensorData(dataOnBackend, disposePrevious: true, disposeIsDelayed: false);

            return X.dataOnBackend as ComputeTensorData;
        }
    }
}
