using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

namespace Unity.InferenceEngine
{
    internal class TensorDataHelper
    {
        internal static bool OnMainThread => (System.Threading.Thread.CurrentThread.ManagedThreadId == s_MainThreadId);

        static int s_MainThreadId = 1;

        /// <summary>
        /// Capture the correct main thread ID for Unity Runtime
        /// RuntimeInitializeOnLoadMethod is guaranteed to run on the main thread during Unity runtime initialization
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetMainThread()
        {
            s_MainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Capture the correct main thread ID for Unity Editor
        /// is guaranteed to run on the main thread during Unity Editor initialization
        /// </summary>
        [UnityEditor.InitializeOnLoadMethod]
        static void SetMainThread()
        {
            s_MainThreadId = Thread.CurrentThread.ManagedThreadId;
        }
#endif
    }

    /// <summary>
    /// An interface that provides methods for converting custom tensor data to <see cref="CPUTensorData"/>.
    /// </summary>
    interface IConvertibleToCPUTensorData
    {
        /// <summary>
        /// Implement this method to convert to <see cref="CPUTensorData"/>.
        /// </summary>
        /// <param name="dstCount">The number of elements.</param>
        /// <returns>Converted <see cref="CPUTensorData"/>.</returns>
        CPUTensorData ConvertToCPUTensorData(int dstCount);
    }

    /// <summary>
    /// An interface that provides Job system dependency fences for the memory resource.
    /// </summary>
    interface IDependableMemoryResource
    {
        /// <summary>
        /// A read fence job handle. You can use <see cref="fence"/> as a <c>dependsOn</c> argument when you schedule a job that reads data. The job will start when the tensor data is ready for read access.
        /// </summary>
        Unity.Jobs.JobHandle fence { get; set; }
        /// <summary>
        /// A write fence job handle. You can use <see cref="reuse"/> as a <c>dependsOn</c> argument when you schedule a job that reads data. The job will start when the tensor data is ready for write access.
        /// </summary>
        Unity.Jobs.JobHandle reuse { get; set; }
        /// <summary>
        /// The raw memory pointer for the resource.
        /// </summary>
        unsafe void* rawPtr { get; }
    }

    /// <summary>
    /// Represents Burst-specific internal data storage for a <see cref="Tensor"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="CPUTensorData"/> stores tensor elements in native memory on the CPU, compatible with the Burst compiler and Unity's [Job system](xref:um-job-system-overview).
    /// Use it when you need direct access to tensor data for custom CPU operations, or when running inference on the CPU backend.
    ///
    /// Access the underlying buffer via <see cref="array"/>, which returns a <see cref="NativeTensorArray"/>. Use <see cref="Pin"/> to ensure a tensor's data resides on CPU before scheduling jobs that read or write it. The <see cref="fence"/> and <see cref="reuse"/> properties provide Job system dependency handles for synchronization.
    ///
    /// Call <see cref="Dispose"/> when finished to release native memory. Dispose must be called from the main thread; do not call from a finalizer.
    ///
    /// The Sentis package provides provides a complete sample that uses Burst to write data to a tensor in the Job system. To learn more, refer to [Samples](xref:sentis-package-samples).
    ///
    /// **Additional resources**
    ///
    /// - <see cref="NativeTensorArray"/>
    /// - <see cref="Tensor"/>
    /// - <see cref="ComputeTensorData"/>
    /// - <see cref="ITensorData"/>
    /// </remarks>
    /// <example>
    /// <code lang="cs"><![CDATA[
    /// // Pin a tensor to CPU and write data via a Burst job.
    /// var cpuData = CPUTensorData.Pin(inputTensor);
    /// var job = new MyJob { data = cpuData.array.GetNativeArrayHandle<float>() };
    /// cpuData.fence = job.Schedule(inputTensor.shape.length, 64);
    /// worker.Schedule(inputTensor);
    /// 
    /// // Define the job struct (used in examples below)
    /// [BurstCompile]
    /// struct MyJob : IJobParallelFor
    /// {
    ///     [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction]
    ///     public NativeArray<float> data;
    ///     public void Execute(int i)
    ///     {
    ///         data[i] = 3.14f;
    ///     }
    /// }
    /// ]]></code>
    /// </example>
    [UnityEngine.Scripting.APIUpdating.MovedFrom("Unity.Sentis")]
    public class CPUTensorData : ITensorData, IDependableMemoryResource, IConvertibleToComputeTensorData
    {
        bool m_IsDisposed;
        JobHandle m_ReadFence;
        JobHandle m_WriteFence;
        NativeTensorArray m_Array;
        int m_Count;
        bool m_SafeToDispose = true;

        /// <inheritdoc/>
        public BackendType backendType => BackendType.CPU;
        /// <inheritdoc/>
        public int maxCapacity => m_Count;
        /// <summary>
        /// The underlying <see cref="NativeTensorArray"/> containing the tensor data.
        /// </summary>
        /// <remarks>
        /// Use <see cref="NativeTensorArray.GetNativeArrayHandle{T}"/> to obtain a <c>NativeArray&lt;T&gt;</c> for use with Unity Jobs. The buffer is shared with the tensor. Do not dispose of it separately.
        /// </remarks>
        public NativeTensorArray array => m_Array;

