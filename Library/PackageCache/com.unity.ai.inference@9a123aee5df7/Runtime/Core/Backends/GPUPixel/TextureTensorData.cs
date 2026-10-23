using UnityEngine;
using UnityEngine.Assertions;
using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Experimental.Rendering;
using static Unity.InferenceEngine.ShaderPropertyID;
using System.Threading.Tasks;

namespace Unity.InferenceEngine
{
    /// <summary>
    /// Represents the data storage for a 'Tensor' as a render texture, for backends that use GPU pixel shaders.
    ///
    /// Sentis packs the tensor data into the pixels of an RGBA float4 texture.
    ///
    /// Sentis chooses a single tensor dimension as the blocked axis, across which data is chunked in float4 blocks.
    ///
    /// Tensor dimensions don't map directly to texture dimensions.
    /// Sentis creates the texture with dimensions large enough to fit all the data and pixel shaders index the data
    /// based on both the tensor and texture dimensions (see example below).
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom("Unity.Sentis")]
    public class TextureTensorData : ITensorData
    {
        //
        // Rationale and formula for mapping from a tensor to a texture, given the following constraints / considerations:
        //
        // a) we limit ourselves to 2D textures, and these can usually be allocated with 4 channels per texel, with max texel sizes of float4.
        // b) we have limits on 2D textures width and height, and the maximum size available for us to use is usually achieved if we choose
        // a square dimension for some hardware "limit" of a dimension, for a total texel num of limit^2.
        // c) tensors have usually more than 2 dimensions.
        // d) tensors can have non square shapes, sometimes with very large ratios between dimension sizes, eg 1xn for the last 2 dims
        // (where "last" means the dim with the shortest and/or unit stride).
        // e) ML convolutions are a mix of spatial pooling using a "true" convolution on the inner dimensions (ie width, height of the tensor)
        // followed by summing across all channels. Typically the bottleneck is in the summation across channels.
        //
        // Let a tensor T be of shape (N, C, H, W).
        //
        // First suppose we choose axis 1 (the axis of size C, the channel dimension) as the dimension along which to group 4 slices of this dimension
        // into 4 channels of a single texel. This is because of e), where kernel footprint (halo) are usually much smaller than the channel dimension
        // across which we must also sum (and thus fetch the data for a single result), so a texel read can pack 4 channel values which can be summed
        // together after weight multiplications.
        //
        // Denote this new chunked tensor ChunkedT and note its shape as (N, ceil(C/4), H, W).
        //
        // Imagine now a 2D texture storing this ChunkedT where all dimensions after the inner 2 (ie after the ones with sizes H and W) are
        // folded / flattened into the H dimension.
        //
        // Denote this new texture TexFromChunkedT and let its dimensions be factor*H x W, where "factor" is TBD.
        //
        // Denote a multidimensional index of the original tensor T to be (n, k, y, x).
        // Denote a corresponding linear (1D) pixel offset inside the TexFromChunkedT texture by texIdx.
        //
        // A way to calculate that texIdx could be:
        //
        //   texIdx = (((n)*ceil(C/4) + k/4)*H + y)*W + x
        //
        // Here we see that "y" strides by W, but then each single increment on the channel dimension becomes k/4 (k is divided by 4 and truncated)
        // and strides by H*W, and finally, increments in the batch dimension (of size N in the original tensor) stride by ceil(C/4) * H * W
        // since ceil(C/4) is the channel dimension size (channel number) divided by 4 (as we pack 4 of them per texel).
        // (Note also that since ceil is used, masking/padding is required when channel number is not divisible by 4)
        //
        // Since we said we folded all dimensions except the inner / last 2, we could thus imagine the texture having a height of:
        //
        //   N * ceil(C/4) * H
        //
        // Finally, because of point (b) above, we can take the total number of 4-channel texels required,
        //
        //   N * ceil(C/4) * H * W := texelsRequired
        //
        // and take the square root of the NextPowerOfTwo of texelsRequired to get a more robust and appropriate size for our texture,
        // along with having quicker access indices calculations. This thus address points b) and d).
        // We can fix our FinalTexWidth to this value,
        //
        //   FinalTexWidth = Sqrt(NextPowerOfTwo(texelsRequired))
        //
        // and have FinalTexHeight be calculated from texelsRequired,
        //
        //   FinalTexHeight = ceil(texelsRequired / FinalTexWidth),
        //
        // height being sized to make up for all the space required.
        //
        // Denote the final texture by FinalTex, and let its dimensions be FinalTexHeight X FinalTexWidth.
        //
        // The texel coordinates FinalTex.x and FinalTex.y and the selected channel corresponding to (n, k, y, x) (the later indexing the original tensor T)
        // finally become
        //
        //   texIdx = (((n)*ceil(C/4) + k/4)*H + y)*W + x
        //
        //   FinalTex.x = texIdx % FinalTexWidth
        //   FinalTex.y = texIdx / FinalTexWidth
        //   FinalTexChannel = k % 4,
        //
        // so we have
        //
        //   FinalTex(FinalTex.x, FinalTex.y)[FinalTexChannel] = T(n, k, y, x)
        //
        //
        //                                     /-------\
        //                                 /-------\   |
        //                             /-------\   |   |
        //                         /-------\   | ---------> channel = k % 4
        // y = texIdx / finalW <-- |   x   |   |---/
        //                         |       |---/
        //                         \-------/
        //                             |
        //                             +--> x = texIdx % finalW
        //
        // (where finalW is FinalTexWidth and (x,y,channel) are (FinalTex.x, FinalTex.y, FinalTexChannel)).
        //
        bool m_IsDisposed;
        RenderTexture m_BufferAsTexture;
        int m_WidthShift;
        int m_WidthMask;

        DataType m_DataType;
        TensorShape m_Shape;
        TensorShape m_BlockedShape;
        int m_BlockAxis;
        int m_DimAxis;
        int m_DimAxisDiv4;
        int m_StrideAxis;
        int[] m_BlockedAxisDiv4RemainderMask;

        /// <summary>
        /// Returns the backing texture storing the tensor data.
        /// </summary>
        public RenderTexture bufferAsTexture => m_BufferAsTexture;
        /// <summary>
        /// Returns the power in the power of two width of the backing texture.
        /// </summary>
        public int widthShift => m_WidthShift;
        /// <summary>
        /// Returns the width of the texture - 1 for efficient masking in shaders.
        /// </summary>
        public int widthMask => m_WidthMask;

        /// <summary>
        /// Returns the data type of the associated tensor.
        /// </summary>
        public DataType dataType => m_DataType;
        /// <summary>
        /// Returns the shape of the associated tensor.
        /// </summary>
        public TensorShape shape => m_Shape;
        /// <summary>
        /// Returns the shape of the tensor with the blocked axis divided by 4.
        /// </summary>
        public TensorShape blockedShape => m_BlockedShape;
        /// <summary>
        /// Returns the axis of the tensor which is blocked.
        ///
        /// It is possible to block on negative axes by considering a tensor of shape (d0, d1 ... dn) as one of shape (1, 1, .... 1, d0, d1 ... dn).
        ///
        /// Thus negative axis values do not count from the back of the shape as elsewhere.
        /// </summary>
        public int blockAxis => m_BlockAxis;
        /// <summary>
        /// The size of the blocked axis in the original tensor shape (when not blocked).
        /// </summary>
        public int dimAxis => m_DimAxis;
        /// <summary>
        /// The size of the blocked axis in the blocked tensor shape, i.e. dimAxisDiv4 = ceil(dimAxis / 4).
        /// </summary>
        public int dimAxisDiv4 => m_DimAxisDiv4;
        /// <summary>
        /// A 4 int mask with 1 or 0 in each component to indicate whether the last blocked slice along the blocked axis has a valid entry or not.
        /// eg. if dimAxis % 4 = ceil(dimAxis/4)*4 - dimAxis = 3, blockedAxisDiv4RemainderMask = {1, 1, 1, 0};
        /// </summary>
        public int[] blockedAxisDiv4RemainderMask => m_BlockedAxisDiv4RemainderMask;
        /// <summary>
        /// The size of the stride of the blocked axis.
        /// </summary>
        public int strideAxis => m_StrideAxis;

        static int MaxTextureSize => Mathf.Min(SystemInfo.maxTextureSize, 16384);

        static RenderTexture CreateRenderTexture(int width, int height, RenderTextureFormat renderTextureFormat)
        {
            var renderTexture = new RenderTexture(width, height, 0, renderTextureFormat);
            renderTexture.Create();
            return renderTexture;
        }

        /// <summary>
        /// Initializes and returns an instance of `TextureTensorData` with given shape and blocked axis. A `RenderTexture` is allocated to the correct size.
        /// </summary>
        /// <param name="dataType">The data type of the tensor.</param>
        /// <param name="shape">The (unblocked) shape of the tensor.</param>
        /// <param name="axis">The axis on which to block the shape.</param>
        /// <param name="clearOnInit">Whether to zero the data on allocation. The default value is `false`.</param>
        public TextureTensorData(DataType dataType, TensorShape shape, int axis, bool clearOnInit = false)
        {
            m_IsDisposed = false;
            m_BlockedAxisDiv4RemainderMask = new int[4];
            m_DataType = dataType;
            SetShape(shape, axis);

            if (shape.HasZeroDims())
                return;

            var numPixels = m_BlockedShape.length;
            CalculateTextureDimensions(numPixels, out var newWidthShift, out var width, out var height);
            m_WidthShift = newWidthShift;
            m_WidthMask = (1 << widthShift) - 1;
            Logger.AssertIsTrue(width <= MaxTextureSize && height <= MaxTextureSize, "Tensor of shape {0} is too big to be allocated as a TextureTensorData", m_Shape);

            m_BufferAsTexture = CreateRenderTexture(width, height, dataType == DataType.Int ? RenderTextureFormat.ARGBInt : RenderTextureFormat.ARGBFloat);

            if (clearOnInit)
            {
                var previousActiveRT = RenderTexture.active;
                RenderTexture.active = m_BufferAsTexture;
                GL.Clear(true, true, Color.clear);
                RenderTexture.active = previousActiveRT;
            }
        }

        internal void SetShape(TensorShape newShape, int newBlockedAxis)
        {
            m_Shape = newShape;
            m_BlockAxis = newBlockedAxis;
            m_BlockedShape = newShape;
            if (blockAxis >= 0)
            {
                m_DimAxis = newShape[newBlockedAxis];
                m_StrideAxis = newShape.Strides(newBlockedAxis);
                m_DimAxisDiv4 = ComputeHelper.IDivC(m_DimAxis, 4);
                m_BlockedShape[newBlockedAxis] = m_DimAxisDiv4;
                int remainder = m_DimAxisDiv4 * 4 - m_DimAxis;
                int leadingOneCount = 4 - remainder;
                for (var i = 0; i < leadingOneCount; i++)
                {
                    m_BlockedAxisDiv4RemainderMask[i] = 1;
                }
                for (var i = leadingOneCount; i < 4; i++)
                {
                    m_BlockedAxisDiv4RemainderMask[i] = 0;
                }
            }
            else
            {
                m_DimAxis = 1;
                m_StrideAxis = newShape.length;
                m_DimAxisDiv4 = 1;
                m_BlockedAxisDiv4RemainderMask[0]
                    = m_BlockedAxisDiv4RemainderMask[1]
                    = m_BlockedAxisDiv4RemainderMask[2]
                    = m_BlockedAxisDiv4RemainderMask[3] = 1;
                // TODO TOCHECK,
                // Since nothing is 4-packed (the "blocked axis" has element stride = total shape length!)
                // should be:
                //m_BlockedAxisDiv4RemainderMask[0] = 1;
                //m_BlockedAxisDiv4RemainderMask[1]
                //    = m_BlockedAxisDiv4RemainderMask[2]
                //    = m_BlockedAxisDiv4RemainderMask[3] = 0;
            }
        }

        internal bool IsLayoutIdentical(TensorShape newShape, int newBlockedAxis)
        {
            if (newBlockedAxis >= 0)
            {
                var newDimAxis = newShape[newBlockedAxis];
                return newShape.Strides(newBlockedAxis) == strideAxis && (newDimAxis == dimAxis || (newDimAxis % 4 == 0 && dimAxis % 4 == 0));
            }

            return newShape.length == strideAxis && dimAxis == 1;
        }

        static void CalculateTextureDimensions(int numPixels, out int widthShift, out int width, out int height)
        {
            widthShift = ComputeHelper.CalculateWidthShift(numPixels);
            width = Mathf.Min(numPixels, 1 << widthShift);
            height = ComputeHelper.IDivC(numPixels, width);
        }

        /// <summary>
        /// Finalizes the `TextureTensorData`.
        /// </summary>
        ~TextureTensorData()
        {
            if (m_BufferAsTexture == null)
                return;
            if (m_IsDisposed)
                return;

            D.LogWarning($"Found unreferenced, but undisposed TextureTensorData which might lead to GPU resource leak");
        }

        /// <summary>
        /// Disposes of the `TextureTensorData` and any associated memory.
        /// </summary>
        public void Dispose()
        {
            if (!m_IsDisposed)
            {
                // In emergency shutdown situations active RenderTexture might be the one we are trying to release
                if (RenderTexture.active == m_BufferAsTexture)
                    RenderTexture.active = null;

                if (m_BufferAsTexture)
                {
#if UNITY_EDITOR
                    UnityEngine.Object.DestroyImmediate(m_BufferAsTexture);
#else
                    UnityEngine.Object.Destroy(m_BufferAsTexture);
#endif
                    m_BufferAsTexture = null;
                }
            }

            m_IsDisposed = true;
            System.GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        public bool IsReadbackRequestDone()
        {
            return true;
        }

        /// <inheritdoc/>
        public void ReadbackRequest() {}

        /// <inheritdoc/>
        public Task<bool> ReadbackRequestAsync()
        {
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public void CompleteAllPendingOperations() { }

        /// <inheritdoc/>
        public void Upload<T>(NativeArray<T> data, int srcCount) where T : unmanaged
        {
            if (data.Length == 0)
                return;

            var numItemToCopy = shape.length;
            var numItemAvailableInData = data.Length;

            Assert.IsTrue(numItemToCopy <= numItemAvailableInData);

            var numPixels = ComputeHelper.IDivC(numItemToCopy, 4);
            CalculateTextureDimensions(numPixels, out var linearWidthShift, out var linearWidth, out var linearHeight);

            if (dataType == DataType.Float)
            {
                var texture = new Texture2D(linearWidth, linearHeight, GraphicsFormat.R32G32B32A32_SFloat, TextureCreationFlags.None);

                unsafe
                {
                    void* dataPtr = (byte*)data.GetUnsafeReadOnlyPtr();
                    var dest = texture.GetRawTextureData<float>();
                    UnsafeUtility.MemCpy(dest.GetUnsafePtr(), dataPtr, sizeof(float) * srcCount);
                }

                texture.Apply();

                var func = new PixelFunc("Hidden/Sentis/TextureTensorDataUpload");
                func.EnableKeyword("TensorFloat");

                func.SetTexture(k_ID_Xptr, texture);
                func.SetInt(k_TensorPropertiesX.k_ID_WidthShift, linearWidthShift);
                func.SetInt(k_TensorPropertiesX.k_ID_WidthMask, (1 << linearWidthShift) - 1);
                func.SetTensorBlockStride(k_TensorPropertiesO, this);
                func.Dispatch(this);

#if UNITY_EDITOR
                UnityEngine.Object.DestroyImmediate(texture);
#else
                UnityEngine.Object.Destroy(texture);
#endif
            }
            else if (dataType == DataType.Int) // integers have to be split to upper 2 bytes and lower 2 bytes to avoid denormals and nan floats being set to zero when uploading
            {
                var textureLower = new Texture2D(linearWidth, linearHeight, GraphicsFormat.R32G32B32A32_SFloat, TextureCreationFlags.None);
                var textureUpper = new Texture2D(linearWidth, linearHeight, GraphicsFormat.R32G32B32A32_SFloat, TextureCreationFlags.None);

                unsafe
                {
                    void* dataPtr = (byte*)data.GetUnsafeReadOnlyPtr();
                    var job = new GPUPixelBurstJobs.IntBytesAsFloatJob
                    {
                        src = (int*)dataPtr,
                        destLower = textureLower.GetRawTextureData<float>(),
                        destUpper = textureUpper.GetRawTextureData<float>()
                    };
                    var jobHandle = job.Schedule(srcCount, 32);
                    jobHandle.Complete();
                }

                textureLower.Apply();
                textureUpper.Apply();

                var func = new PixelFunc("Hidden/Sentis/TextureTensorDataUpload");
                func.EnableKeyword("TensorInt");

                func.SetTexture(k_ID_Xptr, textureLower);
                func.SetInt(k_TensorPropertiesX.k_ID_WidthShift, linearWidthShift);
                func.SetInt(k_TensorPropertiesX.k_ID_WidthMask, (1 << linearWidthShift) - 1);
                func.SetTexture(k_ID_Sptr, textureUpper);
                func.SetInt(k_TensorPropertiesS.k_ID_WidthShift, linearWidthShift);
                func.SetInt(k_TensorPropertiesS.k_ID_WidthMask, (1 << linearWidthShift) - 1);
                func.SetTensorBlockStride(k_TensorPropertiesO, this);
                func.Dispatch(this);

#if UNITY_EDITOR
                UnityEngine.Object.DestroyImmediate(textureLower);
                UnityEngine.Object.DestroyImmediate(textureUpper);
#else
                UnityEngine.Object.Destroy(textureLower);
                UnityEngine.Object.Destroy(textureUpper);
#endif
            }
            else
            {
                throw new NotSupportedException($"Upload is not supported on GPUPixel for {dataType}");
            }
        }

        #if UNITY_2023_2_OR_NEWER
        /// <inheritdoc/>
        public async Awaitable<NativeArray<T>> DownloadAsync<T>(int dstCount) where T : unmanaged
        {
            await Awaitable.MainThreadAsync();
            return Download<T>(dstCount);
        }
        #endif

        /// <inheritdoc/>
        public NativeArray<T> Download<T>(int dstCount) where T : unmanaged
        {
            Assert.IsTrue(maxCapacity >= dstCount);

            var count = shape.length;
            if (count == 0)
                return new NativeArray<T>();

            ProfilerMarkers.TextureTensorDataDownload.Begin();

            var numValues = shape.length;
            var numPixels = ComputeHelper.IDivC(numValues, 4);
            CalculateTextureDimensions(numPixels, out var linearWidthShift, out var linearWidth, out var linearHeight);

            var previousActiveRT = RenderTexture.active;
            var data = new NativeArray<T>(count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            if (dataType == DataType.Float)
            {
                bool gotTemporary = false;
                var linearRenderTexture = bufferAsTexture;

                if (strideAxis != 1 || (dimAxis % 4 != 0 && count != dimAxis)) // if the data is not already linear we have to make it linear before downloading
                {
                    gotTemporary = true;
                    linearRenderTexture = RenderTexture.GetTemporary(linearWidth, linearHeight, 0, RenderTextureFormat.ARGBFloat);

                    var func = new PixelFunc("Hidden/Sentis/TextureTensorDataDownload");
                    func.EnableKeyword("TensorFloat");
                    func.SetTensor(k_TensorPropertiesX, this);
                    func.SetTensorBlockStride(k_TensorPropertiesX, this);
                    func.SetInt(k_TensorPropertiesO.k_ID_WidthShift, linearWidthShift);
                    func.Dispatch(linearRenderTexture);
                }

                var texture = new Texture2D(linearRenderTexture.width, linearRenderTexture.height, TextureFormat.RGBAFloat, false);
                texture.hideFlags = HideFlags.HideAndDontSave;

                RenderTexture.active = linearRenderTexture;
                texture.ReadPixels(new Rect(0, 0, linearRenderTexture.width, linearRenderTexture.height), 0, 0);
                texture.Apply();

                if (gotTemporary)
                    RenderTexture.ReleaseTemporary(linearRenderTexture);

                unsafe
                {
                    void* dataPtr = (byte*)data.GetUnsafeReadOnlyPtr();
                    var src = texture.GetRawTextureData<float>();
                    UnsafeUtility.MemCpy(dataPtr, src.GetUnsafePtr(), sizeof(float) * numValues);
                }
#if UNITY_EDITOR
                UnityEngine.Object.DestroyImmediate(texture);
#else
                UnityEngine.Object.Destroy(texture);
#endif
            }
            else if (dataType == DataType.Int) // integers have to be split to upper 2 bytes and lower 2 bytes to avoid denormals and nan floats being set to zero when downloading
            {
                var linearRenderTexture = RenderTexture.GetTemporary(linearWidth, linearHeight, 0, RenderTextureFormat.ARGBFloat);
                RenderTexture.active = linearRenderTexture;

                var textureLower = new Texture2D(linearWidth, linearHeight, TextureFormat.RGBAFloat, false);
                var textureUpper = new Texture2D(linearWidth, linearHeight, TextureFormat.RGBAFloat, false);

                var func = new PixelFunc("Hidden/Sentis/TextureTensorDataDownload");
                func.SetTensor(k_TensorPropertiesX, this);
                func.SetTensorBlockStride(k_TensorPropertiesX, this);
                func.SetInt(k_TensorPropertiesO.k_ID_WidthShift, linearWidthShift);

                // lower bytes then upper bytes
                for (var i = 0; i < 2; i++)
                {
                    func.EnableKeyword(i == 1 ? "TensorIntUpper" : "TensorIntLower");
                    func.Dispatch(linearRenderTexture);

                    var texture = i == 1 ? textureUpper : textureLower;
                    texture.hideFlags = HideFlags.HideAndDontSave;
                    texture.ReadPixels(new Rect(0, 0, linearWidth, linearHeight), 0, 0);
                    texture.Apply();
                }

                RenderTexture.ReleaseTemporary(linearRenderTexture);

                unsafe
                {
                    void* dataPtr = (byte*)data.GetUnsafeReadOnlyPtr();

                    var job = new GPUPixelBurstJobs.FloatBytesAsIntJob
                    {
                        srcLower = textureLower.GetRawTextureData<float>(),
                        srcUpper = textureUpper.GetRawTextureData<float>(),
                        dest = (int*)dataPtr
                    };
                    var jobHandle = job.Schedule(numValues, 32);
                    jobHandle.Complete();
                }
#if UNITY_EDITOR
                UnityEngine.Object.DestroyImmediate(textureLower);
                UnityEngine.Object.DestroyImmediate(textureUpper);
#else
                UnityEngine.Object.Destroy(textureLower);
                UnityEngine.Object.Destroy(textureUpper);
#endif
            }
            else
            {
                throw new NotSupportedException($"Download is not supported on GPUPixel for {dataType}");
            }

            RenderTexture.active = previousActiveRT;

            ProfilerMarkers.TextureTensorDataDownload.End();
            return data;
        }

        /// <summary>
        /// Moves the tensor into GPU memory on the `GPUPixel` back end device.
        /// </summary>
        /// <param name="X">The tensor to move to the compute backend.</param>
        /// <param name="blockAxis">Which axis to block the tensor shape on.</param>
        /// <param name="clearOnInit">Whether to zero the data on pinning. The default value is `false`.</param>
        /// <returns>The pinned `TextureTensorData`.</returns>
        public static TextureTensorData Pin(Tensor X, int blockAxis, bool clearOnInit = false)
        {
            Assert.IsTrue(X.dataType == DataType.Float || X.dataType == DataType.Int, "Unsupported DataType");
            var onDevice = X.dataOnBackend;
            if (onDevice == null)
            {
                X.AdoptTensorData(new TextureTensorData(X.dataType, X.shape, blockAxis, clearOnInit), disposePrevious: true, disposeIsDelayed: false);
                return X.dataOnBackend as TextureTensorData;
            }

            if (onDevice is TextureTensorData textureTensorData)
            {
                var newTextureTensorData = textureTensorData.SwitchBlockedLayout(X.shape, blockAxis);
                X.AdoptTensorData(newTextureTensorData, disposePrevious: true, disposeIsDelayed: false);
                return X.dataOnBackend as TextureTensorData;
            }

            // TODO as IConvertibleToTextureTensorData
            var dataOnBackend = new TextureTensorData(X.dataType, X.shape, blockAxis, clearOnInit: false);
            dataOnBackend.Upload<int>(onDevice.Download<int>(X.count), X.count);
            X.AdoptTensorData(dataOnBackend, disposePrevious: true, disposeIsDelayed: false);

            return X.dataOnBackend as TextureTensorData;
        }

        /// <summary>
        /// Returns a `TextureTensorData` with the same data as this but with a new layout.
        /// If the layout of the data hasn't changed this will be the same object,
        /// otherwise we need to run a shader to perform the layout switch.
        /// </summary>
        internal TextureTensorData SwitchBlockedLayout(TensorShape newShape, int newBlockedAxis)
        {
            if (IsLayoutIdentical(newShape, newBlockedAxis))
            {
                SetShape(newShape, newBlockedAxis);
                return this;
            }

            var textureTensorData = new TextureTensorData(m_DataType, newShape, newBlockedAxis, false);
            var func = new PixelFunc("Hidden/Sentis/LayoutSwitchBlockedAxis");
            func.EnableKeyword(dataType == DataType.Float ? "TENSORFLOAT" : "TENSORINT");
            func.SetTensor(k_TensorPropertiesX, this);
            func.SetTensorBlockStride(k_TensorPropertiesX, this);
            func.SetTensorBlockStride(k_TensorPropertiesO, textureTensorData);
            func.Dispatch(textureTensorData);
            return textureTensorData;
        }

        /// <inheritdoc/>
        public int maxCapacity => shape.length;

        /// <inheritdoc/>
        public BackendType backendType => BackendType.GPUPixel;

        /// <summary>
        /// Returns a string that represents the `TextureTensorData`.
        /// </summary>
        /// <returns>The summary string of the `TextureTensorData`.</returns>
        public override string ToString()
        {
            return $"GPU<TextureTensorData>:{shape} texture: {bufferAsTexture}";
        }
    }
}
