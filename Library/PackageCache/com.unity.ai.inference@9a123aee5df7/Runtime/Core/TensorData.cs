using System;
using Unity.Collections;
using UnityEngine;

namespace Unity.InferenceEngine
{
    /// <summary>
    /// Interface for device-dependent storage of tensor data.
    /// </summary>
    /// <remarks>
    /// <see cref="ITensorData"/> abstracts where tensor elements are physically stored (CPU, GPU compute, or GPU pixel). A <see cref="Tensor"/> holds an <see cref="ITensorData"/> instance via <see cref="Tensor.dataOnBackend"/>.
    ///
    /// <b>Implementations</b><br/>
    /// Use <see cref="CPUTensorData"/> for CPU storage (Burst-compatible, Job system). Use <see cref="ComputeTensorData"/> for GPU compute buffers. Use <see cref="TextureTensorData"/> for GPU texture-backed storage.
    ///
    /// <b>Data transfer</b><br/>
    /// Call <see cref="Upload"/> to copy data into the backing storage. Call <see cref="Download"/> to copy data out (blocking), or <see cref="ReadbackRequest"/> and <see cref="IsReadbackRequestDone"/> for async readback. Call <see cref="CompleteAllPendingOperations"/> before accessing data when operations may be pending.
    ///
    /// <b>Lifetime</b><br/>
    /// Implementations manage native resources. Call <see cref="IDisposable.Dispose"/> when finished. Ownership typically belongs to the tensor; do not dispose separately when the tensor owns the data.
    /// </remarks>
    /// <example>
    /// <para>Pin a tensor to CPU and access its data.</para>
    /// <code lang="cs"><![CDATA[
    /// var cpuData = CPUTensorData.Pin(inputTensor);
    /// var job = new MyJob { data = cpuData.array.GetNativeArrayHandle<float>() };
    /// cpuData.fence = job.Schedule(inputTensor.shape.length, 64);
    /// worker.Schedule(inputTensor);
    /// cpuData.CompleteAllPendingOperations();
    /// cpuData.Dispose();
    /// ]]></code>
    /// </example>
    /// <seealso cref="Tensor"/>
    /// <seealso cref="CPUTensorData"/>
    /// <seealso cref="ComputeTensorData"/>
    /// <seealso cref="TextureTensorData"/>
    /// <seealso cref="BackendType"/>
    [UnityEngine.Scripting.APIUpdating.MovedFrom("Unity.Sentis")]
    public interface ITensorData : IDisposable
    {
        /// <summary>
        /// Uploads a contiguous block of tensor data to internal storage.
        /// </summary>
        /// <remarks>
        /// Copies <paramref name="srcCount"/> elements from <paramref name="data"/> into the internal buffer. For GPU backends, this transfers data from CPU to the device. Call <see cref="CompleteAllPendingOperations"/> before reading the uploaded data if other operations may be pending.
        /// </remarks>
        /// <param name="data">The source data to copy.</param>
        /// <param name="srcCount">The number of elements to copy from <paramref name="data"/>.</param>
        /// <typeparam name="T">The element type of the data (for example, <c>float</c> or <c>int</c>).</typeparam>
        /// <example>
        /// <para>Upload data to backing storage and wait for completion.</para>
        /// <code lang="cs"><![CDATA[
        /// var data = new NativeArray<float>(256, Allocator.Temp);
        /// // Fill data
        /// tensorData.Upload(data, 256);
        /// tensorData.CompleteAllPendingOperations();
        /// ]]></code>
        /// </example>
        void Upload<T>(NativeArray<T> data, int srcCount) where T : unmanaged;

        /// <summary>
        /// Checks if asynchronous readback request is done.
        /// </summary>
        /// <remarks>
        /// Use after calling <see cref="ReadbackRequest"/> to poll for completion. When this returns <see langword="true"/>, the data is available for CPU access. For CPU backends, this completes any pending Job operations and returns <see langword="true"/> when ready.
        /// </remarks>
        /// <returns><see langword="true"/> if the readback has completed. Otherwise <see langword="false"/>.</returns>
        /// <example>
        /// <para>Poll for readback completion, then download.</para>
        /// <code lang="cs"><![CDATA[
        /// tensorData.ReadbackRequest();
        /// while (!tensorData.IsReadbackRequestDone())
        ///     await Task.Yield();
        /// var data = tensorData.Download<float>(count);
        /// ]]></code>
        /// </example>
        bool IsReadbackRequestDone();

