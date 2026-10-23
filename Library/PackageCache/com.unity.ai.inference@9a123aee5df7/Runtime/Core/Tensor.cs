using System;
using Unity.Collections;
using UnityEngine;

namespace Unity.InferenceEngine
{
    /// <summary>
    /// Represents data in a multidimensional array-like structure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tensors are the fundamental data structure used to represent multidimensional arrays of data, such as images and audio.
    /// Use them as inputs for models, and to download a copy of the backend output values (<see cref="backendType"/>).
    /// </para>
    /// <para>
    /// <b>Ownership and Lifetime</b><br/>
    /// Tensors manage native memory resources. You must call <see cref="Dispose"/> of a tensor when it is no longer needed.
    /// The ownership of the tensor's internal data (<see cref="dataOnBackend"/>) belongs to the <see cref="Tensor"/> object itself.
    /// Disposing the tensor also disposes its underlying data.
    /// </para>
    /// <para>
    /// <b>Data Representation</b><br/>
    /// A <see cref="Tensor"/>'s structure is defined by its <see cref="shape"/> (a <see cref="TensorShape"/> object) and its <see cref="dataType"/>.
    /// The actual data is held by an <see cref="ITensorData"/> implementation, which dictates the physical storage location <see cref="BackendType"/>.
    /// Data within the tensor is stored in a flattened, row-major format.
    /// </para>
    /// <para>
    /// <b>Asynchronous Operations and Data Access</b><br/>
    /// Tensor data can be pending a device is performing computations, or if the data is stored
    /// on a non-readable device-specific type (for example GPU memory).
    /// To get a CPU readable copy of the data, use <see cref="ReadbackAndClone"/>, or <see cref="ReadbackAndCloneAsync"/> for an asynchronous operation.
    /// You can check the status of an asynchronous readback request with <see cref="IsReadbackRequestDone"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>
    /// The following example demonstrates how to interact with a <see cref="Tensor"/> object,
    /// ensuring its data is available on the CPU for reading, and then properly disposing of resources.
    ///
    /// For a full workflow example, refer to [Workflow example](xref:sentis-workflow-example).
    ///
    /// </para>
    /// <code lang="cs"><![CDATA[
    /// // Create a tensor
    /// m_Tensor = new Tensor<float>(new TensorShape(2, 3), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f });
    /// // Alternatively with `using` so you don't need to call `Dispose()` when you are done with the tensor
    /// using var m_OtherTensor = new Tensor<float>(new TensorShape(2, 3), new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f });
    ///
    /// // Get a CPU-accessible clone of the tensor. This copy owns its own data.
    /// Tensor<float> cpuCopyTensor = m_Tensor.ReadbackAndClone() as Tensor<float>;
    ///
    /// // Release memory.
    /// cpuCopyTensor.Dispose();
    /// m_Tensor.Dispose();
    /// ]]></code>
    /// </example>
    /// <seealso cref="ITensorData"/>
    /// <seealso cref="TensorShape"/>
    /// <seealso cref="DataType"/>
    /// <seealso cref="BackendType"/>
    /// <seealso cref="Unity.Collections.NativeArray{T}"/>
    public abstract class Tensor : IDisposable
    {
        private protected ITensorData m_DataOnBackend;
        private protected TensorShape m_Shape;
        private protected bool m_Disposed = false;
        private protected int m_Count;
        private protected DataType m_DataType;

        /// <summary>
        /// The data type of the elements of the tensor.
        /// </summary>
        public DataType dataType { get { return m_DataType; } }

        /// <summary>
        /// The total number of elements in the tensor, calculated as the product of its dimensions.
        /// </summary>
        public int count
        {
            get => m_Count;
            internal set => m_Count = value;
        }

        /// <summary>
        /// The shape of the tensor, as a <see cref="TensorShape"/> object, defining its dimensions.
        /// </summary>
        public TensorShape shape
        {
            get => m_Shape;
            internal set => m_Shape = value;
        }

        /// <summary>
        /// The device-specific internal representation of the tensor data.
        /// </summary>
        /// <remarks>
        /// Accessing this property allows for direct manipulation of the underlying data,
        /// but typically requires knowledge of concrete <see cref="ITensorData"/> implementations.
        /// </remarks>
        public ITensorData dataOnBackend
        {
            get => m_DataOnBackend;
            internal set => m_DataOnBackend = value;
        }