        /// <inheritdoc/>
        public JobHandle fence { get { return m_ReadFence; } set { m_ReadFence = value; m_WriteFence = value; m_SafeToDispose = false; } }
        /// <inheritdoc/>
        public JobHandle reuse { get { return m_WriteFence; } set { m_WriteFence = JobHandle.CombineDependencies(value, m_WriteFence); m_SafeToDispose = false; } }

        /// <inheritdoc/>
        public unsafe void* rawPtr => m_Array.AddressAt<float>(0);

        /// <summary>
        /// Allocates a new <see cref="CPUTensorData"/> with storage for the specified number of elements.
        /// </summary>
        /// <remarks>
        /// Use this constructor when creating tensor data from scratch. Set <paramref name="clearOnInit"/> to <c>true</c> to zero-initialize the buffer. For tensors backed by existing data, use the <see cref="CPUTensorData(NativeTensorArray)"/> overload.
        /// </remarks>
        /// <param name="count">The number of elements to allocate.</param>
        /// <param name="clearOnInit">Whether to zero the data on allocation. The default value is <c>false</c>.</param>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var data = new CPUTensorData(1024, clearOnInit: true);
        /// // data.array contains 1024 zero-initialized floats
        /// ]]></code>
        /// </example>
        public CPUTensorData(int count, bool clearOnInit = false)
        {
            m_IsDisposed = false;
            m_Count = count;
            if (m_Count == 0)
                return;
            m_Array = new NativeTensorArray(m_Count, clearOnInit);
        }

        /// <summary>
        /// Wraps an existing <see cref="NativeTensorArray"/> as <see cref="CPUTensorData"/>.
        /// </summary>
        /// <remarks>
        /// Use this constructor when you have pre-allocated tensor data. The <see cref="CPUTensorData"/> takes ownership of the array. Do not dispose of it separately. Pass <c>null</c> to create an empty instance.
        /// </remarks>
        /// <param name="data">The tensor data to wrap, or <c>null</c> for an empty instance.</param>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var nativeArray = new NativeTensorArray(256);
        /// var cpuData = new CPUTensorData(nativeArray);
        /// ]]></code>
        /// </example>
        public CPUTensorData(NativeTensorArray data)
        {
            m_IsDisposed = false;
            if (data == null)
            {
                m_Count = 0;
                m_Array = null;
                return;
            }
            m_Count = data.Length;
            m_Array = data;
        }

        /// <summary>
        /// Finalizes the <see cref="CPUTensorData"/>.
        /// </summary>
        ~CPUTensorData()
        {
            if (m_Array == null || m_Array is NativeTensorArrayFromManagedArray)
                return;
            if (m_IsDisposed)
                return;
            D.LogWarning($"Found unreferenced, but undisposed CPUTensorData which might lead to CPU resource leak");
        }