        /// <summary>
        /// Schedules asynchronous readback of the internal data.
        /// </summary>
        /// <remarks>
        /// For GPU backends, initiates a non-blocking transfer from device to CPU. Poll <see cref="IsReadbackRequestDone"/> to check completion, then call <see cref="Download"/> to obtain the data. For CPU backends, this is a no-op; data is already on CPU.
        /// </remarks>
        /// <example>
        /// <para>Schedule async readback.</para>
        /// <code lang="cs"><![CDATA[
        /// tensorData.ReadbackRequest();
        /// // Continue other work, then check IsReadbackRequestDone()
        /// ]]></code>
        /// </example>
        void ReadbackRequest();

        /// <summary>
        /// Blocking call to make sure that internal data is correctly written to and available for CPU read back.
        /// </summary>
        /// <remarks>
        /// Call before reading data via <see cref="Download"/> or <see cref="IsReadbackRequestDone"/> when Jobs or GPU operations may still be in progress. For CPU backends, this completes any scheduled Jobs. For GPU backends, this waits for any in-flight transfers.
        /// </remarks>
        /// <example>
        /// <para>Complete pending jobs, then download.</para>
        /// <code lang="cs"><![CDATA[
        /// cpuData.fence = job.Schedule(count, 64);
        /// worker.Schedule(inputTensor);
        /// cpuData.CompleteAllPendingOperations();
        /// var data = cpuData.Download<float>(count);
        /// ]]></code>
        /// </example>
        void CompleteAllPendingOperations();

        /// <summary>
        /// Blocking call that returns a contiguous block of data from internal storage.
        /// </summary>
        /// <remarks>
        /// Blocks until the data is available. For GPU backends, this may trigger a synchronous readback. The returned array uses <see cref="Allocator.Temp"/>. Dispose of it or use it within the same frame. Call <see cref="CompleteAllPendingOperations"/> first if Jobs or GPU work may be pending.
        /// </remarks>
        /// <param name="dstCount">The number of elements to copy.</param>
        /// <typeparam name="T">The element type of the data (for example, <c>float</c> or <c>int</c>).</typeparam>
        /// <returns>A new <see cref="NativeArray{T}"/> containing the copied data.</returns>
        /// <example>
        /// <para>Wait for operations, download data, and dispose the array.</para>
        /// <code lang="cs"><![CDATA[
        /// tensorData.CompleteAllPendingOperations();
        /// var data = tensorData.Download<float>(count);
        /// float value = data[0];
        /// data.Dispose();
        /// ]]></code>
        /// </example>
        NativeArray<T> Download<T>(int dstCount) where T : unmanaged;

        #if UNITY_2023_2_OR_NEWER
        /// <summary>
        /// Awaitable contiguous block of data from internal storage.
        /// </summary>
        /// <remarks>
        /// Use this to download data without blocking the main thread. For GPU backends, the readback runs asynchronously. The returned array uses <see cref="Allocator.Temp"/>. Dispose of it when finished.
        /// </remarks>
        /// <param name="dstCount">The number of elements to copy.</param>
        /// <typeparam name="T">The element type of the data (for example, <c>float</c> or <c>int</c>).</typeparam>
        /// <returns>An <see cref="Awaitable{T}"/> that resolves to a <see cref="NativeArray{T}"/> containing the copied data.</returns>
        /// <example>
        /// Download without blocking the main thread.
        /// <code lang="cs"><![CDATA[
        /// var data = await tensorData.DownloadAsync<float>(count);
        /// float value = data[0];
        /// data.Dispose();
        /// ]]></code>
        /// </example>
        Awaitable<NativeArray<T>> DownloadAsync<T>(int dstCount) where T : unmanaged;
        #endif

        /// <summary>
        /// The maximum count of the stored data elements.
        /// </summary>
        int maxCapacity { get; }

        /// <summary>
        /// The device backend (CPU, GPU compute, or GPU pixel) where the tensor data is stored.
        /// </summary>
        /// <remarks>
        /// Returns <see cref="BackendType.CPU"/>, <see cref="BackendType.GPUCompute"/>, or <see cref="BackendType.GPUPixel"/>. Use this to determine whether data is on CPU or GPU before calling <see cref="Download"/> or scheduling Jobs that access the buffer.
        /// </remarks>
        BackendType backendType { get; }
    }
}