        /// <summary>
        /// The backend type where the tensor data is currently stored (for example, CPU, GPU).
        /// </summary>
        public BackendType backendType
        {
            get {
                Logger.AssertIsTrue(m_DataOnBackend != null, "Tensor is empty and has no data on any backend");
                return m_DataOnBackend.backendType;
            }
        }

        internal bool disposed => m_Disposed;

        /// <summary>
        /// Changes the logical shape of the tensor without changing the backing data's physical allocation.
        /// </summary>
        /// <param name="shape">The new shape for the tensor. The total number of elements in the new shape must
        /// fit within the currently allocated backend tensor data's capacity.</param>
        /// <exception cref="UnityEngine.Assertions.AssertionException">Thrown if the new shape's total elements exceed the allocated capacity.</exception>
        public abstract void Reshape(TensorShape shape);

        /// <summary>
        /// Associates a new tensor data object with this tensor.
        /// </summary>
        /// <param name="tensorData">The new <see cref="ITensorData"/> instance to associate with the tensor.
        /// This data must have sufficient capacity for the tensor's current count.</param>
        /// <param name="disposePrevious">If <see langword="true"/>, the previously associated tensor data will be disposed.
        /// Set to <see langword="false"/> if you intend to manage the lifetime of the previous data manually.</param>
        /// <exception cref="UnityEngine.Assertions.AssertionException">Thrown if the provided `tensorData` has insufficient capacity for the tensor's current element count.</exception>
        public void AdoptTensorData(ITensorData tensorData, bool disposePrevious = true)
        {
            AdoptTensorData(tensorData, disposePrevious, disposeIsDelayed: true);
        }

        /// <summary>
        /// Associates a new tensor data to the tensor.
        /// </summary>
        /// <param name="tensorData">The new tensor data to associate to the tensor.</param>
        /// <param name="disposePrevious">Whether to dispose the previous tensor data.</param>
        /// <param name="disposeIsDelayed">If tensor data is on GPU compute backend, ensures the disposition happens only after a later dispatch call that uses the underlying buffer.</param>
        internal void AdoptTensorData(ITensorData tensorData, bool disposePrevious = true, bool disposeIsDelayed = true)
        {
            if (m_DataOnBackend == tensorData)
                return;

            Logger.AssertIsTrue(tensorData?.maxCapacity >= count || tensorData == null, "Tensor.AdoptTensorData: not enough capacity on device to pin tensor or device null");

            if (disposePrevious)
            {
                if (disposeIsDelayed && m_DataOnBackend is ComputeTensorData computeDataB)
                {
                    computeDataB?.DelayedDispose();
                }
                else
                {
                    m_DataOnBackend?.Dispose();
                }
            }

            m_DataOnBackend = tensorData;
        }

        /// <summary>
        /// Detaches the current <see cref="ITensorData"/> object from the tensor and returns it.
        /// The tensor will no longer manage the lifetime of this data.
        /// </summary>
        /// <returns>The <see cref="ITensorData"/> object that was previously associated with this tensor.
        /// Returns <see langword="null"/> if no data was associated.</returns>
        public ITensorData ReleaseTensorData()
        {
            var tensorData = m_DataOnBackend;
            m_DataOnBackend = null;
            return tensorData;
        }

        internal abstract Tensor CloneEmpty();

        /// <summary>
        /// Checks if an asynchronous readback request for the tensor's data has completed.
        /// </summary>
        /// <returns><see langword="true"/> if the asynchronous readback request is done and successful. Otherwise <see langword="false"/>.</returns>
        public bool IsReadbackRequestDone()
        {
            if (m_DataOnBackend == null)
                return false;

            return m_DataOnBackend.IsReadbackRequestDone();
        }

        /// <summary>
        /// Schedules an asynchronous download of the internal tensor data from its backend to a CPU-accessible location.
        /// You can check the completion of this request using <see cref="IsReadbackRequestDone"/>.
        /// </summary>
        public void ReadbackRequest()
        {
            m_DataOnBackend?.ReadbackRequest();
        }

        /// <summary>
        /// Performs a blocking download of the internal tensor data from its backend to a new CPU-accessible <see cref="Tensor"/> instance.
        /// This method ensures all pending operations on the original tensor's data are completed before the download begins.
        /// </summary>
        /// <returns>A new <see cref="Tensor"/> instance containing a CPU-accessible copy of the original tensor's data.
        /// This new tensor is independent and must also be disposed of.</returns>
        public Tensor ReadbackAndClone()
        {
            var tensor = CloneEmpty();
            if (count == 0)
            {
                tensor.dataOnBackend = new CPUTensorData(0);
                return tensor;
            }

            var data = m_DataOnBackend.Download<int>(count);

            var cpuData = new CPUTensorData(count);
            NativeTensorArray.Copy(data, 0, cpuData.array, 0, count);

            tensor.dataOnBackend = cpuData;
            return tensor;
        }