        /// <summary>
        /// Releases the native memory associated with this <see cref="CPUTensorData"/>.
        /// </summary>
        /// <remarks>
        /// Must be called from the main thread. If pending Job operations exist, this method completes them before releasing memory. Do not call from a finalizer; the garbage collector may run on a different thread and cause undefined behavior.
        /// </remarks>
        /// <example>
        /// <para>Pin a tensor to CPU, schedule jobs, complete pending operations, then dispose.</para>
        /// <code lang="cs"><![CDATA[
        /// var cpuData = CPUTensorData.Pin(inputTensor);
        /// var job = new MyJob { data = cpuData.array.GetNativeArrayHandle<float>() };
        /// cpuData.fence = job.Schedule(inputTensor.shape.length, 64);
        /// worker.Schedule(inputTensor);
        /// cpuData.CompleteAllPendingOperations();
        /// cpuData.Dispose();
        /// ]]></code>
        /// <para>(Refer to the class-level example for an example <c>MyJob</c> definition.)</para>
        /// </example>
        public void Dispose()
        {
            if (!m_SafeToDispose)
            {
                // Only complete operations if the job system is available (must be on main thread)
                if (TensorDataHelper.OnMainThread)
                {
                    CompleteAllPendingOperations();
                }
                else
                {
                    // Note: if not on main thread, it is not even safe to check the jobhandle object itself,
                    // will crash the editor on closing it / teardown, so can't do
                    //      else if (m_ReadFence.IsCompleted && m_WriteFence.IsCompleted)
                    D.LogWarning("CPUTensorData.Dispose() called from a non-main thread while operations are pending");
                    return;
                }
            }

            if (!m_IsDisposed)
            {
                m_Array?.Dispose();
                m_Array = null;
            }

            m_IsDisposed = true;
            System.GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        public void CompleteAllPendingOperations()
        {
            fence.Complete();
            reuse.Complete();
            m_SafeToDispose = true;
        }

        /// <inheritdoc/>
        public void Upload<T>(NativeArray<T> data, int srcCount) where T : unmanaged
        {
            var job = new CPUBackend.CopyJob<T>();
            job.srcIndex = 0;
            job.dstIndex = 0;
            job.length = srcCount;
            unsafe
            {
                job.X = new CPUBackend.ReadOnlyMemResource() { ptr = data.GetUnsafeReadOnlyPtr<T>() };
                job.O = new CPUBackend.ReadWriteMemResource() { ptr = m_Array.RawPtr };
            }
            this.fence = job.Schedule(this.reuse);
        }

        /// <inheritdoc/>
        public NativeArray<T> Download<T>(int dstCount) where T : unmanaged
        {
            if (dstCount == 0)
                return new NativeArray<T>();

            // Download() as optimization gives direct access to the internal buffer
            // thus need to prepare internal buffer for potential writes
            CompleteAllPendingOperations();
            var dest = new NativeArray<T>(dstCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            NativeTensorArray.Copy(m_Array, 0, dest, 0, dstCount);
            return dest;
        }

        #if UNITY_2023_2_OR_NEWER
        /// <inheritdoc/>
        public async Awaitable<NativeArray<T>> DownloadAsync<T>(int dstCount) where T : unmanaged
        {
            if (dstCount == 0)
                return new NativeArray<T>();

            while (!fence.IsCompleted)
                await Task.Yield();

            await Awaitable.MainThreadAsync();

            // Download() as optimization gives direct access to the internal buffer
            // thus need to prepare internal buffer for potential writes
            CompleteAllPendingOperations();
            var dest = new NativeArray<T>(dstCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            NativeTensorArray.Copy(m_Array, 0, dest, 0, dstCount);
            return dest;
        }
        #endif

        /// <inheritdoc/>
        public ComputeTensorData ConvertToComputeTensorData(int count)
        {
            CompleteAllPendingOperations();

            var output = new ComputeTensorData(count);
            if (count == 0)
                return output;

            output.buffer.SetData(array.GetNativeArrayHandle<float>(), 0, 0, count);

            return output;
        }

        /// <inheritdoc/>
        public bool IsReadbackRequestDone()
        {
            if (!fence.IsCompleted)
                return false;
            CompleteAllPendingOperations();
            return true;
        }

        /// <inheritdoc/>
        public void ReadbackRequest() {}

        /// <summary>
        /// Returns a string representation of the CPU tensor data.
        /// </summary>
        /// <remarks>
        /// The format is <c>(CPU burst: [length], uploaded: count)</c>, where <c>length</c> is the buffer length and <c>count</c> is the uploaded element count.
        /// </remarks>
        /// <returns>A string in the form <c>(CPU burst: [length], uploaded: count)</c>.</returns>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var cpuData = CPUTensorData.Pin(inputTensor);
        /// Debug.Log(cpuData.ToString());
        /// // Output: (CPU burst: [256], uploaded: 256)
        /// ]]></code>
        /// </example>
        public override string ToString()
        {
            return string.Format("(CPU burst: [{0}], uploaded: {1})", m_Array?.Length, m_Count);
        }

        /// <summary>
        /// Ensures the tensor's data resides on the CPU and returns the <see cref="CPUTensorData"/>.
        /// </summary>
        /// <remarks>
        /// If the tensor is already on CPU, returns the existing <see cref="CPUTensorData"/>. If on GPU, copies or converts the data to CPU. Use this before scheduling Jobs that read or write the tensor via <see cref="array"/>.
        /// </remarks>
        /// <param name="X">The tensor to pin to CPU.</param>
        /// <param name="clearOnInit">Whether to zero-initialize when allocating new CPU storage. The default value is <c>false</c>.</param>
        /// <returns>The <see cref="CPUTensorData"/> backing the tensor.</returns>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// // Pin a tensor to CPU and write data via a Burst job.
        /// var cpuData = CPUTensorData.Pin(inputTensor);
        /// var job = new MyJob { data = cpuData.array.GetNativeArrayHandle<float>() };
        /// cpuData.fence = job.Schedule(inputTensor.shape.length, 64);
        /// worker.Schedule(inputTensor);
        /// ]]></code>
        /// </example>
        public static CPUTensorData Pin(Tensor X, bool clearOnInit = false)
        {
            var onDevice = X.dataOnBackend;
            if (onDevice == null)
            {
                X.AdoptTensorData(new CPUTensorData(X.count, clearOnInit), disposePrevious: true, disposeIsDelayed: false);
                return X.dataOnBackend as CPUTensorData;
            }

            if (onDevice is CPUTensorData)
                return onDevice as CPUTensorData;
            CPUTensorData dataOnBackend;
            if (onDevice is IConvertibleToCPUTensorData asConvertible)
            {
                dataOnBackend = asConvertible.ConvertToCPUTensorData(X.count);
            }
            else
            {
                dataOnBackend = new CPUTensorData(X.count, clearOnInit: false);
                dataOnBackend.Upload<int>(onDevice.Download<int>(X.count), X.count);
            }
            X.AdoptTensorData(dataOnBackend, disposePrevious: true, disposeIsDelayed: onDevice is ComputeTensorData);

            return X.dataOnBackend as CPUTensorData;
        }
    }
}
