using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.InferenceEngine
{
    ///see https://referencesource.microsoft.com/#mscorlib/system/runtime/interopservices/safehandle.cs
    class NativeMemorySafeHandle : SafeHandle
    {
        readonly Allocator m_AllocatorLabel;
        const int k_Alignment = sizeof(float);

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        public unsafe NativeMemorySafeHandle(long size, bool clearOnInit, Allocator allocator) : base(IntPtr.Zero, true)
        {
            m_AllocatorLabel = allocator;
            SetHandle((IntPtr)UnsafeUtility.Malloc(size, k_Alignment, allocator));
            if (clearOnInit)
                UnsafeUtility.MemClear((void*)handle, size);
        }

        public override bool IsInvalid {
            get { return handle == IntPtr.Zero; }
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        protected override unsafe bool ReleaseHandle()
        {
            UnsafeUtility.Free((void*)handle, m_AllocatorLabel);
            return true;
        }
    }

    class PinnedMemorySafeHandle : SafeHandle
    {
        private readonly GCHandle m_GCHandle;

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        public PinnedMemorySafeHandle(Array managedObject)
            : base(IntPtr.Zero, true)
        {
            m_GCHandle = GCHandle.Alloc(managedObject, GCHandleType.Pinned);
            IntPtr pinnedPtr = m_GCHandle.AddrOfPinnedObject();
            SetHandle(pinnedPtr);
        }

        public PinnedMemorySafeHandle(ArraySegment<byte> segment)
            : base(IntPtr.Zero, true)
        {
            m_GCHandle = GCHandle.Alloc(segment.Array, GCHandleType.Pinned);
            IntPtr basePtr = m_GCHandle.AddrOfPinnedObject();
            SetHandle(basePtr + segment.Offset);
        }

        public override bool IsInvalid {
            get { return handle == IntPtr.Zero; }
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        protected override bool ReleaseHandle()
        {
            m_GCHandle.Free();
            return true;
        }
    }

    /// <summary>
    /// Options for the data type of a <see cref="Tensor"/>.
    /// </summary>
    /// <remarks>
    /// Pass a value from this enum to specify the element type of a tensor. Use <see cref="Float"/> or <see cref="Int"/> for most operations; these map directly to 32-bit storage. <see cref="Short"/>, <see cref="Byte"/>, and <see cref="Custom"/> are stored padded in an int32 buffer for alignment.
    /// </remarks>
    /// <example>
    /// <code lang="cs"><![CDATA[
    /// var input = Functional.Constant(new[] { 1.5f, 2.7f, 3.2f });
    /// var result = Functional.Type(input, DataType.Int);
    /// // result has integer elements: [1, 2, 3]
    /// ]]></code>
    /// </example>
    [UnityEngine.Scripting.APIUpdating.MovedFrom("Unity.Sentis")]
    public enum DataType
    {
        /// <summary>
        /// 32-bit floating point data.
        /// </summary>
        Float,
        /// <summary>
        /// 32-bit signed integer data.
        /// </summary>
        Int,
        /// <summary>
        /// 16-bit signed integer data, padded in an int32 buffer.
        /// </summary>
        Short,
        /// <summary>
        /// 8-bit unsigned integer data, padded in an int32 buffer.
        /// </summary>
        Byte,
        /// <summary>
        /// Raw n-bit field data, padded in an int32 buffer.
        /// </summary>
        Custom
    }

    /// <summary>
    /// Represents an area of managed memory that's exposed as if it's native memory.
    /// </summary>
    /// <remarks>
    /// Pins the managed array so it can be used where a <see cref="NativeTensorArray"/> is expected, avoiding copies. The managed array must remain allocated for the lifetime of this instance. <see cref="Dispose"/> does not free the managed array; it only releases the pin.
    ///
    /// **Additional resources**
    ///
    /// - <see cref="NativeTensorArray"/>
    /// - <see cref="CPUTensorData"/>
    /// </remarks>
    /// <example>
    /// <para>Wrap a managed float array for use with tensor operations without copying.</para>
    /// <code lang="cs"><![CDATA[
    /// var data = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
    /// using var tensorArray = new NativeTensorArrayFromManagedArray(data, 0, sizeof(float), data.Length);
    /// float value = tensorArray.Get<float>(2);
    /// // value is 3.0f; data is pinned, not copied
    /// ]]></code>
    /// </example>
    [UnityEngine.Scripting.APIUpdating.MovedFrom("Unity.Sentis")]
    public class NativeTensorArrayFromManagedArray : NativeTensorArray
    {
        readonly int m_PinnedMemoryByteOffset;

        /// <summary>
        /// Creates a new wrapper that pins a byte array and exposes a view of it as a native tensor array.
        /// </summary>
        /// <remarks>
        /// Pins <paramref name="srcData"/> and exposes a view of <paramref name="numDestElement"/> elements (1 byte each) starting at <paramref name="srcOffset"/>. The source is padded to <c>k_DataItemSize</c> bytes per element.
        /// </remarks>
        /// <param name="srcData">The backing data as a byte array.</param>
        /// <param name="srcOffset">The byte offset into <paramref name="srcData"/> where the view starts.</param>
        /// <param name="numDestElement">The number of elements (1 byte each) to expose.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="srcOffset"/> or <paramref name="numDestElement"/> is invalid for the source array.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the source buffer is too small to account for padding.</exception>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var bytes = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };
        /// var tensorArray = new NativeTensorArrayFromManagedArray(bytes, 2, 4);
        /// // Exposes elements at indices 2, 3, 4, 5
        /// ]]></code>
        /// </example>
        public NativeTensorArrayFromManagedArray(byte[] srcData, int srcOffset, int numDestElement)
            : this(srcData, srcOffset, sizeof(byte), numDestElement) { }

        /// <summary>
        /// Creates a new wrapper that pins a managed array and exposes a view of it as a native tensor array.
        /// </summary>
        /// <remarks>
        /// Pins <paramref name="srcData"/> and exposes a view of <paramref name="numDestElement"/> elements. Each element is <paramref name="srcElementSize"/> bytes; the backing buffer pads to <c>k_DataItemSize</c> bytes per element.
        /// </remarks>
        /// <param name="srcData">The backing data as a managed array.</param>
        /// <param name="srcElementOffset">The element offset into <paramref name="srcData"/> where the view starts.</param>
        /// <param name="srcElementSize">The size in bytes of each element in <paramref name="srcData"/>.</param>
        /// <param name="numDestElement">The number of elements to expose.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="srcElementOffset"/> exceeds the array length or <paramref name="numDestElement"/> exceeds the available data.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the source buffer is too small to account for padding to <c>k_DataItemSize</c> bytes per element.</exception>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var floats = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
        /// var tensorArray = new NativeTensorArrayFromManagedArray(floats, 0, sizeof(float), floats.Length);
        /// float value = tensorArray.Get<float>(0);
        /// ]]></code>
        /// </example>
        public NativeTensorArrayFromManagedArray(Array srcData, int srcElementOffset, int srcElementSize, int numDestElement)
            : base(new PinnedMemorySafeHandle(srcData), numDestElement)
        {
            m_PinnedMemoryByteOffset = srcElementSize * srcElementOffset;

            //Safety checks
            int srcLengthInByte = (srcData.Length - srcElementOffset) * srcElementSize;
            int dstLengthInByte = numDestElement * k_DataItemSize;
            if (srcElementOffset > srcData.Length)
                throw new ArgumentOutOfRangeException(nameof(srcElementOffset), "SrcElementOffset must be <= srcData.Length");
            if (dstLengthInByte > srcLengthInByte)
                throw new ArgumentOutOfRangeException(nameof(numDestElement), "NumDestElement too big for srcData and srcElementOffset");
            var neededSrcPaddedLengthInByte = numDestElement * k_DataItemSize;
            if (srcLengthInByte < neededSrcPaddedLengthInByte)
                throw new InvalidOperationException($"The NativeTensorArrayFromManagedArray source ptr (including offset) is too small to account for extra padding.");
        }

        internal NativeTensorArrayFromManagedArray(ArraySegment<byte> srcData, int numDestElement)
            : base(new PinnedMemorySafeHandle(srcData), numDestElement) // TODO check this
        {
            m_PinnedMemoryByteOffset = 0;
        }

        /// <inheritdoc/>
        public override unsafe void* RawPtr => (byte*)base.RawPtr + m_PinnedMemoryByteOffset;

        /// <summary>
        /// Disposes of the array and any associated memory.
        /// </summary>
        /// <remarks>
        /// Releases the pin on the managed array. Does not free the managed array itself.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var data = new float[] { 1.0f, 2.0f, 3.0f };
        /// var tensorArray = new NativeTensorArrayFromManagedArray(data, 0, sizeof(float), data.Length);
        /// tensorArray.Dispose();
        /// // data remains allocated; only the pin is released
        /// ]]></code>
        /// </example>
        public override void Dispose() {}
    }

    /// <summary>
    /// Represents an area of native memory that's exposed to managed code for tensor data storage.
    /// </summary>
    /// <remarks>
    /// <see cref="NativeTensorArray"/> provides a managed wrapper around native memory allocated for tensor operations.
    /// The backing buffer stores elements as 32-bit floats (<c>k_DataItemSize</c> bytes per element), regardless of the logical data type.
    ///
    /// Use this class when you need to allocate native memory for tensor data that will be used by CPU backends.
    /// For data backed by managed arrays (avoiding copies), use <see cref="NativeTensorArrayFromManagedArray"/> instead.
    ///
    /// The class implements <see cref="IDisposable"/>. Call <see cref="Dispose"/> when you no longer need the array to release native resources.
    /// Accessing <see cref="RawPtr"/> or other members after disposal throws <see cref="InvalidOperationException"/>.
    ///
    /// Use <see cref="Get"/> and <see cref="Set"/> for individual element access.
    /// Use the static <see cref="Copy"/> and <see cref="BlockCopy"/> methods to transfer data in bulk between <see cref="NativeTensorArray"/> instances and managed arrays.
    ///
    /// **Additional resources**
    ///
    /// - <see cref="CPUTensorData"/>
    /// - <see cref="NativeTensorArrayFromManagedArray"/>
    /// </remarks>
    /// <example>
    /// <code lang="cs"><![CDATA[
    /// // Allocate native memory for 1024 float elements
    /// var tensorArray = new NativeTensorArray(1024);
    /// // Set and get values
    /// tensorArray.Set(0, 1.0f);
    /// float value = tensorArray.Get<float>(0);
    /// // Copy to managed array
    /// float[] floatArray = tensorArray.ToArray<float>(1024, 0);
    /// // Dispose of the native memory
    /// tensorArray.Dispose();
    /// ]]></code>
    /// </example>
    [UnityEngine.Scripting.APIUpdating.MovedFrom("Unity.Sentis")]
    public class NativeTensorArray : IDisposable
    {
        private protected readonly SafeHandle m_SafeHandle;
        readonly Allocator m_Allocator;
        readonly int m_Length;

        /// <summary>
        /// Size in bytes of an individual element.
        /// </summary>
        /// <remarks>
        /// The backing buffer stores elements as 32-bit floats.
        /// <b>Note</b>: Generic methods use <c>sizeof(T)</c> for indexing instead.
        /// </remarks>
        public const int k_DataItemSize = sizeof(float);

        /// <summary>
        /// Initializes and returns an instance of <see cref="NativeTensorArray"/> with a preexisting handle.
        /// </summary>
        /// <remarks>
        /// Use this constructor when creating derived types that wrap existing memory (for example, pinned managed arrays).
        /// The caller is responsible for ensuring the handle remains valid for the lifetime of the array.
        /// </remarks>
        /// <param name="safeHandle">The safe handle to the data.</param>
        /// <param name="dataLength">The number of elements.</param>
        protected NativeTensorArray(SafeHandle safeHandle, int dataLength)
        {
            m_Length = dataLength;
            m_SafeHandle = safeHandle;
            m_Allocator = Allocator.Persistent;
        }

        /// <summary>
        /// Initializes and returns an instance of <see cref="NativeTensorArray"/> with a given length.
        /// </summary>
        /// <remarks>
        /// Allocates native memory for <paramref name="length"/> elements (each <c>k_DataItemSize</c> bytes). Use <paramref name="clearOnInit"/> to zero-initialize the buffer.
        /// The default allocator is <see cref="Allocator.Persistent"/>, which survives across frames.
        /// </remarks>
        /// <param name="length">The number of elements to allocate.</param>
        /// <param name="clearOnInit">Whether to zero the data after allocating. The default value is <c>false</c>.</param>
        /// <param name="allocator">The allocation type to use. The default value is <see cref="Allocator.Persistent"/>.</param>
        /// <exception cref="InvalidOperationException">Thrown when the allocator is not valid.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when length is less than or equal to zero.</exception>
        public NativeTensorArray(int length, bool clearOnInit = false, Allocator allocator = Allocator.Persistent)
        {
            if (!UnsafeUtility.IsValidAllocator(allocator))
                throw new InvalidOperationException("The NativeTensorArray should use a valid allocator.");
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof (length), "Length must be > 0");

            m_Length = length;
            m_SafeHandle = new NativeMemorySafeHandle(m_Length * k_DataItemSize, clearOnInit, allocator);
            m_Allocator = allocator;
        }

        /// <summary>
        /// Clears the allocated memory to zero.
        /// </summary>
        /// <remarks>
        /// Fills the entire backing buffer with zeros.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// using (var tensorArray = new NativeTensorArray(256))
        /// {
        ///     // Clear before use to ensure deterministic behavior
        ///     tensorArray.ZeroMemory();
        /// }
        /// ]]></code>
        /// </example>
        public unsafe void ZeroMemory()
        {
            var numByteToClear = m_Length * k_DataItemSize;
            UnsafeUtility.MemClear(RawPtr, numByteToClear);
        }

        /// <summary>
        /// Disposes of the array and any associated memory.
        /// </summary>
        /// <remarks>
        /// Releases the native memory backing the array. Once disposed, <see cref="Disposed"/> returns <see langword="true"/>, and any access to <see cref="RawPtr"/> or other members throws <see cref="InvalidOperationException"/>.
        /// </remarks>
        /// <example>
        /// <para>Use a <c>using</c> block to ensure Dispose is called when finished.</para>
        /// <code lang="cs"><![CDATA[
        /// using (var tensorArray = new NativeTensorArray(64))
        /// {
        ///     tensorArray.Set(0, 1.0f);
        ///     // Dispose is called automatically when the using block exits.
        /// }
        /// ]]></code>
        /// </example>
        public virtual void Dispose()
        {
            m_SafeHandle.Dispose();
        }

        /// <summary>
        /// The number of allocated elements in the backing buffer.
        /// </summary>
        /// <remarks>
        /// The total size in bytes is <c>Length * k_DataItemSize</c>.
        /// </remarks>
        public int Length => m_Length;

        /// <summary>
        /// The raw pointer of the backing data.
        /// </summary>
        /// <remarks>
        /// Returns a pointer to the first element. Throws <see cref="InvalidOperationException"/> if the array has been disposed.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the array has been disposed.</exception>
        public virtual unsafe void* RawPtr
        {
            get
            {
                if (Disposed)
                    throw new InvalidOperationException("The NativeTensorArray was disposed.");
                return (void*)m_SafeHandle.DangerousGetHandle();
            }
        }

        /// <summary>
        /// Whether the backing data has been disposed.
        /// </summary>
        /// <remarks>
        /// When <see langword="true"/>, accessing <see cref="RawPtr"/> or performing operations throws <see cref="InvalidOperationException"/>.
        /// </remarks>
        public bool Disposed => m_SafeHandle.IsClosed;

        /// <summary>
        /// Returns the raw pointer of the backing data at a given index.
        /// </summary>
        /// <remarks>
        /// Computes the address of the element at <paramref name="index"/> as a pointer to type <typeparamref name="T"/>. Use for low-level access when <see cref="Get{T}"/> or <see cref="Set{T}"/> are insufficient.
        /// </remarks>
        /// <param name="index">The index of the element.</param>
        /// <typeparam name="T">The type of the element.</typeparam>
        /// <returns>The raw pointer to the element in the data.</returns>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// using (var tensorArray = new NativeTensorArray(64))
        /// {
        ///     unsafe
        ///     {
        ///         float* ptr = tensorArray.AddressAt<float>(0);
        ///         *ptr = 3.14f;
        ///     }
        /// }
        /// ]]></code>
        /// </example>
        public unsafe T* AddressAt<T>(long index) where T : unmanaged
        {
            return ((T*)RawPtr) + index;
        }

        /// <summary>
        /// Returns the value of the backing data at a given index.
        /// </summary>
        /// <remarks>
        /// Uses <c>sizeof(T)</c> for byte-offset indexing. For correct behavior, use <c>T</c> such that <c>sizeof(T) == k_DataItemSize</c> (for example, <c>float</c> or <c>int</c>), since the backing buffer stores elements as 32-bit floats.
        /// </remarks>
        /// <param name="index">The index of the element.</param>
        /// <typeparam name="T">The type of the element.</typeparam>
        /// <returns>The value of the element at <c>index</c>, read as type <c>T</c>.</returns>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// using (var tensorArray = new NativeTensorArray(64))
        /// {
        ///     tensorArray.Set(0, 42.0f);
        ///     float value = tensorArray.Get<float>(0); // Returns 42.0f
        /// }
        /// ]]></code>
        /// </example>
        public unsafe T Get<T>(int index) where T : unmanaged
        {
            return UnsafeUtility.ReadArrayElement<T>(RawPtr, index);
        }

        /// <summary>
        /// Sets the value of the backing data at a given index.
        /// </summary>
        /// <remarks>
        /// Uses <c>sizeof(T)</c> for byte-offset indexing. For correct behavior, use <c>T</c> such that <c>sizeof(T) == k_DataItemSize</c> (for example, <c>float</c> or <c>int</c>), since the backing buffer stores elements as 32-bit floats.
        /// </remarks>
        /// <param name="index">The index of the element.</param>
        /// <param name="value">The value to set at the index.</param>
        /// <typeparam name="T">The type of the element.</typeparam>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// using (var tensorArray = new NativeTensorArray(64))
        /// {
        ///     tensorArray.Set(0, 1.0f);
        ///     tensorArray.Set(1, 2.0f);
        /// }
        /// ]]></code>
        /// </example>
        public unsafe void Set<T>(int index, T value) where T : unmanaged
        {
            UnsafeUtility.WriteArrayElement<T>(RawPtr, index, value);
        }

        /// <summary>
        /// Returns the data converted to a [NativeArray](xref:Unity.Collections.NativeArray_1.ReadOnly.AsReadOnlySpan).
        /// </summary>
        /// <remarks>
        /// Creates a view over the backing memory without copying. The returned array shares the same allocator and lifetime as this instance.
        /// Do not use the returned array after this instance is disposed.
        /// </remarks>
        /// <typeparam name="T">The type of the data.</typeparam>
        /// <returns>The converted native array from data.</returns>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// using (var tensorArray = new NativeTensorArray(64))
        /// {
        ///     NativeArray<float> nativeArray = tensorArray.GetNativeArrayHandle<float>();
        ///     // Use nativeArray with Unity Jobs or other NativeArray APIs
        /// }
        /// ]]></code>
        /// </example>
        public NativeArray<T> GetNativeArrayHandle<T>() where T : unmanaged
        {
            unsafe
            {
                NativeArray<T> nativeArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(RawPtr, m_Length, m_Allocator);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref nativeArray, AtomicSafetyHandle.Create());
#endif
                return nativeArray;
            }
        }

        /// <summary>
        /// Returns the data as a [NativeArray](xref:Unity.Collections.NativeArray_1.ReadOnly.AsReadOnlySpan) constrained to read only operations.
        /// </summary>
        /// <remarks>
        /// Creates a read-only view over a slice of the backing memory. Use for passing data to jobs or APIs that require read-only access.
        /// </remarks>
        /// <param name="dstCount">The number of elements.</param>
        /// <param name="srcOffset">The index of the first element. Defaults to <c>0</c>.</param>
        /// <typeparam name="T">The type of the data.</typeparam>
        /// <returns>The read only native array of the data.</returns>
        /// <example>
        /// <para>Get a read-only view over a slice for use with jobs or read-only APIs.</para>
        /// <code lang="cs"><![CDATA[
        /// using (var tensorArray = new NativeTensorArray(64))
        /// {
        ///     tensorArray.Set(0, 1.0f);
        ///     NativeArray<float>.ReadOnly readOnly = tensorArray.AsReadOnlyNativeArray<float>(64, 0);
        ///     // Pass readOnly to jobs that require read-only access
        /// }
        /// ]]></code>
        /// </example>
        public NativeArray<T>.ReadOnly AsReadOnlyNativeArray<T>(int dstCount, int srcOffset = 0) where T : unmanaged
        {
            unsafe
            {
                NativeArray<T> nativeArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>((byte*)RawPtr + srcOffset * sizeof(T), dstCount, m_Allocator);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref nativeArray, AtomicSafetyHandle.Create());
#endif
                return nativeArray.AsReadOnly();
            }
        }

        /// <summary>
        /// Returns the data as a <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        /// <remarks>
        /// Creates a read-only span over a slice of the backing memory. Use for passing data to APIs that accept spans.
        /// </remarks>
        /// <param name="dstCount">The number of elements.</param>
        /// <param name="srcOffset">The index of the first element. Defaults to <c>0</c>.</param>
        /// <typeparam name="T">The type of the data.</typeparam>
        /// <returns>The span of the data.</returns>
        /// <example>
        /// <para>Get a read-only span over a slice for use with .NET span APIs.</para>
        /// <code lang="cs"><![CDATA[
        /// using (var tensorArray = new NativeTensorArray(64))
        /// {
        ///     tensorArray.Set(0, 1.0f);
        ///     ReadOnlySpan<float> span = tensorArray.AsReadOnlySpan<float>(64, 0);
        ///     // Pass span to .NET APIs that accept ReadOnlySpan
        /// }
        /// ]]></code>
        /// </example>
        public ReadOnlySpan<T> AsReadOnlySpan<T>(int dstCount, int srcOffset = 0) where T : unmanaged
        {
            unsafe
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(AtomicSafetyHandle.Create());
#endif

                return new ReadOnlySpan<T>((byte*)RawPtr + srcOffset * sizeof(T), dstCount);
            }
        }

        /// <summary>
        /// Returns the data as an array.
        /// </summary>
        /// <remarks>
        /// Allocates a new managed array and copies the specified slice of data.
        /// </remarks>
        /// <param name="dstCount">The number of elements.</param>
        /// <param name="srcOffset">The index of the first element. Defaults to <c>0</c>.</param>
        /// <typeparam name="T">The type of the data.</typeparam>
        /// <returns>The copied array of the data.</returns>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// using (var tensorArray = new NativeTensorArray(64))
        /// {
        ///     tensorArray.Set(0, 1.0f);
        ///     float[] managedArray = tensorArray.ToArray<float>(64, 0);
        /// }
        /// ]]></code>
        /// </example>
        public T[] ToArray<T>(int dstCount, int srcOffset = 0) where T : unmanaged
        {
            var array = new T[dstCount];
            Copy(this, srcOffset, array, 0, dstCount);
            return array;
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        static void CheckCopyArguments(int srcLength, int srcIndex, int dstLength, int dstIndex, int length)
        {
            // all dims in byte
            if (length < 0)
                throw new ArgumentOutOfRangeException("length must be equal or greater than zero.");

            if (srcIndex < 0 || srcIndex > srcLength || (srcIndex == srcLength && srcLength > 0))
                throw new ArgumentOutOfRangeException("srcIndex is outside the range of valid indexes for the source buffer.");

            if (dstIndex < 0 || dstIndex > dstLength || (dstIndex == dstLength && dstLength > 0))
                throw new ArgumentOutOfRangeException("dstIndex is outside the range of valid indexes for the destination buffer.");

            if (srcIndex + length > srcLength)
                throw new ArgumentException("length is greater than the number of elements from srcIndex to the end of the source buffer.");

            if (srcIndex + length < 0)
                throw new ArgumentException("srcIndex + length causes an integer overflow");

            if (dstIndex + length > dstLength)
                throw new ArgumentException("length is greater than the number of elements from dstIndex to the end of the destination buffer.");

            if (dstIndex + length < 0)
                throw new ArgumentException("dstIndex + length causes an integer overflow");
        }
#endif

        /// <summary>
        /// Copies the data from a source <see cref="NativeTensorArray"/> to a destination <see cref="NativeTensorArray"/> up to a given length starting from given indexes.
        /// </summary>
        /// <remarks>
        /// Performs a byte-level copy. Both source and destination use <c>k_DataItemSize</c> bytes per element.
        /// </remarks>
        /// <param name="src">The array to copy from.</param>
        /// <param name="srcIndex">The index of the first element to copy from.</param>
        /// <param name="dst">The array to copy to.</param>
        /// <param name="dstIndex">The index of the first element to copy to.</param>
        /// <param name="length">The number of elements.</param>
        /// <exception cref="ArgumentException">Thrown if the given indexes and length are out of bounds of the source or destination array.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when indexes or length are invalid.</exception>
        /// <example>
        /// <para>Copy elements between two NativeTensorArray instances.</para>
        /// <code lang="cs"><![CDATA[
        /// using (var src = new NativeTensorArray(100))
        /// using (var dst = new NativeTensorArray(100))
        /// {
        ///     NativeTensorArray.Copy(src, 0, dst, 0, 100);
        /// }
        /// ]]></code>
        /// </example>
        public static unsafe void Copy(NativeTensorArray src, int srcIndex, NativeTensorArray dst, int dstIndex, int length)
        {
            if (length == 0)
                return;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CheckCopyArguments(src.Length, srcIndex, dst.Length, dstIndex, length);
#endif

            void* srcPtr = src.RawPtr;
            void* dstPtr = dst.RawPtr;
            UnsafeUtility.MemCpy((byte*)dstPtr + dstIndex * k_DataItemSize,
                                 (byte*)srcPtr + srcIndex * k_DataItemSize,
                                 length * k_DataItemSize);
        }

        /// <summary>
        /// Copies the data from a source <see cref="NativeTensorArray"/> to a destination managed array up to a given length starting from given indexes.
        /// </summary>
        /// <remarks>
        /// The source uses <c>k_DataItemSize</c> bytes per element. The destination uses <c>sizeof(T)</c>. Use when transferring tensor data to a managed array for interoperability.
        /// </remarks>
        /// <param name="src">The array to copy from.</param>
        /// <param name="srcIndex">The index of the first element to copy from.</param>
        /// <param name="dst">The array to copy to.</param>
        /// <param name="dstIndex">The index of the first element to copy to.</param>
        /// <param name="length">The number of elements.</param>
        /// <typeparam name="T">The data type of the elements.</typeparam>
        /// <exception cref="ArgumentException">Thrown if the given indexes and length are out of bounds of the source or destination array.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when indexes or length are invalid.</exception>
        /// <example>
        /// <para>Copy tensor data to a managed array.</para>
        /// <code lang="cs"><![CDATA[
        /// using (var src = new NativeTensorArray(64))
        /// {
        ///     src.Set(0, 1.0f);
        ///     float[] dst = new float[64];
        ///     NativeTensorArray.Copy(src, 0, dst, 0, 64);
        /// }
        /// ]]></code>
        /// </example>
        public static unsafe void Copy<T>(NativeTensorArray src, int srcIndex, T[] dst, int dstIndex, int length) where T : unmanaged
        {
            if (length == 0)
                return;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CheckCopyArguments(src.Length * k_DataItemSize, srcIndex * k_DataItemSize, dst.Length * sizeof(T), dstIndex * sizeof(T), length * sizeof(T));
#endif

            fixed (void* dstPtr = &dst[0])
            {
                void* srcPtr = src.RawPtr;
                UnsafeUtility.MemCpy((byte*)dstPtr + dstIndex * sizeof(T),
                                    (byte*)srcPtr + srcIndex * k_DataItemSize,
                                    length * sizeof(T));
            }
        }

        /// <summary>
        /// Copies the data from a source <see cref="NativeTensorArray"/> to a destination <see cref="NativeArray{T}"/> up to a given length starting from given indexes.
        /// </summary>
        /// <remarks>
        /// The source uses <c>k_DataItemSize</c> bytes per element. The destination uses <c>sizeof(T)</c>. Use when transferring tensor data to a <c>NativeArray</c> for use with Unity Jobs.
        /// </remarks>
        /// <param name="src">The array to copy from.</param>
        /// <param name="srcIndex">The index of the first element to copy from.</param>
        /// <param name="dst">The array to copy to.</param>
        /// <param name="dstIndex">The index of the first element to copy to.</param>
        /// <param name="length">The number of elements.</param>
        /// <typeparam name="T">The data type of the elements.</typeparam>
        /// <exception cref="ArgumentException">Thrown if the given indexes and length are out of bounds of the source or destination array.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when indexes or length are invalid.</exception>
        /// <example>
        /// <para>Copy tensor data to a NativeArray to use with Unity Jobs.</para>
        /// <code lang="cs"><![CDATA[
        /// using (var src = new NativeTensorArray(64))
        /// {
        ///     src.Set(0, 1.0f);
        ///     var dst = new NativeArray<float>(64, Allocator.Temp);
        ///     NativeTensorArray.Copy(src, 0, dst, 0, 64);
        ///     dst.Dispose();
        /// }
        /// ]]></code>
        /// </example>
        public static unsafe void Copy<T>(NativeTensorArray src, int srcIndex, NativeArray<T> dst, int dstIndex, int length) where T : unmanaged
        {
            if (length == 0)
                return;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CheckCopyArguments(src.Length * k_DataItemSize, srcIndex * k_DataItemSize, dst.Length * sizeof(T), dstIndex * sizeof(T), length * sizeof(T));
#endif
            void* srcPtr = src.RawPtr;
            void* dstPtr = dst.GetUnsafeReadOnlyPtr<T>();
            UnsafeUtility.MemCpy((byte*)dstPtr + dstIndex * sizeof(T),
                                 (byte*)srcPtr + srcIndex * k_DataItemSize,
                                 length * sizeof(T));
        }

        /// <summary>
        /// Copies the data from a source managed array to a destination <see cref="NativeTensorArray"/> up to a given length starting from given indexes.
        /// </summary>
        /// <remarks>
        /// The source uses <c>sizeof(T)</c> bytes per element. The destination uses <c>k_DataItemSize</c>. Use when uploading managed data into a tensor buffer.
        /// </remarks>
        /// <param name="src">The array to copy from.</param>
        /// <param name="srcIndex">The index of the first element to copy from.</param>
        /// <param name="dst">The array to copy to.</param>
        /// <param name="dstIndex">The index of the first element to copy to.</param>
        /// <param name="length">The number of elements.</param>
        /// <typeparam name="T">The data type of the elements.</typeparam>
        /// <exception cref="ArgumentException">Thrown if the given indexes and length are out of bounds of the source or destination array.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when indexes or length are invalid.</exception>
        /// <example>
        /// <para>Upload managed array data into a tensor buffer.</para>
        /// <code lang="cs"><![CDATA[
        /// float[] src = { 1.0f, 2.0f, 3.0f };
        /// using (var dst = new NativeTensorArray(64))
        /// {
        ///     NativeTensorArray.Copy(src, 0, dst, 0, 3);
        /// }
        /// ]]></code>
        /// </example>
        public static unsafe void Copy<T>(T[] src, int srcIndex, NativeTensorArray dst, int dstIndex, int length) where T : unmanaged
        {
            if (length == 0)
                return;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CheckCopyArguments(src.Length * sizeof(T), srcIndex * sizeof(T), dst.Length * k_DataItemSize, dstIndex * k_DataItemSize, length * sizeof(T));
#endif
            fixed (void* srcPtr = &src[0])
            {
                void* dstPtr = dst.RawPtr;
                UnsafeUtility.MemCpy((byte*)dstPtr + dstIndex * k_DataItemSize,
                                     (byte*)srcPtr + srcIndex * sizeof(T),
                                     length * sizeof(T));
            }
        }

        /// <summary>
        /// Copies the data from a source <see cref="NativeArray{T}"/> to a destination <see cref="NativeTensorArray"/> up to a given length starting from given indexes.
        /// </summary>
        /// <remarks>
        /// The source uses <c>sizeof(T)</c> bytes per element. The destination uses <c>k_DataItemSize</c>. Use when uploading job output or other <c>NativeArray</c> data into a tensor buffer.
        /// </remarks>
        /// <param name="src">The array to copy from.</param>
        /// <param name="srcIndex">The index of the first element to copy from.</param>
        /// <param name="dst">The array to copy to.</param>
        /// <param name="dstIndex">The index of the first element to copy to.</param>
        /// <param name="length">The number of elements.</param>
        /// <typeparam name="T">The data type of the elements.</typeparam>
        /// <exception cref="ArgumentException">Thrown if the given indexes and length are out of bounds of the source or destination array.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when indexes or length are invalid.</exception>
        /// <example>
        /// <para>Upload NativeArray data into a tensor buffer.</para>
        /// <code lang="cs"><![CDATA[
        /// var src = new NativeArray<float>(new[] { 1.0f, 2.0f, 3.0f }, Allocator.Temp);
        /// using (var dst = new NativeTensorArray(64))
        /// {
        ///     NativeTensorArray.Copy(src, 0, dst, 0, 3);
        /// }
        /// src.Dispose();
        /// ]]></code>
        /// </example>
        public static unsafe void Copy<T>(NativeArray<T> src, int srcIndex, NativeTensorArray dst, int dstIndex, int length) where T : unmanaged
        {
            if (length == 0)
                return;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CheckCopyArguments(src.Length * sizeof(T), srcIndex * sizeof(T), dst.Length * k_DataItemSize, dstIndex * k_DataItemSize, length * sizeof(T));
#endif
            void* srcPtr = src.GetUnsafeReadOnlyPtr<T>();
            void* dstPtr = dst.RawPtr;
            UnsafeUtility.MemCpy((byte*)dstPtr + dstIndex * k_DataItemSize,
                                 (byte*)srcPtr + srcIndex * sizeof(T),
                                 length * sizeof(T));
        }

        /// <summary>
        /// Copies the data from a source read-only <see cref="NativeArray{T}"/> to a destination <see cref="NativeTensorArray"/> up to a given length starting from given indexes.
        /// </summary>
        /// <remarks>
        /// The source uses <c>sizeof(T)</c> bytes per element. The destination uses <c>k_DataItemSize</c>. Use when uploading read-only job output into a tensor buffer.
        /// </remarks>
        /// <param name="src">The array to copy from.</param>
        /// <param name="srcIndex">The index of the first element to copy from.</param>
        /// <param name="dst">The array to copy to.</param>
        /// <param name="dstIndex">The index of the first element to copy to.</param>
        /// <param name="length">The number of elements.</param>
        /// <typeparam name="T">The data type of the elements.</typeparam>
        /// <exception cref="ArgumentException">Thrown if the given indexes and length are out of bounds of the source or destination array.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when indexes or length are invalid.</exception>
        /// <example>
        /// <para>Upload read-only NativeArray data into a tensor buffer.</para>
        /// <code lang="cs"><![CDATA[
        /// var src = new NativeArray<float>(new[] { 1.0f, 2.0f, 3.0f }, Allocator.Temp);
        /// using (var dst = new NativeTensorArray(64))
        /// {
        ///     NativeTensorArray.Copy(src.AsReadOnly(), 0, dst, 0, 3);
        /// }
        /// src.Dispose();
        /// ]]></code>
        /// </example>
        public static unsafe void Copy<T>(NativeArray<T>.ReadOnly src, int srcIndex, NativeTensorArray dst, int dstIndex, int length) where T : unmanaged
        {
            if (length == 0)
                return;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CheckCopyArguments(src.Length * sizeof(T), srcIndex * sizeof(T), dst.Length * k_DataItemSize, dstIndex * k_DataItemSize, length * sizeof(T));
#endif
            void* srcPtr = src.GetUnsafeReadOnlyPtr<T>();
            void* dstPtr = dst.RawPtr;
            UnsafeUtility.MemCpy((byte*)dstPtr + dstIndex * k_DataItemSize,
                                 (byte*)srcPtr + srcIndex * sizeof(T),
                                 length * sizeof(T));
        }

        /// <summary>
        /// Copies the data from a source <see cref="NativeTensorArray"/> to a destination byte array up to a given length starting from given offsets.
        /// </summary>
        /// <remarks>
        /// Performs a raw byte copy. Use when you need to transfer a specific byte range without element-type conversion. Offsets and length are in bytes.
        /// </remarks>
        /// <param name="src">The array to copy from.</param>
        /// <param name="srcByteIndex">The offset of the first element to copy from.</param>
        /// <param name="dst">The array to copy to.</param>
        /// <param name="dstByteIndex">The offset to copy to in the destination array.</param>
        /// <param name="lengthInBytes">The number of bytes to copy.</param>
        /// <exception cref="ArgumentException">Thrown if the given indexes and length are out of bounds of the source or destination array.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when indexes or length are invalid.</exception>
        /// <example>
        /// <para>Copy a raw byte range from a tensor buffer to a byte array.</para>
        /// <code lang="cs"><![CDATA[
        /// using (var src = new NativeTensorArray(64))
        /// {
        ///     src.Set(0, 1.0f);
        ///     byte[] dst = new byte[256];
        ///     NativeTensorArray.BlockCopy(src, 0, dst, 0, 256);
        /// }
        /// ]]></code>
        /// </example>
        public static unsafe void BlockCopy(NativeTensorArray src, int srcByteIndex, byte[] dst, int dstByteIndex, int lengthInBytes)
        {
             if (lengthInBytes == 0)
                return;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CheckCopyArguments(src.Length * k_DataItemSize, srcByteIndex, dst.Length, dstByteIndex, lengthInBytes);
#endif

            fixed (void* dstPtr = &dst[0])
            {
                void* srcPtr = src.RawPtr;
                UnsafeUtility.MemCpy((byte*)dstPtr + dstByteIndex,
                                     (byte*)srcPtr + srcByteIndex,
                                     lengthInBytes);
            }
        }

        /// <summary>
        /// Copies the data from a source byte array to a destination <see cref="NativeTensorArray"/> up to a given length starting from given offsets.
        /// </summary>
        /// <remarks>
        /// Performs a raw byte copy. Use when you need to transfer a specific byte range without element-type conversion. Offsets and length are in bytes.
        /// </remarks>
        /// <param name="src">The array to copy from.</param>
        /// <param name="srcByteIndex">The offset to copy from.</param>
        /// <param name="dst">The array to copy to.</param>
        /// <param name="dstByteIndex">The offset to copy to in the destination array.</param>
        /// <param name="lengthInBytes">The number of bytes to copy.</param>
        /// <exception cref="ArgumentException">Thrown if the given indexes and length are out of bounds of the source or destination array.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when indexes or length are invalid.</exception>
        /// <example>
        /// <para>Copy a raw byte range from a byte array into a tensor buffer.</para>
        /// <code lang="cs"><![CDATA[
        /// byte[] src = new byte[256];
        /// using (var dst = new NativeTensorArray(64))
        /// {
        ///     NativeTensorArray.BlockCopy(src, 0, dst, 0, 256);
        /// }
        /// ]]></code>
        /// </example>
        public static unsafe void BlockCopy(byte[] src, int srcByteIndex, NativeTensorArray dst, int dstByteIndex, int lengthInBytes)
        {
            if (lengthInBytes == 0)
                return;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CheckCopyArguments(src.Length, srcByteIndex, dst.Length * k_DataItemSize, dstByteIndex, lengthInBytes);
#endif

            fixed (void* srcPtr = &src[0])
            {
                void* dstPtr = dst.RawPtr;
                UnsafeUtility.MemCpy((byte*)dstPtr + dstByteIndex,
                                     (byte*)srcPtr + srcByteIndex,
                                     lengthInBytes);
            }
        }
    }
}