        #if UNITY_2023_2_OR_NEWER
        /// <summary>
        /// Schedules an asynchronous download of the internal tensor data from its backend to a new CPU-accessible <see cref="Tensor"/> instance.
        /// This method returns an <see cref="Awaitable{T}"/> that can be used to await the completion of the download operation without blocking the main thread.
        /// </summary>
        /// <returns>An <see cref="Awaitable{T}"/> that resolves to a new <see cref="Tensor"/> instance containing a CPU-accessible copy of the original tensor's data.
        /// This new tensor is independent and must also be disposed of.</returns>
        public async Awaitable<Tensor> ReadbackAndCloneAsync()
        {
            var tensor = CloneEmpty();
            if (count == 0)
            {
                tensor.dataOnBackend = new CPUTensorData(0);
                return tensor;
            }

            var data = await m_DataOnBackend.DownloadAsync<int>(count);

            var cpuData = new CPUTensorData(count);
            NativeTensorArray.Copy(data, 0, cpuData.array, 0, count);

            tensor.dataOnBackend = cpuData;
            return tensor;
        }
        #endif

        /// <summary>
        /// Completes all scheduled tensor operations on the device backend.
        /// This is a blocking call that ensures all pending computations or data transfers related to this tensor have completed.
        /// </summary>
        public void CompleteAllPendingOperations()
        {
            m_DataOnBackend?.CompleteAllPendingOperations();
        }

        /// <summary>
        /// Disposes of the tensor and releases any associated unmanaged memory resources.
        /// This method must be called on the main thread to prevent memory leaks.
        /// After calling `Dispose`, the tensor instance should no longer be used.
        /// </summary>
        public void Dispose()
        {
            m_DataOnBackend?.Dispose();
            m_DataOnBackend = null;
            m_Disposed = true;
        }

        /// <summary>
        /// Returns a string that represents the <see cref="Tensor"/>'s data type and shape.
        /// </summary>
        /// <returns>A string representation of the tensor.</returns>
        public override string ToString()
        {
            return $"{dataType}{shape}";
        }

        internal NativeArray<T>.ReadOnly AsReadOnlyNativeArray<T>() where T : unmanaged
        {
            if (count == 0)
                return new NativeArray<T>.ReadOnly();

            if (m_DataOnBackend is CPUTensorData rwData)
            {
                if (rwData.IsReadbackRequestDone())
                    return rwData.array.AsReadOnlyNativeArray<T>(shape.length);
                else
                    throw new InvalidOperationException("Tensor data is still pending, cannot read from tensor.");
            }
            else
                throw new InvalidOperationException("Tensor data cannot be read from, use .ReadbackAndClone() to allow reading from tensor.");
        }
        internal ReadOnlySpan<T> AsReadOnlySpan<T>() where T : unmanaged
        {
            if (count == 0)
                return ReadOnlySpan<T>.Empty;

            if (m_DataOnBackend is CPUTensorData rwData)
            {
                if (rwData.IsReadbackRequestDone())
                    return rwData.array.AsReadOnlySpan<T>(shape.length);
                else
                    throw new InvalidOperationException("Tensor data is still pending, cannot read from tensor.");
            }
            else
                throw new InvalidOperationException("Tensor data cannot be read from, use .ReadbackAndClone() to allow reading from tensor.");
        }
        internal T GetItem<T>(int d0) where T : unmanaged
        {
            if (m_DataOnBackend is CPUTensorData rwData)
            {
                if (rwData.IsReadbackRequestDone())
                    return rwData.array.Get<T>(d0);
                else
                    throw new InvalidOperationException("Tensor data is still pending, cannot read from tensor.");
            }
            else
                throw new InvalidOperationException("Tensor data cannot be read from, use .ReadbackAndClone() to allow reading from tensor.");
        }
        internal void SetItem<T>(int d0, T value) where T : unmanaged
        {
            if (m_DataOnBackend is CPUTensorData rwData)
            {
                if (rwData.IsReadbackRequestDone())
                    rwData.array.Set<T>(d0, value);
                else
                    throw new InvalidOperationException("Tensor data is still pending, cannot write to tensor.");
            }
            else
                throw new InvalidOperationException("Tensor data cannot be read from, use .ReadbackAndClone() to allow writing to the tensor.");
        }
    }
}
