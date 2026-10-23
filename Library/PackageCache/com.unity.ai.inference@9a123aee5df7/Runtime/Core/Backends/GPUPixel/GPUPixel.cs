using UnityEngine;
using System.Runtime.CompilerServices;
using UnityEngine.Assertions;
using System;
using Unity.Mathematics;
using static Unity.InferenceEngine.ShaderPropertyID;

[assembly: InternalsVisibleTo("Unity.InferenceEngine.RuntimeTests")]
[assembly: InternalsVisibleTo("Unity.InferenceEngine.EditorTests")]

namespace Unity.InferenceEngine
{
    readonly struct PixelFunc
    {
        readonly Material m_Material;

        public PixelFunc(string name)
        {
            m_Material = PixelShaderSingleton.Instance.FindMaterial(name);
        }

        public Material material { get => m_Material; }

        public void SetBool(int nameID, bool value)
        {
            m_Material.SetInt(nameID, value ? 1 : 0);
        }

        public void SetInt(int nameID, int value)
        {
            m_Material.SetInteger(nameID, value);
        }

        public void SetFloatArray(int nameID, float[] values)
        {
            m_Material.SetFloatArray(nameID, values);
        }

        public void SetFloat(int nameID, float value)
        {
            m_Material.SetFloat(nameID, value);
        }

        public void SetVector(int nameID, Vector4 value)
        {
            m_Material.SetVector(nameID, value);
        }

        public void SetTexture(int nameID, Texture value)
        {
            m_Material.SetTexture(nameID, value);
        }

        public void EnableKeyword(string keyword)
        {
            m_Material.EnableKeyword(keyword);
        }

        public void DisableKeyword(string keyword)
        {
            m_Material.DisableKeyword(keyword);
        }

        public void SetKeyword(string keyword, bool state)
        {
            if (state)
                m_Material.EnableKeyword(keyword);
            else
                m_Material.DisableKeyword(keyword);
        }

        internal void SetTensor(TensorProperties tensorProperties, TextureTensorData pinX)
        {
            m_Material.SetTexture(tensorProperties.k_ID_Ptr, pinX.bufferAsTexture);
            m_Material.SetInteger(tensorProperties.k_ID_WidthMask, pinX.widthMask);
            m_Material.SetInteger(tensorProperties.k_ID_WidthShift, pinX.widthShift);
        }

        internal void SetTensorBlockStride(TensorProperties tensorProperties, TextureTensorData pinX)
        {
            m_Material.SetInteger(tensorProperties.k_ID_StrideAxis, pinX.strideAxis);
            m_Material.SetInteger(tensorProperties.k_ID_DimAxis, pinX.dimAxis);
            m_Material.SetInteger(tensorProperties.k_ID_DimBlocked, pinX.dimAxisDiv4);
        }

        public void Dispatch(TextureTensorData pinO)
        {
            m_Material.SetInteger(k_TensorPropertiesO.k_ID_WidthShift, pinO.widthShift);
            m_Material.SetInteger(k_ID_LengthO, pinO.shape.length);
            Graphics.Blit(null, pinO.bufferAsTexture, m_Material);
        }

        public void Dispatch(TextureTensorData pinO, int passNum)
        {
            m_Material.SetInteger(k_TensorPropertiesO.k_ID_WidthShift, pinO.widthShift);
            m_Material.SetInteger(k_ID_LengthO, pinO.shape.length);
            Graphics.Blit(null, pinO.bufferAsTexture, m_Material, pass: passNum);
        }

        public void Dispatch(RenderTexture renderTexture)
        {
            Graphics.Blit(null, renderTexture, m_Material);
        }
    }

    static class PixelShaderHelper
    {
        static readonly float[] k_ScratchPadFloat8 = new float[8];
        static readonly float[] k_ScratchPadFloat4 = new float[4];

        public static unsafe void SetInt4(this PixelFunc func, int nameID, int* ptr)
        {
            for (var i = 0; i < 4; i++)
                k_ScratchPadFloat4[i] = ptr[i];

            func.SetFloatArray(nameID, k_ScratchPadFloat4);
        }

        public static unsafe void SetInt4(this PixelFunc func, int nameID, int[] intArr)
        {
            for (var i = 0; i < 4; i++)
                k_ScratchPadFloat4[i] = intArr[i];

            func.SetFloatArray(nameID, k_ScratchPadFloat4);
        }

        public static unsafe void SetInt8(this PixelFunc func, int nameID, int* ptr, int numElements = 8)
        {
            for (var i = 0; i < numElements; i++)
                k_ScratchPadFloat8[i] = ptr[i];

            func.SetFloatArray(nameID, k_ScratchPadFloat8);
        }

        public static void SetShapeStrides(this PixelFunc func, TensorProperties properties, TensorShape shape)
        {
            unsafe
            {
                var pShape = stackalloc int[TensorShape.maxRank];
                var pStrides = stackalloc int[TensorShape.maxRank];
                OpsUtils.PinTensorShapeStrides(shape, pShape, pStrides);
                func.SetInt8(properties.k_ID_Shape, pShape);
                func.SetInt8(properties.k_ID_Strides, pStrides);
            }
            func.SetInt(properties.k_ID_Rank, shape.rank);
        }

        // SetTensorShape and/or Strides above always sets 8-sized arrays on the GPU, and these arrays are valid
        // for rank elements starting from the end of the array - ie from the highest address - ie Shape[maxRank-1],
        // up to (inclusively) Shape[maxRank - 1 - rank + 1] and all other heading elements are invalid (garbage).
        // With the *CompactedAtHead versions, dimension numbers from 0 to rank-1 can directly be used to index
        // these shape and strides arrays.
        public static unsafe void SetTensorStridesCompactedAtHead(this PixelFunc func, int strideNameID, TensorShape shape)
        {
            int* pStrides = stackalloc int[shape.rank];
            OpsUtils.PinTensorStridesCompact(shape, pStrides);
            func.SetInt8(strideNameID, pStrides, numElements: shape.rank);
        }

        public static unsafe void SetTensorShapesCompactedAtHead(this PixelFunc fn, int strideNameID, TensorShape shape)
        {
            // Note the following is defensive (UnsafeGetPtr shouldn't be called with TensorShape.maxRank) and we add this
            // because this is an unsafe scope, but rank-0 is not supported and we should never be called in that case.
            int rank = Math.Max(shape.rank, 1);
            int* compactShape = shape.UnsafeGetPtr(TensorShape.maxRank - rank);
            fn.SetInt8(strideNameID, compactShape, numElements: shape.rank);
        }

        public static void SetShape(this PixelFunc func, int nameID, TensorShape shape)
        {
            for (var i = 0; i < 8; i++)
            {
                k_ScratchPadFloat8[i] = i < shape.rank ? shape[-1 - i] : 1;
            }

            func.SetFloatArray(nameID, k_ScratchPadFloat8);
        }

        public static void SetStrides(this PixelFunc func, int nameID, TensorShape shape)
        {
            var stride = 1;
            var rank = shape.rank;
            for (var i = 0; i < rank; i++)
            {
                var dim = shape[rank - 1 - i];
                k_ScratchPadFloat8[i] = dim == 1 ? 0 : stride;
                stride *= dim;
            }

            Array.Clear(k_ScratchPadFloat8, rank, 8 - rank);

            func.SetFloatArray(nameID, k_ScratchPadFloat8);
        }
    }

    /// <summary>
    /// Represents a GPUPixel backend ops.
    /// </summary>
    class GPUPixelBackend : IBackend
    {
        /// <summary>
        /// Initializes and returns an instance of `GPUPixelBackend`.
        /// </summary>
        public GPUPixelBackend() { }
        /// <inheritdoc/>
        public void Dispose() { }

        /// <inheritdoc/>
        public BackendType backendType => BackendType.GPUPixel;

        TensorClassPool<Tensor<float>> m_TensorFloatPool = new TensorClassPool<Tensor<float>>();
        TensorClassPool<Tensor<int>> m_TensorIntPool = new TensorClassPool<Tensor<int>>();

        Tensor AllocTensor(TensorShape shape, DataType dataType)
        {
            switch (dataType)
            {
                case DataType.Float:
                    var tensorf = m_TensorFloatPool.AdoptFromPool();
                    if (tensorf == null)
                        tensorf = new Tensor<float>(shape, data: null);
                    tensorf.shape = shape;
                    tensorf.count = shape.length;
                    return tensorf;
                case DataType.Int:
                    var tensori = m_TensorIntPool.AdoptFromPool();
                    if (tensori == null)
                        tensori = new Tensor<int>(shape, data: null);
                    tensori.shape = shape;
                    tensori.count = shape.length;
                    return tensori;
                default:
                    throw new NotImplementedException();
            }
        }

        void ReleaseTensor(Tensor tensor)
        {
            if (tensor == null)
                return;
            tensor.dataOnBackend.Dispose();
            tensor.dataOnBackend = null;
            switch (tensor.dataType)
            {
                case DataType.Float:
                    m_TensorFloatPool.ReleaseToPool(tensor as Tensor<float>);
                    break;
                case DataType.Int:
                    m_TensorIntPool.ReleaseToPool(tensor as Tensor<int>);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        internal Tensor<float> AllocTensorFloat(TensorShape shape) => (Tensor<float>)AllocTensor(shape, DataType.Float);
        internal void ReleaseTensorFloat(Tensor tensor) => ReleaseTensor(tensor);

        /// <summary>
        /// Pins the tensor as `TextureTensorData` on any axis (choose last).
        /// </summary>
        static TextureTensorData PinBlockAny(Tensor X, bool clearOnInit = false)
        {
            if (X.dataOnBackend is TextureTensorData textureTensorData)
                return textureTensorData;
            return TextureTensorData.Pin(X, X.shape.rank - 1, clearOnInit);
        }

        /// <summary>
        /// Pins the tensor as TextureTensorData on any axis except `nonBlockAxis`. (Choose last unless avoid, else one before last.)
        /// </summary>
        static TextureTensorData PinBlockOther(Tensor X, int nonBlockAxis, bool clearOnInit = false)
        {
            if (X.dataOnBackend is TextureTensorData textureTensorData)
                if (textureTensorData.blockAxis != nonBlockAxis)
                    return textureTensorData;
            var axis = nonBlockAxis == X.shape.rank - 1 ? X.shape.rank - 2 : X.shape.rank - 1;
            return TextureTensorData.Pin(X, axis, clearOnInit);
        }

        /// <summary>
        /// Pins the tensor X blocking along the same axis as a given other TextureTensorData
        /// This can be used to block an output tensor along the same axis as an input tensor for an op
        /// </summary>
        static TextureTensorData PinAsSame(Tensor X, TextureTensorData other, bool clearOnInit = false)
        {
            return TextureTensorData.Pin(X, X.shape.rank - other.shape.rank + other.blockAxis, clearOnInit);
        }

        /// <summary>
        /// Pin tensors A and B along the same axis, the blocking for A takes priority in case neither tensor is pinned or
        /// both tensors are pinned
        /// </summary>
        static void PinBothSame(Tensor A, Tensor B)
        {
            var pinA = A.dataOnBackend as TextureTensorData;
            var pinB = B.dataOnBackend as TextureTensorData;
            if (pinA == null == pinB is null)
                pinA = PinBlockAny(A);
            else if (pinB != null)
                pinA = PinAsSame(A, pinB);
            PinAsSame(B, pinA);
        }

        /// <inheritdoc/>
        public void Cast(Tensor<int> X, Tensor<float> O)
        {
            var pinX = PinBlockAny(X);
            var pinO = PinAsSame(O, pinX, false);

            var func = new PixelFunc("Hidden/Sentis/Cast");
            func.EnableKeyword("IntToFloat");
            func.SetTensor(k_TensorPropertiesX, pinX);
            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void Cast(Tensor<float> X, Tensor<int> O)
        {
            var pinX = PinBlockAny(X);
            var pinO = PinAsSame(O, pinX, false);

            var func = new PixelFunc("Hidden/Sentis/Cast");
            func.EnableKeyword("FloatToInt");
            func.SetTensor(k_TensorPropertiesX, pinX);
            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void Cast(Tensor<short> X, Tensor<float> O)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void DequantizeLinear(Tensor<byte> X, Tensor<float> O, float scale, byte zeroPoint)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void MemClear(Tensor O)
        {
            if (O.dataType == DataType.Float)
                MemSet(O as Tensor<float>, 0f);
            else
                MemSet(O as Tensor<int>, 0);
        }

        /// <inheritdoc/>
        public void MemSet(Tensor<float> O, float value)
        {
            var func = new PixelFunc("Hidden/Sentis/ConstantOfShape");
            var pinO = PinBlockAny(O, false);
            func.EnableKeyword("TensorFloat");
            func.SetFloat(k_ID_memValueFloat, value);
            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void MemSet(Tensor<int> O, int value)
        {
            var func = new PixelFunc("Hidden/Sentis/ConstantOfShape");
            var pinO = PinBlockAny(O, false);
            func.EnableKeyword("TensorInt");
            func.SetInt(k_ID_memValueInt, value);
            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void MatMul(Tensor<float> X, Tensor<float> Y, Tensor<float> O)
        {
            var xShape = X.shape.rank == 1 ? new TensorShape(1, X.shape[0]) : X.shape;
            var yShape = Y.shape.rank == 1 ? new TensorShape(Y.shape[0], 1) : Y.shape;
            var oShape = X.shape.rank > 1 && Y.shape.rank > 1 ? O.shape : xShape.MatMul(yShape);

            var func = new PixelFunc("Hidden/Sentis/MatMul");

            var pinO = TextureTensorData.Pin(O, Y.shape.rank == 1 ? -1 : O.shape.rank - 1);
            var pinA = TextureTensorData.Pin(X, X.shape.rank - 1);
            var pinB = TextureTensorData.Pin(Y, Y.shape.rank == 1 ? -1 : Y.shape.rank - 1);
            if (xShape != pinA.shape)
                pinA.SetShape(xShape, xShape.rank - 1);
            if (yShape != pinB.shape)
                pinB.SetShape(yShape, yShape.rank - 1);
            if (oShape != pinO.shape)
                pinO.SetShape(oShape, oShape.rank - 1);

            func.SetTensor(k_TensorPropertiesA, pinA);
            func.SetTensor(k_TensorPropertiesB, pinB);

            func.SetShape(k_ID_DimO, pinO.blockedShape);
            func.SetStrides(k_TensorPropertiesA.k_ID_Strides, pinA.blockedShape);
            func.SetStrides(k_TensorPropertiesB.k_ID_Strides, pinB.blockedShape);
            func.SetInt(k_TensorPropertiesA.k_ID_DimAxis, pinA.dimAxis);
            func.SetInt(k_ID_Kdiv4, pinA.dimAxisDiv4);

            func.Dispatch(pinO);

            if (X.shape != pinA.shape)
                pinA.SetShape(X.shape, X.shape.rank - 1);
            if (Y.shape != pinB.shape)
                pinB.SetShape(Y.shape, Y.shape.rank == 1 ? -1 : Y.shape.rank - 1);
            if (O.shape != pinO.shape)
                pinO.SetShape(O.shape, Y.shape.rank == 1 ? -1 : O.shape.rank - 1);
        }

        /// <inheritdoc/>
        public void MatMul2D(Tensor<float> X, Tensor<float> Y, Tensor<float> O, bool xTranspose, bool yTranspose)
        {
            var func = new PixelFunc("Hidden/Sentis/Gemm");

            var pinX = TextureTensorData.Pin(X, xTranspose ? 0 : 1);
            var pinW = TextureTensorData.Pin(Y, yTranspose ? 0 : 1);
            var pinO = TextureTensorData.Pin(O, 1);
            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetTensor(k_TensorPropertiesW, pinW);

            if (xTranspose)
                func.EnableKeyword("TRANSPOSE_X");
            if (yTranspose)
                func.EnableKeyword("TRANSPOSE_W");
            func.SetInt(k_ID_M, pinO.blockedShape[0]);
            func.SetInt(k_ID_K, pinX.dimAxis);
            func.SetInt(k_ID_Kdiv4, pinX.dimAxisDiv4);
            func.SetInt(k_ID_Ndiv4, pinO.dimAxisDiv4);

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void Dense(Tensor<float> X, Tensor<float> W, Tensor<float> B, Tensor<float> O, Layers.FusableActivation fusedActivation)
        {
            var func = new PixelFunc("Hidden/Sentis/Dense");

            var pinO = TextureTensorData.Pin(O, O.shape.rank - 1);
            var pinX = TextureTensorData.Pin(X, X.shape.rank - 1);
            var pinW = TextureTensorData.Pin(W, W.shape.rank - 1);
            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetTensor(k_TensorPropertiesW, pinW);

            if (B != null)
            {
                var pinB = TextureTensorData.Pin(B, B.shape.rank - 1);
                func.SetTensor(k_TensorPropertiesB, pinB);
                func.EnableKeyword("Dense");
            }

            func.SetInt(k_TensorPropertiesO.k_ID_DimBlocked, pinO.dimAxisDiv4);
            func.SetInt(k_TensorPropertiesX.k_ID_DimAxis, pinX.dimAxis);
            func.SetInt(k_TensorPropertiesX.k_ID_DimBlocked, pinX.dimAxisDiv4);
            func.SetInt(k_TensorPropertiesW.k_ID_DimBlocked, pinW.dimAxisDiv4);

            if (fusedActivation == Layers.FusableActivation.Relu)
                func.EnableKeyword("Relu");

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void DenseBatched(Tensor<float> X, Tensor<float> W, Tensor<float> B, Tensor<float> O, Layers.FusableActivation fusedActivation)
        {
            var Otmp = AllocTensor(O.shape, O.dataType) as Tensor<float>;
            if (fusedActivation == Layers.FusableActivation.Relu)
            {
                MatMul(X, W, O);
                Add(O, B, Otmp);
                Relu(Otmp, O);
            }
            else
            {
                MatMul(X, W, Otmp);
                Add(Otmp, B, O);
            }
            ReleaseTensor(Otmp);
        }

        /// <inheritdoc/>
        public void Conv(Tensor<float> X, Tensor<float> K, Tensor<float> B, Tensor<float> O, int groups, Span<int> strides, Span<int> pads, Span<int> dilations, Layers.FusableActivation fusedActivation)
        {
            var isDepthwise = K.shape[0] == groups && K.shape[1] == 1;

            var pinX = TextureTensorData.Pin(X, 1);
            var pinK = TextureTensorData.Pin(K, isDepthwise ? 0 : 1);
            var pinO = TextureTensorData.Pin(O, 1);

            var numSpatialDims = X.shape.rank - 2;

            PixelFunc func;

            if (isDepthwise)
            {
                func = new PixelFunc("Hidden/Sentis/DepthwiseConv");
            }
            else if (groups > 1)
            {
                func = new PixelFunc("Hidden/Sentis/GroupedConv");
                func.SetInt(k_ID_X_channels, pinX.shape[1]);
                func.SetInt(k_ID_O_channels, pinO.shape[1]);
                func.SetInt(k_ID_K_channelsDivGroupDiv4, pinK.dimAxisDiv4);
            }
            else
            {
                func = new PixelFunc("Hidden/Sentis/Conv");
                func.SetInt(k_ID_X_channels, pinX.shape[1]);
            }

            if (numSpatialDims == 1)
                func.EnableKeyword("CONV1D");
            else if (numSpatialDims == 2)
                func.EnableKeyword("CONV2D");
            else
                func.EnableKeyword("CONV3D");

            func.SetInt(k_ID_O_width, pinO.shape[-1]);
            func.SetInt(k_ID_X_width, pinX.shape[-1]);
            func.SetInt(k_ID_K_width, pinK.shape[-1]);
            func.SetInt(k_ID_StrideX, strides[numSpatialDims - 1]);
            func.SetInt(k_ID_PadX, pads[numSpatialDims - 1]);
            func.SetInt(k_ID_DilationX, dilations[numSpatialDims - 1]);
            func.SetInt(k_ID_O_channelsDiv4, pinO.dimAxisDiv4);
            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetInt(k_ID_X_channelsDiv4, pinX.dimAxisDiv4);
            func.SetTensor(k_TensorPropertiesK, pinK);
            if (B != null)
            {
                func.EnableKeyword("USEBIAS");
                var pinB = TextureTensorData.Pin(B, 0);
                func.SetTensor(k_TensorPropertiesB, pinB);
            }
            func.SetInt(k_ID_Groups, groups);

            if (numSpatialDims > 1)
            {
                func.SetInt(k_ID_O_height, pinO.shape[-2]);
                func.SetInt(k_ID_X_height, pinX.shape[-2]);
                func.SetInt(k_ID_K_height, pinK.shape[-2]);
                func.SetInt(k_ID_StrideY, strides[numSpatialDims - 2]);
                func.SetInt(k_ID_PadY, pads[numSpatialDims - 2]);
                func.SetInt(k_ID_DilationY, dilations[numSpatialDims - 2]);
            }

            if (numSpatialDims > 2)
            {
                func.SetInt(k_ID_O_depth, pinO.shape[-3]);
                func.SetInt(k_ID_X_depth, pinX.shape[-3]);
                func.SetInt(k_ID_K_depth, pinK.shape[-3]);
                func.SetInt(k_ID_StrideZ, strides[numSpatialDims - 3]);
                func.SetInt(k_ID_PadZ, pads[numSpatialDims - 3]);
                func.SetInt(k_ID_DilationZ, dilations[numSpatialDims - 3]);
            }

            if (fusedActivation == Layers.FusableActivation.Relu)
                func.EnableKeyword("RELU");

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void ConvTranspose(Tensor<float> X, Tensor<float> W, Tensor<float> B, Tensor<float> O, int groups, Span<int> strides, Span<int> pads, Span<int> dilations, Span<int> outputPadding, Layers.FusableActivation fusedActivation)
        {
            if (X.shape.rank > 5)
            {
                throw new NotImplementedException();
            }
            var numSpatialDims = X.shape.rank - 2;

            var func = new PixelFunc("Hidden/Sentis/ConvTranspose");

            var pinX = TextureTensorData.Pin(X, 1);
            var pinK = TextureTensorData.Pin(W, 0);
            if (B != null)
            {
                var pinB = TextureTensorData.Pin(B, 0);
                func.SetTensor(k_TensorPropertiesB, pinB);
                func.EnableKeyword("USEBIAS");
            }
            var pinO = TextureTensorData.Pin(O, 1);

            Span<int> lcmOfDilationStride = stackalloc int[numSpatialDims];

            if (numSpatialDims == 1)
                func.EnableKeyword("CONVTRANSPOSE1D");
            else if (numSpatialDims == 2)
                func.EnableKeyword("CONVTRANSPOSE2D");
            else
                func.EnableKeyword("CONVTRANSPOSE3D");

            int numOutputChannelsPerGroup = pinK.shape[1]; // should be == O.shape[1] / groups
            func.SetInt(k_ID_O_channelsPerGroup, numOutputChannelsPerGroup);
            {
                int numInputChannelsPerGroup = X.shape[1] / groups;
                func.SetInt(k_ID_X_channelsPerGroupDiv4Floor, numInputChannelsPerGroup / 4);
                func.SetInt(k_ID_X_channelsPerGroup, numInputChannelsPerGroup);
            }
            if (groups > 1)
                func.EnableKeyword("GROUPS_ENABLED");

            func.SetInt(k_ID_O_channelsDiv4, pinO.dimAxisDiv4); // ceil(pinK.shape[1]/4)
            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetInt(k_ID_X_channels, pinX.dimAxis);
            func.SetInt(k_ID_X_channelsDiv4, pinX.dimAxisDiv4);
            func.SetTensor(k_TensorPropertiesK, pinK);

            func.SetInt(k_ID_K_width, pinK.shape[-1]);
            func.SetInt(k_ID_X_width, pinX.shape[-1]);
            func.SetInt(k_ID_O_width, pinO.shape[-1]);
            func.SetInt(k_ID_StrideX, strides[numSpatialDims - 1]);
            func.SetInt(k_ID_PadX, dilations[numSpatialDims - 1] * (W.shape[-1] - 1) + 1 - pads[numSpatialDims - 1] - 1);
            func.SetInt(k_ID_PadOrigX, pads[numSpatialDims - 1]);
            func.SetInt(k_ID_DilationX, dilations[numSpatialDims - 1]);

            if (dilations[numSpatialDims - 1] > 1)
            {
                var gcdTmp = ComputeHelper.GCD(dilations[numSpatialDims - 1], strides[numSpatialDims - 1]);
                lcmOfDilationStride[numSpatialDims - 1] = Math.Abs(dilations[numSpatialDims - 1] * strides[numSpatialDims - 1]) / gcdTmp;

                if (dilations[numSpatialDims - 1] > strides[numSpatialDims - 1])
                    func.EnableKeyword("DILATIONX_GT_STRIDE");
                else
                    func.EnableKeyword("DILATIONX_GT_ONE_LTE_STRIDE");
            }
            else
            {
                lcmOfDilationStride[numSpatialDims - 1] = 1;
            }
            func.SetInt(k_ID_LCMOfStrideDilationDivStrideX, lcmOfDilationStride[numSpatialDims - 1] / strides[numSpatialDims - 1]);
            func.SetInt(k_ID_LCMOfStrideDilationX, lcmOfDilationStride[numSpatialDims - 1]);

            if (numSpatialDims > 1)
            {
                func.SetInt(k_ID_K_height, pinK.shape[-2]);
                func.SetInt(k_ID_X_height, pinX.shape[-2]);
                func.SetInt(k_ID_O_height, pinO.shape[-2]);
                func.SetInt(k_ID_StrideY, strides[numSpatialDims - 2]);
                func.SetInt(k_ID_PadY, dilations[numSpatialDims - 2] * (W.shape[-2] - 1) + 1 - pads[numSpatialDims - 2] - 1);
                func.SetInt(k_ID_PadOrigY, pads[numSpatialDims - 2]);
                func.SetInt(k_ID_DilationY, dilations[numSpatialDims - 2]);

                if (dilations[numSpatialDims - 2] > 1)
                {
                    var gcdTmp = ComputeHelper.GCD(dilations[numSpatialDims - 2], strides[numSpatialDims - 2]);
                    lcmOfDilationStride[numSpatialDims - 2] = Math.Abs(dilations[numSpatialDims - 2] * strides[numSpatialDims - 2]) / gcdTmp;

                    if (dilations[numSpatialDims - 2] > strides[numSpatialDims - 2])
                        func.EnableKeyword("DILATIONY_GT_STRIDE");
                    else
                        func.EnableKeyword("DILATIONY_GT_ONE_LTE_STRIDE");
                }
                else
                {
                    lcmOfDilationStride[numSpatialDims - 2] = 1;
                }
                func.SetInt(k_ID_LCMOfStrideDilationDivStrideY, lcmOfDilationStride[numSpatialDims - 2] / strides[numSpatialDims - 2]);
                func.SetInt(k_ID_LCMOfStrideDilationY, lcmOfDilationStride[numSpatialDims - 2]);
            }

            if (numSpatialDims > 2)
            {
                func.SetInt(k_ID_K_depth, pinK.shape[-3]);
                func.SetInt(k_ID_X_depth, pinX.shape[-3]);
                func.SetInt(k_ID_O_depth, pinO.shape[-3]);
                func.SetInt(k_ID_StrideZ, strides[numSpatialDims - 3]);
                func.SetInt(k_ID_PadZ, dilations[numSpatialDims - 3] * (W.shape[-3] - 1) + 1 - pads[numSpatialDims - 3] - 1);
                func.SetInt(k_ID_PadOrigZ, pads[numSpatialDims - 3]);
                func.SetInt(k_ID_DilationZ, dilations[numSpatialDims - 3]);

                if (dilations[numSpatialDims - 3] > 1)
                {
                    var gcdTmp = ComputeHelper.GCD(dilations[numSpatialDims - 3], strides[numSpatialDims - 3]);
                    lcmOfDilationStride[numSpatialDims - 3] = Math.Abs(dilations[numSpatialDims - 3] * strides[numSpatialDims - 3]) / gcdTmp;

                    if (dilations[numSpatialDims - 3] > strides[numSpatialDims - 3])
                        func.EnableKeyword("DILATIONZ_GT_STRIDE");
                    else
                        func.EnableKeyword("DILATIONZ_GT_ONE_LTE_STRIDE");
                }
                else
                {
                    lcmOfDilationStride[numSpatialDims - 3] = 1;
                }
                func.SetInt(k_ID_LCMOfStrideDilationDivStrideZ, lcmOfDilationStride[numSpatialDims - 3] / strides[numSpatialDims - 3]);
                func.SetInt(k_ID_LCMOfStrideDilationZ, lcmOfDilationStride[numSpatialDims - 3]);
            }


            if (fusedActivation == Layers.FusableActivation.Relu)
                func.EnableKeyword("RELU");

            func.Dispatch(pinO);
        }

        void Activation(Tensor<float> X, Tensor<float> O, string kernelName, float alpha = 0f, float beta = 0f)
        {
            var func = new PixelFunc("Hidden/Sentis/Activation");

            var pinX = PinBlockAny(X);
            var pinO = PinAsSame(O, pinX);

            func.SetFloat(k_ID_Alpha, alpha);
            func.SetFloat(k_ID_Beta, beta);

            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetTensorBlockStride(k_TensorPropertiesO, pinO);

            func.EnableKeyword(kernelName);

            func.Dispatch(pinO);
        }

        void Activation(Tensor<int> X, Tensor<int> O, string kernelName, int alpha = 0, int beta = 0)
        {
            var func = new PixelFunc("Hidden/Sentis/ActivationInt");

            var pinX = PinBlockAny(X);
            var pinO = PinAsSame(O, pinX);

            func.SetInt(k_ID_Alpha, alpha);
            func.SetInt(k_ID_Beta, beta);

            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetTensorBlockStride(k_TensorPropertiesO, pinO);

            func.EnableKeyword(kernelName);

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void Relu(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Relu");
        }

        /// <inheritdoc/>
        public void Relu6(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Relu6");
        }

        /// <inheritdoc/>
        public void Mish(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Mish");
        }

        /// <inheritdoc/>
        public void LeakyRelu(Tensor<float> X, Tensor<float> O, float alpha)
        {
            Activation(X, O, "LeakyRelu", alpha);
        }

        /// <inheritdoc/>
        public void Tanh(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Tanh");
        }

        /// <inheritdoc/>
        public void Softplus(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Softplus");
        }

        /// <inheritdoc/>
        public void Sigmoid(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Sigmoid");
        }

        /// <inheritdoc/>
        public void HardSigmoid(Tensor<float> X, Tensor<float> O, float alpha, float beta)
        {
            Activation(X, O, "HardSigmoid", alpha, beta);
        }

        /// <inheritdoc/>
        public void Elu(Tensor<float> X, Tensor<float> O, float alpha)
        {
            Activation(X, O, "Elu", alpha);
        }

        /// <inheritdoc/>
        public void Gelu(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Gelu");
        }

        /// <inheritdoc/>
        public void GeluFast(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "GeluFast");
        }

        /// <inheritdoc/>
        public void Shrink(Tensor<float> X, Tensor<float> O, float bias, float lambd)
        {
            Activation(X, O, "Shrink", bias, lambd);
        }

        /// <inheritdoc/>
        public void Selu(Tensor<float> X, Tensor<float> O, float alpha, float gamma)
        {
            Activation(X, O, "Selu", alpha, gamma);
        }

        /// <inheritdoc/>
        public void Swish(Tensor<float> X, Tensor<float> O, float alpha)
        {
            Activation(X, O, "Swish", alpha);
        }

        /// <inheritdoc/>
        public void Abs(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Abs");
        }

        /// <inheritdoc/>
        public void Abs(Tensor<int> X, Tensor<int> O)
        {
            Activation(X, O, "Abs");
        }

        /// <inheritdoc/>
        public void Neg(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Neg");
        }

        /// <inheritdoc/>
        public void Neg(Tensor<int> X, Tensor<int> O)
        {
            Activation(X, O, "Neg");
        }

        /// <inheritdoc/>
        public void Not(Tensor<int> X, Tensor<int> O)
        {
            Activation(X, O, "Not");
        }

        /// <inheritdoc/>
        public void Ceil(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Ceil");
        }

        /// <inheritdoc/>
        public void HardTanh(Tensor<float> X, Tensor<float> O, float minVal, float maxVal)
        {
            Activation(X, O, "HardTanh", minVal, maxVal);
        }

        /// <inheritdoc/>
        public void Clip(Tensor<float> X, Tensor<float> min, Tensor<float> max, Tensor<float> O)
        {
            var func = new PixelFunc("Hidden/Sentis/Clip");

            var pinX = PinBlockAny(X);
            var pinO = PinAsSame(O, pinX);

            if (min != null)
            {
                func.EnableKeyword("UseMin");
                var pinS = PinAsSame(min, pinX);
                func.SetTensor(k_TensorPropertiesS, pinS);
            }

            if (max != null)
            {
                func.EnableKeyword("UseMax");
                var pinB = PinAsSame(max, pinX);
                func.SetTensor(k_TensorPropertiesB, pinB);
            }

            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetTensorBlockStride(k_TensorPropertiesO, pinO);

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void Clip(Tensor<int> X, Tensor<int> min, Tensor<int> max, Tensor<int> O)
        {
            var func = new PixelFunc("Hidden/Sentis/Clip");

            func.EnableKeyword("ClipInt");

            var pinX = PinBlockAny(X);
            var pinO = PinAsSame(O, pinX);

            if (min != null)
            {
                func.EnableKeyword("UseMin");
                var pinS = PinAsSame(min, pinX);
                func.SetTensor(k_TensorPropertiesS, pinS);
            }

            if (max != null)
            {
                func.EnableKeyword("UseMax");
                var pinB = PinAsSame(max, pinX);
                func.SetTensor(k_TensorPropertiesB, pinB);
            }

            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetTensorBlockStride(k_TensorPropertiesO, pinO);

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void Floor(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Floor");
        }

        /// <inheritdoc/>
        public void Trunc(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Trunc");
        }

        /// <inheritdoc/>
        public void Round(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Round");
        }

        /// <inheritdoc/>
        public void Reciprocal(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Reciprocal");
        }

        /// <inheritdoc/>
        public void Square(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Square");
        }

        /// <inheritdoc/>
        public void Square(Tensor<int> X, Tensor<int> O)
        {
            Activation(X, O, "Square");
        }

        /// <inheritdoc/>
        public void Exp(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Exp");
        }

        /// <inheritdoc/>
        public void Expm1(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Expm1");
        }

        /// <inheritdoc/>
        public void Log(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Log");
        }

        /// <inheritdoc/>
        public void Log10(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Log10");
        }

        /// <inheritdoc/>
        public void Log1p(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Log1p");
        }

        /// <inheritdoc/>
        public void Log2(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Log2");
        }

        /// <inheritdoc/>
        public void Rsqrt(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Rsqrt");
        }

        /// <inheritdoc/>
        public void Sqrt(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Sqrt");
        }

        /// <inheritdoc/>
        public void Celu(Tensor<float> X, Tensor<float> O, float alpha)
        {
            Activation(X, O, "Celu", alpha);
        }

        /// <inheritdoc/>
        public void HardSwish(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "HardSwish");
        }

        /// <inheritdoc/>
        public void Softsign(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Softsign");
        }

        /// <inheritdoc/>
        public void ScalarMad(Tensor<float> X, Tensor<float> O, float s, float b)
        {
            var pinX = PinBlockAny(X);
            var pinO = PinAsSame(O, pinX, false);

            var func = new PixelFunc("Hidden/Sentis/ScalarMad");

            func.SetTensor(k_TensorPropertiesX, pinX);

            func.SetFloat(k_ID_s, s);
            func.SetFloat(k_ID_b, b);

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void ScalarMad(Tensor<int> X, Tensor<int> O, int s, int b)
        {
            var pinX = PinBlockAny(X);
            var pinO = PinAsSame(O, pinX, false);

            var func = new PixelFunc("Hidden/Sentis/ScalarMad");
            func.EnableKeyword("INT");

            func.SetTensor(k_TensorPropertiesX, pinX);

            func.SetInt(k_ID_sInt, s);
            func.SetInt(k_ID_bInt, b);

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void Sign(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Sign");
        }

        /// <inheritdoc/>
        public void Sign(Tensor<int> X, Tensor<int> O)
        {
            Activation(X, O, "Sign");
        }

        /// <inheritdoc/>
        public void ThresholdedRelu(Tensor<float> X, Tensor<float> O, float alpha)
        {
            Activation(X, O, "ThresholdedRelu", alpha);
        }

        /// <inheritdoc/>
        public void PRelu(Tensor<float> X, Tensor<float> S, Tensor<float> O)
        {
            Broadcast(X, S, O, "PRelu");
        }

        /// <inheritdoc/>
        public void And(Tensor<int> A, Tensor<int> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "And");
        }

        /// <inheritdoc/>
        public void Equal(Tensor<float> A, Tensor<float> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "Equal");
        }

        /// <inheritdoc/>
        public void Equal(Tensor<int> A, Tensor<int> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "EqualInt");
        }

        /// <inheritdoc/>
        public void NotEqual(Tensor<float> A, Tensor<float> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "NotEqual");
        }

        /// <inheritdoc/>
        public void NotEqual(Tensor<int> A, Tensor<int> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "NotEqualInt");
        }

        /// <inheritdoc/>
        public void Greater(Tensor<float> A, Tensor<float> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "Greater");
        }

        /// <inheritdoc/>
        public void Greater(Tensor<int> A, Tensor<int> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "GreaterInt");
        }

        /// <inheritdoc/>
        public void GreaterOrEqual(Tensor<float> A, Tensor<float> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "GreaterOrEqual");
        }

        /// <inheritdoc/>
        public void GreaterOrEqual(Tensor<int> A, Tensor<int> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "GreaterOrEqualInt");
        }

        /// <inheritdoc/>
        public void Less(Tensor<float> A, Tensor<float> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "Less");
        }

        /// <inheritdoc/>
        public void Less(Tensor<int> A, Tensor<int> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "LessInt");
        }

        /// <inheritdoc/>
        public void LessOrEqual(Tensor<float> A, Tensor<float> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "LessOrEqual");
        }

        /// <inheritdoc/>
        public void LessOrEqual(Tensor<int> A, Tensor<int> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "LessOrEqualInt");
        }

        /// <inheritdoc/>
        public void Or(Tensor<int> A, Tensor<int> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "Or");
        }

        /// <inheritdoc/>
        public void Xor(Tensor<int> A, Tensor<int> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "Xor");
        }

        /// <inheritdoc/>
        public void BitwiseAnd(Tensor<int> A, Tensor<int> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "BitwiseAnd");
        }

        /// <inheritdoc/>
        public void BitwiseOr(Tensor<int> A, Tensor<int> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "BitwiseOr");
        }

        /// <inheritdoc/>
        public void BitwiseXor(Tensor<int> A, Tensor<int> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "BitwiseXor");
        }

        /// <inheritdoc/>
        public void BitwiseNot(Tensor<int> X, Tensor<int> O)
        {
            Activation(X, O, "BitwiseNot");
        }

        /// <inheritdoc/>
        public void Where(Tensor<int> C, Tensor A, Tensor B, Tensor O)
        {
            PinBothSame(A, B);
            PinBothSame(A, C);
            var pinX = C.dataOnBackend as TextureTensorData;
            var pinA = A.dataOnBackend as TextureTensorData;
            var pinB = B.dataOnBackend as TextureTensorData;
            var pinO = PinAsSame(O, pinA, false);

            var func = new PixelFunc("Hidden/Sentis/Where");
            func.EnableKeyword(A.dataType == DataType.Int ? "WhereInt" : "WhereFloat");

            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetTensor(k_TensorPropertiesA, pinA);
            func.SetTensor(k_TensorPropertiesB, pinB);

            func.SetShape(k_ID_DimO, pinO.blockedShape);
            func.SetStrides(k_TensorPropertiesX.k_ID_Strides, pinX.blockedShape);
            func.SetStrides(k_TensorPropertiesA.k_ID_Strides, pinA.blockedShape);
            func.SetStrides(k_TensorPropertiesB.k_ID_Strides, pinB.blockedShape);

            func.SetInt(k_TensorPropertiesX.k_ID_DimAxis, pinX.dimAxis);
            func.SetInt(k_TensorPropertiesA.k_ID_DimAxis, pinA.dimAxis);
            func.SetInt(k_TensorPropertiesB.k_ID_DimAxis, pinB.dimAxis);

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void IsInf(Tensor<float> X, Tensor<int> O, bool detectNegative, bool detectPositive)
        {
            var func = new PixelFunc("Hidden/Sentis/IsInfNaN");
            func.EnableKeyword("IsInf");

            var pinX = PinBlockAny(X);
            var pinO = PinAsSame(O, pinX);

            func.SetInt(k_ID_detectNegative, detectNegative ? 1 : 0);
            func.SetInt(k_ID_detectPositive, detectPositive ? 1 : 0);

            func.SetTensor(k_TensorPropertiesX, pinX);
            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void IsNaN(Tensor<float> X, Tensor<int> O)
        {
            var func = new PixelFunc("Hidden/Sentis/IsInfNaN");
            func.EnableKeyword("IsNaN");

            var pinX = PinBlockAny(X);
            var pinO = PinAsSame(O, pinX);

            func.SetTensor(k_TensorPropertiesX, pinX);
            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void Acos(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Acos");
        }

        /// <inheritdoc/>
        public void Acosh(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Acosh");
        }

        /// <inheritdoc/>
        public void Asin(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Asin");
        }

        /// <inheritdoc/>
        public void Asinh(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Asinh");
        }

        /// <inheritdoc/>
        public void Atan(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Atan");
        }

        /// <inheritdoc/>
        public void Atanh(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Atanh");
        }

        /// <inheritdoc/>
        public void Cos(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Cos");
        }

        /// <inheritdoc/>
        public void Cosh(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Cosh");
        }

        /// <inheritdoc/>
        public void Sin(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Sin");
        }

        /// <inheritdoc/>
        public void Sinh(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Sinh");
        }

        /// <inheritdoc/>
        public void Tan(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Tan");
        }

        /// <inheritdoc/>
        public void Erf(Tensor<float> X, Tensor<float> O)
        {
            Activation(X, O, "Erf");
        }

        /// <inheritdoc/>
        public void Add(Tensor<float> A, Tensor<float> B, Tensor<float> O)
        {
            Broadcast(A, B, O, "Add");
        }

        /// <inheritdoc/>
        public void Add(Tensor<int> A, Tensor<int> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "AddInt");
        }

        /// <inheritdoc/>
        public void Atan2(Tensor<float> y, Tensor<float> x, Tensor<float> O)
        {
            Broadcast(y, x, O, "Atan2");
        }

        /// <inheritdoc/>
        public void Sub(Tensor<float> A, Tensor<float> B, Tensor<float> O)
        {
            Broadcast(A, B, O, "Sub");
        }

        /// <inheritdoc/>
        public void Sub(Tensor<int> A, Tensor<int> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "SubInt");
        }

        /// <inheritdoc/>
        public void Div(Tensor<float> A, Tensor<float> B, Tensor<float> O)
        {
            Broadcast(A, B, O, "Div");
        }

        /// <inheritdoc/>
        public void FloorDiv(Tensor<float> A, Tensor<float> B, Tensor<float> O)
        {
            Broadcast(A, B, O, "FloorDiv");
        }

        /// <inheritdoc/>
        public void TruncDiv(Tensor<float> A, Tensor<float> B, Tensor<float> O)
        {
            Broadcast(A, B, O, "TruncDiv");
        }

        /// <inheritdoc/>
        public void FloorDiv(Tensor<int> A, Tensor<int> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "FloorDivInt");
        }

        /// <inheritdoc/>
        public void TruncDiv(Tensor<int> A, Tensor<int> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "TruncDivInt");
        }

        /// <inheritdoc/>
        public void Pow(Tensor<float> A, Tensor<float> B, Tensor<float> O)
        {
            Broadcast(A, B, O, "PowFloatFloat");
        }

        /// <inheritdoc/>
        public void Pow(Tensor<float> A, Tensor<int> B, Tensor<float> O)
        {
            Broadcast(A, B, O, "PowFloatInt");
        }

        /// <inheritdoc/>
        public void Pow(Tensor<int> A, Tensor<float> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "PowIntFloat");
        }

        /// <inheritdoc/>
        public void Pow(Tensor<int> A, Tensor<int> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "PowIntInt");
        }

        /// <inheritdoc/>
        public void FMod(Tensor<float> A, Tensor<float> B, Tensor<float> O)
        {
            Broadcast(A, B, O, "FMod");
        }

        /// <inheritdoc/>
        public void FMod(Tensor<int> A, Tensor<int> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "FModInt");
        }

        /// <inheritdoc/>
        public void Mod(Tensor<float> A, Tensor<float> B, Tensor<float> O)
        {
            Broadcast(A, B, O, "Mod");
        }

        /// <inheritdoc/>
        public void Mod(Tensor<int> A, Tensor<int> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "ModInt");
        }

        /// <inheritdoc/>
        public void Mul(Tensor<float> A, Tensor<float> B, Tensor<float> O)
        {
            Broadcast(A, B, O, "Mul");
        }

        /// <inheritdoc/>
        public void Mul(Tensor<int> A, Tensor<int> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "MulInt");
        }

        /// <inheritdoc/>
        public void Min(Tensor<float> A, Tensor<float> B, Tensor<float> O)
        {
            Broadcast(A, B, O, "Min");
        }

        /// <inheritdoc/>
        public void Min(Tensor<int> A, Tensor<int> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "MinInt");
        }

        /// <inheritdoc/>
        public void Max(Tensor<float> A, Tensor<float> B, Tensor<float> O)
        {
            Broadcast(A, B, O, "Max");
        }

        /// <inheritdoc/>
        public void Max(Tensor<int> A, Tensor<int> B, Tensor<int> O)
        {
            Broadcast(A, B, O, "MaxInt");
        }

        /// <inheritdoc/>
        public void Expand(Tensor X, Tensor O)
        {
            var func = new PixelFunc("Hidden/Sentis/Expand");
            if (X.dataType == DataType.Int)
                func.EnableKeyword("INT");

            var pinX = PinBlockAny(X);
            var pinO = PinAsSame(O, pinX, false);

            func.SetTensor(k_TensorPropertiesX, pinX);

            func.SetShape(k_ID_DimO, pinO.blockedShape);
            func.SetStrides(k_TensorPropertiesX.k_ID_Strides, pinX.blockedShape);

            func.SetInt(k_TensorPropertiesX.k_ID_DimAxis, pinX.dimAxis);
            func.Dispatch(pinO);
        }

        void Broadcast<T>(ReadOnlySpan<T> inputs, Tensor O, string kernelName) where T : Tensor
        {
            Tensor curX = inputs[0];
            var normalization = 1.0f / inputs.Length;
            for (var t = 1; t < inputs.Length; t++)
            {
                if (t == inputs.Length - 1)
                {
                    Broadcast(curX, inputs[t], O, kernelName, t == 1 ? normalization : 1.0f, normalization);
                    if (t > 1)
                        ReleaseTensor(curX);
                }
                else
                {
                    var oTmp = AllocTensor(TensorShapeHelper.BroadcastShape(curX, inputs[t]), O.dataType);
                    Broadcast(curX, inputs[t], oTmp, kernelName, t == 1 ? normalization : 1.0f, normalization);
                    if (t > 1)
                        ReleaseTensor(curX);
                    curX = oTmp;
                }
            }
        }

        void Broadcast(Tensor A, Tensor B, Tensor O, string kernelName, float normalizationX = 0, float normalizationY = 0)
        {
            var isALarger = A.shape.length > B.shape.length;
            PinBothSame(isALarger ? A : B, isALarger ? B : A);
            var pinA = A.dataOnBackend as TextureTensorData;
            var pinB = B.dataOnBackend as TextureTensorData;
            var pinO = PinAsSame(O, pinA, false);

            var func = new PixelFunc("Hidden/Sentis/Broadcast");
            func.EnableKeyword(kernelName);

            func.SetTensor(k_TensorPropertiesA, pinA);
            func.SetTensor(k_TensorPropertiesB, pinB);

            func.SetShape(k_ID_DimO, pinO.blockedShape);
            func.SetStrides(k_TensorPropertiesA.k_ID_Strides, pinA.blockedShape);
            func.SetStrides(k_TensorPropertiesB.k_ID_Strides, pinB.blockedShape);

            func.SetInt(k_TensorPropertiesA.k_ID_DimAxis, pinA.dimAxis);
            func.SetInt(k_TensorPropertiesB.k_ID_DimAxis, pinB.dimAxis);

            func.SetFloat(k_ID_alpha, normalizationX);
            func.SetFloat(k_ID_beta, normalizationY);

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void SliceSet(Tensor X, Tensor O, int axis, int start, int step)
        {
            var tempO = AllocTensor(O.shape, O.dataType);
            MemCopy(O, tempO);
            unsafe
            {
                var startsLocal = stackalloc int[] { 0, 0, 0, 0, 0, 0, 0, 0 };
                var stepsLocal = stackalloc int[] { 1, 1, 1, 1, 1, 1, 1, 1 };
                axis = O.shape.Axis(axis);
                startsLocal[axis] = start;
                stepsLocal[axis] = step;
                SliceSet(tempO, X, O, startsLocal, stepsLocal);
            }
            ReleaseTensor(tempO);
        }

        unsafe void Slice(Tensor X, Tensor O, int* startsLocal, int* stepsLocal)
        {
            if (!(X.dataOnBackend is TextureTensorData))
            {
                // find axis that isn't sliced along
                for (var axis = X.shape.rank - 1; axis >= 0; axis--)
                {
                    if (X.shape[axis] == O.shape[axis] && startsLocal[axis] == 0 && stepsLocal[axis] == 1)
                    {
                        TextureTensorData.Pin(X, axis);
                        break;
                    }
                }
            }

            var pinX = PinBlockAny(X);
            var pinO = PinAsSame(O, pinX, false);

            var func = new PixelFunc("Hidden/Sentis/Slice");
            if (X.dataType == DataType.Int)
                func.EnableKeyword("INT");

            func.SetTensor(k_TensorPropertiesX, pinX);

            var isBlockwise = pinX.dimAxis == pinO.dimAxis && startsLocal[pinX.blockAxis] == 0 && stepsLocal[pinX.blockAxis] == 1;

            if (isBlockwise)
            {
                func.EnableKeyword("BLOCKWISE");
            }
            else
            {
                func.SetTensorBlockStride(k_TensorPropertiesO, pinO);
                func.SetTensorBlockStride(k_TensorPropertiesX, pinX);
            }

            var offset = 0;
            var rank = 0;
            int accShape = 1;
            int accStride = 1;
            int totalStride = 1;

            var size = stackalloc int[8] { 0, 0, 0, 0, 0, 0, 0, 0 };
            var stride = stackalloc int[8] { 0, 0, 0, 0, 0, 0, 0, 0 };

            var isPrevBlockedAxis = false;

            for (int i = O.shape.rank - 1; i >= 0; --i)
            {
                var isBlockedAxis = isBlockwise && i == pinO.blockAxis;

                int currShape = isBlockedAxis ? pinO.dimAxisDiv4 : O.shape[i];
                int currStride = totalStride * stepsLocal[i];
                offset += totalStride * startsLocal[i];
                totalStride *= isBlockedAxis ? pinX.dimAxisDiv4 : X.shape[i];

                if (!isPrevBlockedAxis && !isBlockedAxis && currStride == accShape * accStride)
                {
                    accShape = currShape * accShape;
                    continue;
                }

                size[rank] = accShape;
                stride[rank] = accStride;
                rank++;

                accShape = currShape;
                accStride = currStride;

                isPrevBlockedAxis = isBlockedAxis;
            }

            size[rank] = accShape;
            stride[rank] = accStride;
            rank++;

            func.SetInt8(k_ID_stride, stride);
            func.SetInt8(k_ID_size, size);
            func.SetInt(k_ID_offset, offset);
            func.SetInt(k_ID_rank, rank);

            func.Dispatch(pinO);
        }

        unsafe void SliceSet(Tensor X, Tensor values, Tensor O, int* startsLocal, int* stepsLocal)
        {
            // note we can't simplify this like on CPU and GPUCompute since the indexing has to be done on the output tensor
            // we can't just iterate through the indices in values, we have to iterate in O, then work out if the corresponding
            // indices in V are in our slice

            if (!(X.dataOnBackend is TextureTensorData))
            {
                // find axis that isn't sliced along
                for (var axis = X.shape.rank - 1; axis >= 0; axis--)
                {
                    if (X.shape[axis] == values.shape[axis] && startsLocal[axis] == 1 && stepsLocal[axis] == 1)
                    {
                        TextureTensorData.Pin(X, axis);
                        break;
                    }
                }
            }

            var pinX = PinBlockAny(X);
            var pinV = PinAsSame(values, pinX);
            var pinO = PinAsSame(O, pinX, false);

            var func = new PixelFunc("Hidden/Sentis/SliceSet");
            if (X.dataType == DataType.Int)
                func.EnableKeyword("INT");

            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetTensor(k_TensorPropertiesV, pinV);

            TensorShape vShape;

            if (pinX.dimAxis == pinV.dimAxis && startsLocal[pinX.blockAxis] == 1 && stepsLocal[pinX.blockAxis] == 1)
            {
                func.EnableKeyword("BLOCKWISE");
                func.SetShape(k_TensorPropertiesO.k_ID_Shape, pinO.blockedShape);
                func.SetShape(k_TensorPropertiesV.k_ID_Shape, pinV.blockedShape);
                vShape = pinV.blockedShape;
            }
            else
            {
                func.SetTensorBlockStride(k_TensorPropertiesO, pinO);
                func.SetTensorBlockStride(k_TensorPropertiesV, pinV);
                func.SetTensorBlockStride(k_TensorPropertiesX, pinX);
                func.SetShape(k_TensorPropertiesO.k_ID_Shape, pinO.shape);
                func.SetShape(k_TensorPropertiesV.k_ID_Shape, pinV.shape);
                vShape = pinV.shape;
            }

            var strideV = 1;
            var stridesV = stackalloc int[8];
            var starts = stackalloc int[8] { 0, 0, 0, 0, 0, 0, 0, 0 };
            var steps = stackalloc int[8] { 1, 1, 1, 1, 1, 1, 1, 1 };
            for (var i = 0; i < pinX.shape.rank; i++)
            {
                var axis = pinV.shape.rank - 1 - i;
                starts[i] = startsLocal[axis];
                steps[i] = stepsLocal[axis];
                stridesV[i] = strideV;
                strideV *= vShape[axis];
            }

            func.SetInt8(k_ID_Starts, starts);
            func.SetInt8(k_ID_Steps, steps);
            func.SetInt8(k_TensorPropertiesV.k_ID_Strides, stridesV);

            func.Dispatch(pinO);
        }

        unsafe void PrepareSliceLocal(ReadOnlySpan<int> starts, ReadOnlySpan<int> axes, ReadOnlySpan<int> steps, int* startsLocal, int* stepsLocal)
        {
            for (var i = 0; i < 8; i++)
            {
                stepsLocal[i] = 1;
            }

            for (var i = 0; i < starts.Length; i++)
            {
                var axis = axes[i];
                startsLocal[axis] = starts[i];
                stepsLocal[axis] = steps[i];
            }
        }

        /// <inheritdoc/>
        public void Slice(Tensor X, Tensor O, ReadOnlySpan<int> starts, ReadOnlySpan<int> axes, ReadOnlySpan<int> steps)
        {
            unsafe
            {
                var startsLocal = stackalloc int[TensorShape.maxRank];
                var stepsLocal = stackalloc int[TensorShape.maxRank];
                PrepareSliceLocal(starts, axes, steps, startsLocal, stepsLocal);
                Slice(X, O, startsLocal, stepsLocal);
            }
        }

        /// <inheritdoc/>
        public void SliceSet(Tensor X, Tensor values, Tensor O, ReadOnlySpan<int> starts, ReadOnlySpan<int> axes, ReadOnlySpan<int> steps)
        {
            unsafe
            {
                var startsLocal = stackalloc int[TensorShape.maxRank];
                var stepsLocal = stackalloc int[TensorShape.maxRank];
                PrepareSliceLocal(starts, axes, steps, startsLocal, stepsLocal);
                SliceSet(X, values, O, startsLocal, stepsLocal);
            }
        }

        /// <inheritdoc/>
        public void AsStrided(Tensor X, Tensor O, ReadOnlySpan<int> strides, int offset)
        {
            var pinX = PinBlockAny(X);

            unsafe
            {
                var blockAxis = 0;
                var hasFoundBlockAxis = false;

                for (var axis = O.shape.rank - 1; axis >= 0; axis--)
                {
                    if (pinX.dimAxis == O.shape[axis] && strides[axis] == pinX.strideAxis && offset == 0)
                    {
                        blockAxis = axis;
                        hasFoundBlockAxis = true;
                        break;
                    }
                }

                var isBlockwise = hasFoundBlockAxis;
                var strideOuter = pinX.strideAxis * pinX.dimAxis;
                var strideInner = pinX.strideAxis;

                var blockedStrides = stackalloc int[8] { 0, 0, 0, 0, 0, 0, 0, 0 };

                // check all axes are compatible with blocking on that axis
                for (var axis = 0; axis < O.shape.rank && isBlockwise; axis++)
                {
                    if (strides[axis] > strideInner)
                    {
                        // blocking still only works if the stride on this axis is a multiple of the outer stride
                        isBlockwise &= strides[axis] % strideOuter == 0;
                        // reduce the strides by the correct factor
                        blockedStrides[axis] = strides[axis] / pinX.dimAxis * pinX.dimAxisDiv4;
                    }
                    else if (strides[axis] < strideInner)
                    {
                        // blocking still only works if the inner stride is a multiple of the stride on this axis or the stride is 0
                        isBlockwise &= strides[axis] == 0 || strideInner % strides[axis] == 0;
                        blockedStrides[axis] = strides[axis];
                    }
                    else if (axis != blockAxis)
                    {
                        // blocking still only works if this is a degenerate axis
                        isBlockwise &= (O.shape[axis] == 1);
                        blockedStrides[axis] = strides[axis];
                    }
                }

                var pinO = isBlockwise ? TextureTensorData.Pin(O, blockAxis, false) : PinBlockAny(O, false);

                var func = new PixelFunc("Hidden/Sentis/Slice");
                if (X.dataType == DataType.Int)
                    func.EnableKeyword("INT");

                func.SetTensor(k_TensorPropertiesX, pinX);

                if (isBlockwise)
                {
                    func.EnableKeyword("BLOCKWISE");
                }
                else
                {
                    func.SetTensorBlockStride(k_TensorPropertiesO, pinO);
                    func.SetTensorBlockStride(k_TensorPropertiesX, pinX);
                }

                var rank = 0;
                int accShape = 1;
                int accStride = 1;

                var size = stackalloc int[8] { 0, 0, 0, 0, 0, 0, 0, 0 };
                var stride = stackalloc int[8] { 0, 0, 0, 0, 0, 0, 0, 0 };

                var isPrevBlockedAxis = false;

                for (int i = O.shape.rank - 1; i >= 0; --i)
                {
                    var isBlockedAxis = isBlockwise && i == pinO.blockAxis;

                    int currShape = isBlockedAxis ? pinO.dimAxisDiv4 : O.shape[i];
                    int currStride = isBlockwise ? blockedStrides[i] : strides[i];

                    if (!isPrevBlockedAxis && !isBlockedAxis && currStride == accShape * accStride)
                    {
                        accShape = currShape * accShape;
                        continue;
                    }

                    size[rank] = accShape;
                    stride[rank] = accStride;
                    rank++;

                    accShape = currShape;
                    accStride = currStride;

                    isPrevBlockedAxis = isBlockedAxis;
                }

                size[rank] = accShape;
                stride[rank] = accStride;
                rank++;

                func.SetInt8(k_ID_stride, stride);
                func.SetInt8(k_ID_size, size);
                func.SetInt(k_ID_offset, offset);
                func.SetInt(k_ID_rank, rank);

                func.Dispatch(pinO);
            }
        }

        void SoftmaxActivation(Tensor X, Tensor<float> O, int reduceAxis, string endKernelName)
        {
            var reduceOpShape = X.shape.Reduce(reduceAxis);
            var B = AllocTensor(reduceOpShape, DataType.Float);
            var S = AllocTensor(reduceOpShape, DataType.Float);

            reduceAxis = X.shape.Axis(reduceAxis);

            var pinX = PinBlockOther(X, nonBlockAxis: reduceAxis);
            var pinO = PinAsSame(O, pinX, false);
            var pinB = PinAsSame(B, pinX, false);
            var pinS = PinAsSame(S, pinX, false);

            var dimAxis = pinX.blockedShape[reduceAxis];
            var strideAxis = pinX.blockedShape.Strides(reduceAxis);

            // x_max = X.max(axis=1)
            {
                var func = new PixelFunc("Hidden/Sentis/Reduce");
                func.EnableKeyword("ReduceMax");
                func.SetTensor(k_TensorPropertiesX, pinX);
                func.SetInt(k_TensorPropertiesX.k_ID_StrideAxis, strideAxis);
                func.SetInt(k_TensorPropertiesX.k_ID_DimAxis, dimAxis);
                func.Dispatch(pinB);
            }
            // e_x_sum = Sum[exp(x[:,c] - x_max[:]), c]
            {
                var func = new PixelFunc("Hidden/Sentis/ReduceExpBias");
                func.SetTensor(k_TensorPropertiesX, pinX);
                func.SetTensor(k_TensorPropertiesB, pinB);
                func.SetInt(k_TensorPropertiesX.k_ID_StrideAxis, strideAxis);
                func.SetInt(k_TensorPropertiesX.k_ID_DimAxis, dimAxis);
                func.Dispatch(pinS);
            }
            {
                var func = new PixelFunc("Hidden/Sentis/Softmax");
                func.EnableKeyword(endKernelName);
                func.SetTensor(k_TensorPropertiesX, pinX);
                func.SetTensor(k_TensorPropertiesB, pinB);
                func.SetTensor(k_TensorPropertiesS, pinS);
                func.SetInt(k_TensorPropertiesX.k_ID_StrideAxis, strideAxis);
                func.SetInt(k_TensorPropertiesX.k_ID_DimAxis, dimAxis);
                func.Dispatch(pinO);
            }

            ReleaseTensor(B);
            ReleaseTensor(S);
        }

        /// <inheritdoc/>
        public void Softmax(Tensor<float> X, Tensor<float> O, int axis)
        {
            SoftmaxActivation(X, O, axis, "SOFTMAXEND");
        }

        /// <inheritdoc/>
        public void LogSoftmax(Tensor<float> X, Tensor<float> O, int axis)
        {
            SoftmaxActivation(X, O, axis, "LOGSOFTMAXEND");
        }

        /// <inheritdoc/>
        public void Hardmax(Tensor<float> X, Tensor<float> O, int axis)
        {
            axis = X.shape.Axis(axis);

            // Allocate temp tensors
            var reduceOpShape = X.shape.Reduce(axis);
            var argMax = AllocTensor(reduceOpShape, DataType.Int);

            // argmax
            ReduceIndices(X, argMax, "ArgMax", axis, false);

            // one hot from argmax
            var pinArgMax = PinBlockAny(argMax);
            var pinO = PinAsSame(O, pinArgMax);

            var func = new PixelFunc("Hidden/Sentis/HardmaxEnd");

            func.SetTensor(k_TensorPropertiesX, pinArgMax);
            func.SetInt(k_ID_StrideAxis, pinO.blockedShape.Strides(axis));
            func.SetInt(k_TensorPropertiesO.k_ID_DimAxis, pinO.blockedShape[axis]);

            func.Dispatch(pinO);

            ReleaseTensor(argMax);
        }

        /// <inheritdoc/>
        public void OneHot(Tensor<int> X, Tensor<int> O, int axis, int depth, int offValue, int onValue, bool allowNegativeIndexes)
        {
            axis = O.shape.Axis(axis);

            var pinX = PinBlockAny(X);
            var pinO = TextureTensorData.Pin(O, axis > pinX.blockAxis ? pinX.blockAxis : pinX.blockAxis + 1, false);

            var func = new PixelFunc("Hidden/Sentis/OneHot");
            func.EnableKeyword("OneHotInt");

            func.SetInt(k_ID_negativeIndexOffset, allowNegativeIndexes ? depth : 0);
            func.SetInt(k_ID_offValueInt, offValue);
            func.SetInt(k_ID_onValueInt, onValue);

            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetInt(k_ID_StrideAxis, pinO.blockedShape.Strides(axis));
            func.SetInt(k_TensorPropertiesO.k_ID_DimAxis, pinO.blockedShape[axis]);

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void OneHot(Tensor<int> X, Tensor<float> O, int axis, int depth, float offValue, float onValue, bool allowNegativeIndexes)
        {
            axis = O.shape.Axis(axis);

            var pinX = PinBlockAny(X);
            var pinO = TextureTensorData.Pin(O, axis > pinX.blockAxis ? pinX.blockAxis : pinX.blockAxis + 1, false);

            var func = new PixelFunc("Hidden/Sentis/OneHot");

            func.SetInt(k_ID_negativeIndexOffset, allowNegativeIndexes ? depth : 0);
            func.SetFloat(k_ID_offValue, offValue);
            func.SetFloat(k_ID_onValue, onValue);

            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetInt(k_ID_StrideAxis, pinO.blockedShape.Strides(axis));
            func.SetInt(k_TensorPropertiesO.k_ID_DimAxis, pinO.blockedShape[axis]);

            func.Dispatch(pinO);
        }

        void Reduce(Tensor X, Tensor O, int reduceAxis, string kernelName)
        {
            reduceAxis = X.shape.rank == 0 ? -1 : X.shape.Axis(reduceAxis);

            var pinX = PinBlockOther(X, nonBlockAxis: reduceAxis);
            var pinO = PinAsSame(O, pinX, false);

            var func = new PixelFunc("Hidden/Sentis/Reduce");
            func.EnableKeyword(kernelName);

            func.SetTensor(k_TensorPropertiesX, pinX);
            if (X.shape.rank == 0)
            {
                func.SetInt(k_TensorPropertiesX.k_ID_StrideAxis, 1);
                func.SetInt(k_TensorPropertiesX.k_ID_DimAxis, 1);
                func.SetFloat(k_ID_Normalization, 1f);
            }
            else
            {
                func.SetInt(k_TensorPropertiesX.k_ID_StrideAxis, pinX.blockedShape.Strides(reduceAxis));
                func.SetInt(k_TensorPropertiesX.k_ID_DimAxis, pinX.blockedShape[reduceAxis]);
                func.SetFloat(k_ID_Normalization, 1.0f / X.shape[reduceAxis]);
            }
            func.Dispatch(pinO);
        }

        void Reduce(Tensor X, Tensor O, ReadOnlySpan<int> axes, string fullKernelName, string startKernelName = null, string middleKernelName = null, string endKernelName = null)
        {
            bool keepDim = X.shape.rank == O.shape.rank;
            var Oout = keepDim ? O : AllocTensor(X.shape.Reduce(axes, true), X.dataType);

            startKernelName ??= fullKernelName;
            middleKernelName ??= fullKernelName;
            endKernelName ??= fullKernelName;

            var allAxes = (axes == null) || (axes.Length == 0);
            var axesDim = allAxes ? X.shape.rank : axes.Length;
            var shapeXReduced = X.shape;
            var isInitial = true;

            for (var i = 0; i < axesDim - 1; i++)
            {
                var axis = allAxes ? i : X.shape.Axis(axes[i]);
                Assert.AreNotEqual(0, X.shape[axis], "ValueError: zero-size array to reduction operation which has no identity.");

                shapeXReduced[axis] = 1;
                var Otmp = AllocTensor(shapeXReduced, O.dataType);
                Reduce(X, Otmp, axis, isInitial ? startKernelName : middleKernelName);
                if (!isInitial)
                    ReleaseTensor(X);
                X = Otmp;

                isInitial = false;
            }

            {
                var axis = allAxes ? axesDim - 1 : X.shape.Axis(axes[axesDim - 1]);
                Reduce(X, Oout, axis, isInitial ? fullKernelName : endKernelName);
                if (!isInitial)
                    ReleaseTensor(X);
            }

            if (!keepDim)
            {
                Reshape(Oout, O);
                ReleaseTensor(Oout);
            }
        }

        /// <inheritdoc/>
        public void ReduceMax(Tensor<float> X, Tensor<float> O, ReadOnlySpan<int> axes)
        {
            Reduce(X, O, axes, "ReduceMax");
        }

        /// <inheritdoc/>
        public void ReduceMax(Tensor<int> X, Tensor<int> O, ReadOnlySpan<int> axes)
        {
            Reduce(X, O, axes, "ReduceMaxInt");
        }

        /// <inheritdoc/>
        public void ReduceMean(Tensor<float> X, Tensor<float> O, ReadOnlySpan<int> axes)
        {
            Reduce(X, O, axes, "ReduceMean");
        }

        /// <inheritdoc/>
        public void RMSNormalization(Tensor<float> X, Tensor<float> S, Tensor<float> O, float epsilon)
        {
            var axis = X.shape.Axis(-1);
            var reducedShape = X.shape.Reduce(axis);
            var K = AllocTensor(reducedShape, DataType.Float);

            Reduce(X, K, axis, "ReduceMeanSquare");

            var pinX = PinBlockAny(X);
            var pinK = PinAsSame(K, pinX);
            var pinS = TextureTensorData.Pin(S, -1);
            var pinO = PinAsSame(O, pinX);
            var func = new PixelFunc("Hidden/Sentis/RMSNormalizationTail");

            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetTensor(k_TensorPropertiesS, pinS);
            func.SetTensor(k_TensorPropertiesK, pinK);
            func.SetInt(k_ID_reduceLength, pinO.shape[-1]);
            func.SetFloat(k_ID_epsilon, epsilon);

            func.Dispatch(pinO);

            ReleaseTensor(K);
        }

        /// <inheritdoc/>
        public void ReduceMin(Tensor<float> X, Tensor<float> O, ReadOnlySpan<int> axes)
        {
            Reduce(X, O, axes, "ReduceMin");
        }

        /// <inheritdoc/>
        public void ReduceMin(Tensor<int> X, Tensor<int> O, ReadOnlySpan<int> axes)
        {
            Reduce(X, O, axes, "ReduceMinInt");
        }

        /// <inheritdoc/>
        public void ReduceProd(Tensor<float> X, Tensor<float> O, ReadOnlySpan<int> axes)
        {
            Reduce(X, O, axes, "ReduceProd");
        }

        /// <inheritdoc/>
        public void ReduceProd(Tensor<int> X, Tensor<int> O, ReadOnlySpan<int> axes)
        {
            Reduce(X, O, axes, "ReduceProdInt");
        }

        /// <inheritdoc/>
        public void ReduceSum(Tensor<float> X, Tensor<float> O, ReadOnlySpan<int> axes)
        {
            Reduce(X, O, axes, "ReduceSum");
        }

        /// <inheritdoc/>
        public void ReduceSum(Tensor<int> X, Tensor<int> O, ReadOnlySpan<int> axes)
        {
            Reduce(X, O, axes, "ReduceSumInt");
        }

        /// <inheritdoc/>
        public void ReduceL1(Tensor<float> X, Tensor<float> O, ReadOnlySpan<int> axes)
        {
            Reduce(X, O, axes, "ReduceL1", "ReduceL1", "ReduceSum", "ReduceSum");
        }

        /// <inheritdoc/>
        public void ReduceL1(Tensor<int> X, Tensor<int> O, ReadOnlySpan<int> axes)
        {
            Reduce(X, O, axes, "ReduceL1Int", "ReduceL1Int", "ReduceSumInt", "ReduceSumInt");
        }

        /// <inheritdoc/>
        public void ReduceL2(Tensor<float> X, Tensor<float> O, ReadOnlySpan<int> axes)
        {
            Reduce(X, O, axes, "ReduceL2", "ReduceSumSquare", "ReduceSum", "ReduceSqrt");
        }

        /// <inheritdoc/>
        public void ReduceLogSum(Tensor<float> X, Tensor<float> O, ReadOnlySpan<int> axes)
        {
            Reduce(X, O, axes, "ReduceLogSum", "ReduceSum", "ReduceSum", "ReduceLogSum");
        }

        /// <inheritdoc/>
        public void ReduceLogSumExp(Tensor<float> X, Tensor<float> O, ReadOnlySpan<int> axes)
        {
            Reduce(X, O, axes, "ReduceLogSumExp");
        }

        /// <inheritdoc/>
        public void ReduceSumSquare(Tensor<float> X, Tensor<float> O, ReadOnlySpan<int> axes)
        {
            Reduce(X, O, axes, "ReduceSumSquare", "ReduceSumSquare", "ReduceSum", "ReduceSum");
        }

        /// <inheritdoc/>
        public void ReduceSumSquare(Tensor<int> X, Tensor<int> O, ReadOnlySpan<int> axes)
        {
            Reduce(X, O, axes, "ReduceSumSquareInt", "ReduceSumSquareInt", "ReduceSumInt", "ReduceSumInt");
        }

        void ReduceIndices(Tensor X, Tensor O, string kernelName, int axis, bool selectLastIndex)
        {
            axis = X.shape.Axis(axis);
            var pinX = PinBlockOther(X, nonBlockAxis: axis);
            var pinO = PinAsSame(O, pinX, false);

            var func = new PixelFunc("Hidden/Sentis/ReduceIndices");
            func.EnableKeyword(kernelName);
            if (X.dataType == DataType.Int)
                func.EnableKeyword("X_INT");
            func.EnableKeyword(selectLastIndex ? "Last" : "First");

            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetInt(k_TensorPropertiesX.k_ID_StrideAxis, pinX.blockedShape.Strides(axis));
            func.SetInt(k_TensorPropertiesX.k_ID_DimAxis, pinX.blockedShape[axis]);
            func.Dispatch(pinO);
        }

        void ReduceIndices(Tensor X, Tensor<int> O, string kernelName, int axis, bool keepDim, bool selectLastIndex)
        {
            if (keepDim)
            {
                ReduceIndices(X, O, kernelName, axis, selectLastIndex);
            }
            else
            {
                var Otmp = AllocTensor(X.shape.Reduce(axis, true), DataType.Int);
                ReduceIndices(X, Otmp, kernelName, axis, selectLastIndex);
                Reshape(Otmp, O);
                ReleaseTensor(Otmp);
            }
        }

        /// <inheritdoc/>
        public void ArgMax(Tensor<float> X, Tensor<int> O, int axis, bool selectLastIndex)
        {
            bool keepDim = X.shape.rank == O.shape.rank;
            ReduceIndices(X, O, "ArgMax", axis, keepDim, selectLastIndex);
        }

        /// <inheritdoc/>
        public void ArgMax(Tensor<int> X, Tensor<int> O, int axis, bool selectLastIndex)
        {
            bool keepDim = X.shape.rank == O.shape.rank;
            ReduceIndices(X, O, "ArgMax", axis, keepDim, selectLastIndex);
        }

        /// <inheritdoc/>
        public void ArgMin(Tensor<float> X, Tensor<int> O, int axis, bool selectLastIndex)
        {
            bool keepDim = X.shape.rank == O.shape.rank;
            ReduceIndices(X, O, "ArgMin", axis, keepDim, selectLastIndex);
        }

        /// <inheritdoc/>
        public void ArgMin(Tensor<int> X, Tensor<int> O, int axis, bool selectLastIndex)
        {
            bool keepDim = X.shape.rank == O.shape.rank;
            ReduceIndices(X, O, "ArgMin", axis, keepDim, selectLastIndex);
        }

        /// <inheritdoc/>
        public void Gather(Tensor X, Tensor<int> indices, Tensor O, int axis)
        {
            var pinX = PinBlockAny(X);
            var pinB = PinBlockAny(indices);
            var pinO = PinBlockAny(O, false);

            var func = new PixelFunc("Hidden/Sentis/Gather");
            if (X.dataType == DataType.Int)
                func.EnableKeyword("GatherInt");
            func.SetInt(k_ID_endLength, X.shape.Strides(axis));
            func.SetInt(k_ID_indicesLength, indices.shape.length);
            func.SetInt(k_ID_axisDim, X.shape[axis]);
            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetTensorBlockStride(k_TensorPropertiesX, pinX);
            func.SetTensor(k_TensorPropertiesB, pinB);
            func.SetTensorBlockStride(k_TensorPropertiesB, pinB);
            func.SetTensorBlockStride(k_TensorPropertiesO, pinO);
            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void GatherElements(Tensor X, Tensor<int> indices, Tensor O, int axis)
        {
            axis = X.shape.Axis(axis); // need positive axis below

            var pinX = PinBlockAny(X);
            var pinB = PinBlockAny(indices);
            var pinO = PinBlockAny(O, false);

            bool fastPathPossible = ShapeInference.ScatterGatherElementsSupportsFastPath(pinB.shape, pinX.shape, axis);
            // We handle both the generic and fast path in the same shader file, see NoFastPath keyword below
            var func = new PixelFunc("Hidden/Sentis/GatherElements");

            if (X.dataType == DataType.Int)
                func.EnableKeyword("GatherInt");
            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetTensorBlockStride(k_TensorPropertiesX, pinX);
            func.SetTensor(k_TensorPropertiesB, pinB);
            func.SetTensorBlockStride(k_TensorPropertiesB, pinB);
            func.SetTensorBlockStride(k_TensorPropertiesO, pinO);

            func.SetInt(k_ID_inputAxisSize, pinX.shape[axis]);
            if (fastPathPossible)
            {
                func.SetInt(k_ID_inputAxisElementStride, pinX.shape.Strides(axis));
                func.SetInt(k_ID_indicesAxisElementStride, indices.shape.Strides(axis));
                func.SetInt(k_ID_indicesAxisMinusOneElementStride, indices.shape[axis] * indices.shape.Strides(axis));
            }
            else
            {
                func.EnableKeyword("NoFastPath");
                func.SetTensorStridesCompactedAtHead(k_TensorPropertiesO.k_ID_Strides, indices.shape);
                func.SetTensorStridesCompactedAtHead(k_TensorPropertiesX.k_ID_Strides, pinX.shape);
                func.SetInt(k_ID_posAxis, axis);
                func.SetInt(k_TensorPropertiesX.k_ID_Rank, pinX.shape.rank);
            }
            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void GatherND(Tensor X, Tensor<int> indices, Tensor O, int batchDims)
        {
            var pinX = PinBlockAny(X);
            var pinB = PinBlockAny(indices);
            var pinO = PinBlockAny(O, false);

            var func = new PixelFunc("Hidden/Sentis/GatherND");
            func.SetKeyword("GatherInt", X.dataType == DataType.Int);

            func.SetInt(k_ID_iStart, TensorShape.maxRank - pinO.shape.rank);
            func.SetInt(k_ID_iEndIndices, TensorShape.maxRank - pinO.shape.rank + pinB.shape.rank - 1);
            func.SetInt(k_ID_iEndX, TensorShape.maxRank - pinO.shape.rank + batchDims);
            func.SetInt(k_ID_iStartB, TensorShape.maxRank - pinX.shape.rank + batchDims);
            func.SetInt(k_ID_iEndB, TensorShape.maxRank - pinX.shape.rank + batchDims + pinB.shape[-1]);
            func.SetShapeStrides(k_TensorPropertiesX, pinX.shape);
            func.SetShapeStrides(k_TensorPropertiesO, pinO.shape);
            func.SetShapeStrides(k_TensorPropertiesB, pinB.shape);
            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetTensorBlockStride(k_TensorPropertiesX, pinX);
            func.SetTensor(k_TensorPropertiesB, pinB);
            func.SetTensorBlockStride(k_TensorPropertiesB, pinB);
            func.SetTensorBlockStride(k_TensorPropertiesO, pinO);
            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void ScatterElements(Tensor X, Tensor<int> indices, Tensor updates, Tensor O, int axis, Layers.ScatterReductionMode reduction)
        {
            axis = X.shape.Axis(axis); // need positive axis below

            var pinX = PinBlockOther(X, axis);
            var pinB = PinAsSame(indices, pinX);
            var pinW = PinAsSame(updates, pinX);
            var pinO = PinAsSame(O, pinX, false);

            bool fastPathPossible = ShapeInference.ScatterGatherElementsSupportsFastPath(pinB.blockedShape, pinX.blockedShape, axis);
            // We handle both the generic and fast path in the same shader file, see NoFastPath keyword below
            var func = new PixelFunc("Hidden/Sentis/ScatterElements");

            if (X.dataType == DataType.Int)
                func.EnableKeyword("ScatterInt");
            switch (reduction)
            {
                case Layers.ScatterReductionMode.None:
                    func.EnableKeyword("ReduceNone");
                    break;
                case Layers.ScatterReductionMode.Add:
                    func.EnableKeyword("ReduceAdd");
                    break;
                case Layers.ScatterReductionMode.Mul:
                    func.EnableKeyword("ReduceMul");
                    break;
                case Layers.ScatterReductionMode.Max:
                    func.EnableKeyword("ReduceMax");
                    break;
                case Layers.ScatterReductionMode.Min:
                    func.EnableKeyword("ReduceMin");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(reduction), reduction, null);
            }

            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetTensorBlockStride(k_TensorPropertiesX, pinX);
            func.SetTensor(k_TensorPropertiesB, pinB);
            func.SetTensorBlockStride(k_TensorPropertiesB, pinB);
            func.SetTensor(k_TensorPropertiesW, pinW);
            func.SetTensorBlockStride(k_TensorPropertiesW, pinW);
            func.SetTensorBlockStride(k_TensorPropertiesO, pinO);
            func.SetInt(k_ID_NumIndices, pinB.blockedShape[axis]);

            Logger.AssertIsTrue(pinB.blockedShape.length < int.MaxValue, "ScatterElements: indices is too large.");
            func.SetInt(k_ID_indicesLinearSize, pinB.blockedShape.length);

            func.SetInt(k_ID_outAxisSize, pinO.blockedShape[axis]);
            if (fastPathPossible)
            {
                func.SetInt(k_ID_outAxisElementStride, pinO.blockedShape.Strides(axis));
                func.SetInt(k_ID_indicesAxisElementStride, pinB.blockedShape.Strides(axis));
            }
            else
            {
                func.EnableKeyword("NoFastPath");
                func.SetTensorStridesCompactedAtHead(k_TensorPropertiesO.k_ID_Strides, pinO.blockedShape);
                func.SetTensorShapesCompactedAtHead(k_TensorPropertiesB.k_ID_Shape, pinB.blockedShape);
                func.SetTensorStridesCompactedAtHead(k_TensorPropertiesB.k_ID_Strides, pinB.blockedShape);
                func.SetInt(k_ID_posAxis, axis);
                func.SetInt(k_TensorPropertiesX.k_ID_Rank, pinX.shape.rank);
            }

            if (pinB.dimAxis != pinX.dimAxis)
            {
                func.SetInt4(k_ID_indicesDiv4RemainderMask, pinB.blockedAxisDiv4RemainderMask);
                func.EnableKeyword("UseDiv4Mask");
            }

            func.Dispatch(pinO);
        }

        void ScatterND(Tensor X, Tensor<int> indices, Tensor updates, Tensor O, DataType dataType, Layers.ScatterReductionMode reduction)
        {
            var func = new PixelFunc("Hidden/Sentis/ScatterND");
            if (dataType == DataType.Int)
                func.EnableKeyword("ScatterInt");

            var K = indices.shape[-1];
            var pinX = PinBlockAny(X);
            if (pinX.blockAxis >= 0 && pinX.blockAxis < K)
                pinX = TextureTensorData.Pin(X, K == X.shape.rank ? -1 : X.shape.rank - 1);
            var pinB = TextureTensorData.Pin(indices, indices.shape.rank - 1);
            var pinW = PinAsSame(updates, pinX);
            var pinO = PinAsSame(O, pinX, false);

            switch (reduction)
            {
                case Layers.ScatterReductionMode.None:
                    func.EnableKeyword("ReduceNone");
                    break;
                case Layers.ScatterReductionMode.Add:
                    func.EnableKeyword("ReduceAdd");
                    break;
                case Layers.ScatterReductionMode.Mul:
                    func.EnableKeyword("ReduceMul");
                    break;
                case Layers.ScatterReductionMode.Max:
                    func.EnableKeyword("ReduceMax");
                    break;
                case Layers.ScatterReductionMode.Min:
                    func.EnableKeyword("ReduceMin");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(reduction), reduction, null);
            }

            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetTensorBlockStride(k_TensorPropertiesX, pinX);
            func.SetTensor(k_TensorPropertiesB, pinB);
            func.SetTensorBlockStride(k_TensorPropertiesB, pinB);
            func.SetTensor(k_TensorPropertiesW, pinW);
            func.SetTensorBlockStride(k_TensorPropertiesW, pinW);
            func.SetTensorBlockStride(k_TensorPropertiesO, pinO);

            var Kdiv4 = pinB.blockedShape[-1];
            if (Kdiv4 > 1)
                func.EnableKeyword("K_LARGE");
            func.SetShape(k_TensorPropertiesX.k_ID_Shape, X.shape);
            func.SetInt(k_ID_SliceLength, pinX.blockedShape.Length(K));
            func.SetInt(k_ID_NumIndices, pinB.blockedShape.length / Kdiv4);
            func.SetInt(k_ID_rank, X.shape.rank);
            func.SetInt(k_ID_K, K);
            func.SetInt(k_ID_Kdiv4, Kdiv4);
            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void ScatterND(Tensor<float> X, Tensor<int> indices, Tensor<float> updates, Tensor<float> O, Layers.ScatterReductionMode reduction)
        {
            ScatterND(X, indices, updates, O, DataType.Float, reduction);
        }

        /// <inheritdoc/>
        public void ScatterND(Tensor<int> X, Tensor<int> indices, Tensor<int> updates, Tensor<int> O, Layers.ScatterReductionMode reduction)
        {
            ScatterND(X, indices, updates, O, DataType.Int, reduction);
        }

        /// <inheritdoc/>
        public void Transpose(Tensor X, Tensor O)
        {
            var pinX = PinBlockAny(X);
            var oAxis = pinX.blockAxis < 0 ? -1 : X.shape.rank - 1 - pinX.blockAxis;
            var pinO = TextureTensorData.Pin(O, oAxis);

            var func = new PixelFunc("Hidden/Sentis/Transpose");
            if (X.dataType == DataType.Int)
                func.EnableKeyword("INT");

            func.SetTensor(k_TensorPropertiesX, pinX);

            var rank = pinX.shape.rank;
            unsafe
            {
                var permutedStridesX = stackalloc int[TensorShape.maxRank];
                var strideX = 1;
                for (var i = 0; i < rank; i++)
                {
                    var dim = pinX.blockedShape[-1 - i];
                    permutedStridesX[rank - 1 - i] = dim > 1 ? strideX : 0;
                    strideX *= dim;
                }

                func.SetInt8(k_TensorPropertiesX.k_ID_Strides, permutedStridesX);
            }

            func.SetShape(k_ID_DimO, pinO.blockedShape);

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void Transpose(Tensor X, Tensor O, ReadOnlySpan<int> permutations)
        {
            var pinX = PinBlockAny(X);
            var oAxis = pinX.blockAxis;
            for (var i = 0; i < permutations.Length; i++)
            {
                if (permutations[i] == pinX.blockAxis)
                {
                    oAxis = i;
                    break;
                }
            }

            // pin O so that the transposed blocked axis matches
            var pinO = TextureTensorData.Pin(O, oAxis);

            var func = new PixelFunc("Hidden/Sentis/Transpose");
            if (X.dataType == DataType.Int)
                func.EnableKeyword("INT");

            func.SetTensor(k_TensorPropertiesX, pinX);

            var rank = pinX.shape.rank;
            unsafe
            {
                var stridesX = stackalloc int[TensorShape.maxRank];
                var strideX = 1;
                for (var i = 0; i < rank; i++)
                {
                    var dim = pinX.blockedShape[-1 - i];
                    stridesX[i] = dim > 1 ? strideX : 0;
                    strideX *= dim;
                }

                var permutedStridesX = stackalloc int[TensorShape.maxRank];
                for (var i = 0; i < rank; i++)
                {
                    permutedStridesX[i] = stridesX[rank - 1 - permutations[rank - 1 - i]];
                }

                func.SetInt8(k_TensorPropertiesX.k_ID_Strides, permutedStridesX);
            }

            func.SetShape(k_ID_DimO, pinO.blockedShape);

            func.Dispatch(pinO);
        }

        void GlobalPool(Tensor<float> X, Tensor<float> O, string kernelName)
        {
            var pinX = TextureTensorData.Pin(X, 1);
            var pinO = TextureTensorData.Pin(O, 1);

            var func = new PixelFunc("Hidden/Sentis/GlobalPool");
            func.EnableKeyword(kernelName);

            func.SetTensor(k_TensorPropertiesX, pinX);
            var spatialSize = X.shape.Strides(1);
            func.SetInt(k_ID_SpatialSizeX, spatialSize);
            func.SetInt(k_ID_DimAxis, pinX.blockedShape[1]);
            func.SetFloat(k_ID_Normalization, 1.0f / spatialSize);

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void GlobalAveragePool(Tensor<float> X, Tensor<float> O)
        {
            GlobalPool(X, O, "AVGPOOL");
        }

        /// <inheritdoc/>
        public void GlobalMaxPool(Tensor<float> X, Tensor<float> O)
        {
            GlobalPool(X, O, "MAXPOOL");
        }

        void LocalPool(Tensor<float> X, Tensor<float> O, int[] pool, int[] stride, int[] pad, string kernelName)
        {
            var pinX = TextureTensorData.Pin(X, 1);
            var pinO = TextureTensorData.Pin(O, 1);

            var numSpatialDims = X.shape.rank - 2;

            var func = new PixelFunc("Hidden/Sentis/LocalPool");
            if (numSpatialDims == 3)
                func.EnableKeyword("POOL3D");
            else if (numSpatialDims == 2)
                func.EnableKeyword("POOL2D");
            else
                func.EnableKeyword("POOL1D");
            func.EnableKeyword(kernelName);

            func.SetInt(k_ID_O_width, pinO.shape[-1]);
            func.SetInt(k_ID_O_channelsDiv4, pinO.dimAxisDiv4);
            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetInt(k_ID_X_width, pinX.shape[-1]);
            func.SetInt(k_ID_X_channelsDiv4, pinX.dimAxisDiv4);

            func.SetInt(k_ID_StrideX, stride[numSpatialDims - 1]);
            func.SetInt(k_ID_PadX, pad[numSpatialDims - 1]);
            func.SetInt(k_ID_PoolX, pool[numSpatialDims - 1]);

            if (numSpatialDims > 1)
            {
                func.SetInt(k_ID_StrideY, stride[numSpatialDims - 2]);
                func.SetInt(k_ID_PadY, pad[numSpatialDims - 2]);
                func.SetInt(k_ID_PoolY, pool[numSpatialDims - 2]);
                func.SetInt(k_ID_X_height, pinX.shape[-2]);
                func.SetInt(k_ID_O_height, pinO.shape[-2]);
            }

            if (numSpatialDims > 2)
            {
                func.SetInt(k_ID_StrideZ, stride[numSpatialDims - 3]);
                func.SetInt(k_ID_PadZ, pad[numSpatialDims - 3]);
                func.SetInt(k_ID_PoolZ, pool[numSpatialDims - 3]);
                func.SetInt(k_ID_X_depth, pinX.shape[-3]);
                func.SetInt(k_ID_O_depth, pinO.shape[-3]);
            }

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void MaxPool(Tensor<float> X, Tensor<float> O, int[] kernelShape, int[] strides, int[] pads)
        {
            LocalPool(X, O, kernelShape, strides, pads, "MAXPOOL");
        }

        /// <inheritdoc/>
        public void AveragePool(Tensor<float> X, Tensor<float> O, int[] kernelShape, int[] strides, int[] pads)
        {
            LocalPool(X, O, kernelShape, strides, pads, "AVGPOOL");
        }

        /// <inheritdoc/>
        public void DepthToSpace(Tensor X, Tensor O, int blocksize, Layers.DepthToSpaceMode mode)
        {
            var func = new PixelFunc("Hidden/Sentis/DepthToSpace");
            func.SetKeyword("INT", X.dataType == DataType.Int);

            var pinX = PinBlockAny(X);
            var pinO = PinAsSame(O, pinX, false);

            func.SetInt(k_ID_O_width, pinO.shape[3]);
            func.SetInt(k_ID_O_height, pinO.shape[2]);
            func.SetInt(k_ID_O_channels, pinO.shape[1]);

            func.SetTensor(k_TensorPropertiesX, pinX);

            func.SetInt(k_ID_X_width, pinX.shape[3]);
            func.SetInt(k_ID_X_height, pinX.shape[2]);
            func.SetInt(k_ID_X_channels, pinX.shape[1]);

            func.SetTensorBlockStride(k_TensorPropertiesO, pinO);
            func.SetTensorBlockStride(k_TensorPropertiesX, pinX);

            func.SetInt(k_ID_BlockSize, blocksize);

            if (mode == Layers.DepthToSpaceMode.ColumnRowDepth)
                func.EnableKeyword("COLUMNROWDEPTH");
            else if (mode == Layers.DepthToSpaceMode.DepthColumnRow)
                func.EnableKeyword("DEPTHCOLUMNROW");

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void SpaceToDepth(Tensor X, Tensor O, int blocksize)
        {
            var func = new PixelFunc("Hidden/Sentis/SpaceToDepth");
            func.SetKeyword("INT", X.dataType == DataType.Int);

            var pinX = PinBlockAny(X);
            var pinO = PinAsSame(O, pinX, false);

            func.SetInt(k_ID_O_width, pinO.shape[3]);
            func.SetInt(k_ID_O_height, pinO.shape[2]);
            func.SetInt(k_ID_O_channels, pinO.shape[1]);

            func.SetTensor(k_TensorPropertiesX, pinX);

            func.SetInt(k_ID_X_width, pinX.shape[3]);
            func.SetInt(k_ID_X_height, pinX.shape[2]);
            func.SetInt(k_ID_X_channels, pinX.shape[1]);

            func.SetTensorBlockStride(k_TensorPropertiesO, pinO);
            func.SetTensorBlockStride(k_TensorPropertiesX, pinX);

            func.SetInt(k_ID_BlockSize, blocksize);

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void Split(Tensor X, Tensor O, int axis, int start)
        {
            var pinX = PinBlockAny(X);
            var pinO = PinAsSame(O, pinX, false);

            var func = new PixelFunc("Hidden/Sentis/Split");
            if (X.dataType == DataType.Int)
                func.EnableKeyword("INT");

            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetInt(k_ID_StrideAxis, pinO.blockedShape.Strides(axis));
            func.SetInt(k_TensorPropertiesX.k_ID_DimAxis, pinX.blockedShape[axis]);
            func.SetInt(k_ID_SplitStart, start);
            func.SetInt(k_ID_SplitLength, pinO.blockedShape[axis]);
            if (pinX.blockAxis != axis)
                func.EnableKeyword("BLOCKWISE");

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void Tile(Tensor X, Tensor O, ReadOnlySpan<int> repeats)
        {
            var pinX = X.dataOnBackend as TextureTensorData;
            var xRank = X.shape.rank;
            var blockAxis = pinX?.blockAxis ?? 0;

            if (pinX == null || (blockAxis >= 0 && repeats[blockAxis] > 1))
            {
                // repin X again if repeat on blocked axis
                blockAxis = xRank - 1;
                for (; blockAxis >= 0; blockAxis--)
                {
                    if (X.shape[blockAxis] > 1 && repeats[blockAxis] == 1)
                        break;
                }

                pinX = TextureTensorData.Pin(X, blockAxis);
            }

            var pinO = PinAsSame(O, pinX, false);

            var func = new PixelFunc("Hidden/Sentis/Tile");
            if (X.dataType == DataType.Int)
                func.EnableKeyword("INT");
            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetShape(k_ID_DimO, pinO.blockedShape);
            func.SetShape(k_ID_DimX, pinX.blockedShape);
            func.Dispatch(pinO);
        }

        void ResizeND(Tensor<float> X, Tensor<float> O, ReadOnlySpan<float> scale, Layers.InterpolationMode interpolationMode, Layers.NearestMode nearestMode, Layers.CoordTransformMode coordTransformMode)
        {
            // calculate first and last axes with scaling
            var firstScaleAxis = scale.Length;
            var lastScaleAxis = 0;
            for (var i = 0; i < scale.Length; i++)
            {
                if (scale[i] != 1f)
                {
                    firstScaleAxis = Mathf.Min(firstScaleAxis, i);
                    lastScaleAxis = Mathf.Max(lastScaleAxis, i);
                }
            }

            if (firstScaleAxis > lastScaleAxis)
            {
                // no scale
                MemCopy(X, O);
                return;
            }

            // block on channels if not scaled channels otherwise block before first scale axis
            var blockAxis = scale.Length > 1 && scale[1] == 1f ? 1 : firstScaleAxis - 1;

            for (var i = firstScaleAxis; i <= lastScaleAxis; i++)
            {
                if (scale[i] == 1f)
                    continue;
                var oShape = ShapeInference.Resize(X.shape, i, scale[i]);
                var oCurr = i == lastScaleAxis ? O : AllocTensor(oShape, DataType.Float) as Tensor<float>;
                Resize1D(X, oCurr, blockAxis, i, scale[i], interpolationMode, nearestMode, coordTransformMode);
                if (i != firstScaleAxis)
                    ReleaseTensor(X);
                X = oCurr;
            }
        }

        void Resize1D(Tensor<float> X, Tensor<float> O, int blockAxis, int axis, float scale, Layers.InterpolationMode interpolationMode, Layers.NearestMode nearestMode, Layers.CoordTransformMode coordTransformMode)
        {
            var pinX = TextureTensorData.Pin(X, blockAxis);
            var pinO = TextureTensorData.Pin(O, blockAxis);

            var func = new PixelFunc("Hidden/Sentis/Resize1D");

            OpsUtils.GetScaleAndBias(X.shape[axis], O.shape[axis], scale, coordTransformMode, interpolationMode, nearestMode, out float outputScale, out float outputBias);
            func.SetFloat(k_ID_Scale, outputScale);
            func.SetFloat(k_ID_Bias, outputBias);

            if (interpolationMode == Layers.InterpolationMode.Nearest)
            {
                switch (nearestMode)
                {
                    case Layers.NearestMode.RoundPreferFloor:
                    case Layers.NearestMode.Ceil:
                        func.EnableKeyword("NEAREST_CEIL");
                        break;
                    case Layers.NearestMode.RoundPreferCeil:
                    case Layers.NearestMode.Floor:
                        func.EnableKeyword("NEAREST_FLOOR");
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
            else //if (interpolationMode == Layers.InterpolationMode.Linear)
            {
                func.EnableKeyword("LINEAR");
            }

            func.SetInt(k_ID_innerLength, pinO.blockedShape.Strides(axis));
            func.SetInt(k_ID_outAxisSize, pinO.blockedShape[axis]);
            func.SetInt(k_ID_inputAxisSize, pinX.blockedShape[axis]);
            func.SetTensor(k_TensorPropertiesX, pinX);

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void Pad(Tensor<float> X, Tensor<float> O, ReadOnlySpan<int> pad, Layers.PadMode padMode, float constant)
        {
            var pinX = X.dataOnBackend as TextureTensorData;
            var xRank = X.shape.rank;
            var blockAxis = pinX?.blockAxis ?? 0;

            if (pinX == null || (blockAxis >= 0 && pad[blockAxis] + pad[blockAxis + xRank] > 0))
            {
                // repin X again if pad on blocked axis
                blockAxis = xRank - 1;
                for (; blockAxis >= 0; blockAxis--)
                {
                    if (X.shape[blockAxis] > 1 && pad[blockAxis] + pad[blockAxis + xRank] == 0)
                        break;
                }

                pinX = TextureTensorData.Pin(X, blockAxis);
            }

            var pinO = PinAsSame(O, pinX, false);

            var func = new PixelFunc("Hidden/Sentis/Pad");

            switch (padMode)
            {
                case Layers.PadMode.Constant:
                    func.EnableKeyword("CONSTANT");
                    func.SetFloat(k_ID_memValueFloat, constant);
                    break;
                case Layers.PadMode.Reflect:
                    func.EnableKeyword("REFLECT");
                    break;
                case Layers.PadMode.Edge:
                    func.EnableKeyword("EDGE");
                    break;
                case Layers.PadMode.Symmetric:
                    func.EnableKeyword("SYMMETRIC");
                    break;
                case Layers.PadMode.Wrap:
                    func.EnableKeyword("WRAP");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(padMode), padMode, null);
            }

            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetInt(k_ID_MaxBlockIndexX, pinX.blockedShape.length - 1);

            unsafe
            {
                var padArray = stackalloc int[8];
                for (var i = 0; i < xRank; i++)
                {
                    padArray[i] = pad[xRank - 1 - i];
                }

                func.SetInt8(k_ID_Pad, padArray);
            }

            func.SetShape(k_ID_DimO, pinO.blockedShape);
            func.SetShape(k_ID_DimX, pinX.blockedShape);
            func.SetStrides(k_TensorPropertiesX.k_ID_Strides, pinX.blockedShape);

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void Pad(Tensor<int> X, Tensor<int> O, ReadOnlySpan<int> pad, Layers.PadMode padMode, int constant)
        {
            var pinX = X.dataOnBackend as TextureTensorData;
            var xRank = X.shape.rank;
            var blockAxis = pinX?.blockAxis ?? 0;

            if (pinX == null || (blockAxis >= 0 && pad[blockAxis] + pad[blockAxis + xRank] > 0))
            {
                // repin X again if pad on blocked axis
                blockAxis = xRank - 1;
                for (; blockAxis >= 0; blockAxis--)
                {
                    if (X.shape[blockAxis] > 1 && pad[blockAxis] + pad[blockAxis + xRank] == 0)
                        break;
                }

                pinX = TextureTensorData.Pin(X, blockAxis);
            }

            var pinO = PinAsSame(O, pinX, false);

            var func = new PixelFunc("Hidden/Sentis/Pad");
            func.EnableKeyword("INT");

            switch (padMode)
            {
                case Layers.PadMode.Constant:
                    func.EnableKeyword("CONSTANT");
                    func.SetInt(k_ID_memValueInt, constant);
                    break;
                case Layers.PadMode.Reflect:
                    func.EnableKeyword("REFLECT");
                    break;
                case Layers.PadMode.Edge:
                    func.EnableKeyword("EDGE");
                    break;
                case Layers.PadMode.Symmetric:
                    func.EnableKeyword("SYMMETRIC");
                    break;
                case Layers.PadMode.Wrap:
                    func.EnableKeyword("WRAP");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(padMode), padMode, null);
            }

            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetInt(k_ID_MaxBlockIndexX, pinX.blockedShape.length - 1);

            unsafe
            {
                var padArray = stackalloc int[8];
                for (var i = 0; i < xRank; i++)
                {
                    padArray[i] = pad[xRank - 1 - i];
                }

                func.SetInt8(k_ID_Pad, padArray);
            }

            func.SetShape(k_ID_DimO, pinO.blockedShape);
            func.SetShape(k_ID_DimX, pinX.blockedShape);
            func.SetStrides(k_TensorPropertiesX.k_ID_Strides, pinX.blockedShape);

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void Resize(Tensor<float> X, Tensor<float> O, ReadOnlySpan<float> scale, Layers.InterpolationMode interpolationMode, Layers.NearestMode nearestMode, Layers.CoordTransformMode coordTransformMode)
        {
            if (X.shape.rank < 3 || X.shape.rank > 5 || scale[0] != 1f || scale[1] != 1f)
            {
                ResizeND(X, O, scale, interpolationMode, nearestMode, coordTransformMode);
                return;
            }

            var numSpatialDims = X.shape.rank - 2;

            var pinX = TextureTensorData.Pin(X, 1);
            var pinO = TextureTensorData.Pin(O, 1);

            var func = new PixelFunc("Hidden/Sentis/Upsample");
            switch (numSpatialDims)
            {
                case 1:
                    func.EnableKeyword("UPSAMPLE1D");
                    break;
                case 2:
                    func.EnableKeyword("UPSAMPLE2D");
                    break;
                case 3:
                    func.EnableKeyword("UPSAMPLE3D");
                    break;
            }

            var scaleXY = Vector4.zero;
            var biasXY = Vector4.zero;
            for (var i = 0; i < numSpatialDims; i++)
            {
                OpsUtils.GetScaleAndBias(X.shape[2 + i], O.shape[2 + i], scale[2 + i], coordTransformMode, interpolationMode, nearestMode, out float outputScale, out float outputBias);
                scaleXY[i] = outputScale;
                biasXY[i] = outputBias;
            }
            func.SetVector(k_ID_Scale, scaleXY);
            func.SetVector(k_ID_Bias, biasXY);

            if (interpolationMode == Layers.InterpolationMode.Nearest)
            {
                switch (nearestMode)
                {
                    case Layers.NearestMode.RoundPreferFloor:
                    case Layers.NearestMode.Ceil:
                        func.EnableKeyword("NEAREST_CEIL");
                        break;
                    case Layers.NearestMode.RoundPreferCeil:
                    case Layers.NearestMode.Floor:
                        func.EnableKeyword("NEAREST_FLOOR");
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
            else //if (interpolationMode == Layers.InterpolationMode.Linear)
            {
                func.EnableKeyword("LINEAR");
            }

            func.SetInt(k_ID_O_width, pinO.shape[-1]);
            func.SetInt(k_ID_O_channelsDiv4, pinO.dimAxisDiv4);
            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetInt(k_ID_X_width, pinX.shape[-1]);
            func.SetInt(k_ID_X_channelsDiv4, pinX.dimAxisDiv4);

            if (numSpatialDims > 1)
            {
                func.SetInt(k_ID_O_height, pinO.shape[-2]);
                func.SetInt(k_ID_X_height, pinX.shape[-2]);
            }
            if (numSpatialDims > 2)
            {
                func.SetInt(k_ID_O_depth, pinO.shape[-3]);
                func.SetInt(k_ID_X_depth, pinX.shape[-3]);
            }

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void GridSample(Tensor<float> X, Tensor<float> grid, Tensor<float> O, Layers.InterpolationMode mode, Layers.PaddingMode paddingMode, bool alignCorners)
        {
            var numSpatialDims = X.shape.rank - 2;

            var pinX = TextureTensorData.Pin(X, 1);
            var pinGrid = TextureTensorData.Pin(grid, grid.shape.rank - 1);
            var pinO = TextureTensorData.Pin(O, 1);

            var func = new PixelFunc("Hidden/Sentis/GridSample");
            switch (numSpatialDims)
            {
                case 1:
                    func.EnableKeyword("GRIDSAMPLE1D");
                    break;
                case 2:
                    func.EnableKeyword("GRIDSAMPLE2D");
                    break;
                case 3:
                    func.EnableKeyword("GRIDSAMPLE3D");
                    break;
            }

            switch (mode)
            {
                case Layers.InterpolationMode.Nearest:
                    func.EnableKeyword("NEAREST");
                    break;
                case Layers.InterpolationMode.Linear:
                    func.EnableKeyword("LINEAR");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            switch (paddingMode)
            {
                case Layers.PaddingMode.Zeros:
                    func.EnableKeyword("ZEROS");
                    break;
                case Layers.PaddingMode.Border:
                    func.EnableKeyword("BORDER");
                    break;
                case Layers.PaddingMode.Reflection:
                    func.EnableKeyword("REFLECTION");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(paddingMode), paddingMode, null);
            }

            if (alignCorners)
                func.EnableKeyword("ALIGN_CORNERS");
            else
                func.DisableKeyword("ALIGN_CORNERS");

            func.SetInt(k_ID_O_width, pinO.shape[-1]);
            func.SetInt(k_ID_O_channelsDiv4, pinO.dimAxisDiv4);
            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetInt(k_ID_X_width, pinX.shape[-1]);
            func.SetInt(k_ID_X_channelsDiv4, pinX.dimAxisDiv4);
            func.SetTensor(k_TensorPropertiesS, pinGrid);

            if (numSpatialDims > 1)
            {
                func.SetInt(k_ID_O_height, pinO.shape[-2]);
                func.SetInt(k_ID_X_height, pinX.shape[-2]);
            }
            if (numSpatialDims > 2)
            {
                func.SetInt(k_ID_O_depth, pinO.shape[-3]);
                func.SetInt(k_ID_X_depth, pinX.shape[-3]);
            }

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void ScaleBias(Tensor<float> X, Tensor<float> S, Tensor<float> B, Tensor<float> O)
        {
            var func = new PixelFunc("Hidden/Sentis/ScaleBias");

            var pinX = X.dataOnBackend as TextureTensorData;
            pinX ??= TextureTensorData.Pin(X, X.shape.rank - 2);
            var pinS = TextureTensorData.Pin(S, 0);
            var pinB = TextureTensorData.Pin(B, 0);
            var pinO = PinAsSame(O, pinX, false);

            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetTensor(k_TensorPropertiesS, pinS);
            func.SetTensor(k_TensorPropertiesB, pinB);
            if (pinX.blockAxis == 1)
            {
                func.EnableKeyword("BLOCK_C");
                func.SetInt(k_ID_StrideAxis, pinO.strideAxis);
                func.SetInt(k_TensorPropertiesO.k_ID_DimBlocked, pinO.dimAxisDiv4);
            }
            else
            {
                func.SetInt(k_ID_StrideC, pinO.blockedShape.Strides(1));
                func.SetInt(k_ID_DimC, pinO.blockedShape[1]);
            }

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void BatchNormalization(Tensor<float> X, Tensor<float> S, Tensor<float> B, Tensor<float> mean, Tensor<float> variance, Tensor<float> O, float epsilon)
        {
            var func = new PixelFunc("Hidden/Sentis/BatchNormalization");

            var pinX = TextureTensorData.Pin(X, X.shape.rank == 1 ? -1 : 1);
            var pinS = TextureTensorData.Pin(S, 0);
            var pinB = TextureTensorData.Pin(B, 0);
            var pinM = TextureTensorData.Pin(mean, 0);
            var pinV = TextureTensorData.Pin(variance, 0);
            var pinO = PinAsSame(O, pinX, false);

            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetTensor(k_TensorPropertiesS, pinS);
            func.SetTensor(k_TensorPropertiesB, pinB);
            func.SetTensor(k_TensorPropertiesM, pinM);
            func.SetTensor(k_TensorPropertiesV, pinV);

            func.SetInt(k_ID_O_channels, pinO.dimAxis);
            func.SetInt(k_ID_O_width, pinO.strideAxis);
            func.SetInt(k_ID_O_channelsDiv4, pinO.dimAxisDiv4);

            func.SetFloat(k_ID_epsilon, epsilon);

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void LocalResponseNormalization(Tensor<float> X, Tensor<float> O, int supportLength, float bias, float alpha, float beta)
        {
            var func = new PixelFunc("Hidden/Sentis/LocalResponseNormalization");

            int channelStride = X.shape.Length(2);
            int numChannels = X.shape[1];

            float supportHalfSideSize = (supportLength - 1.0f) / 2.0f;
            int leftSupportLength = (int)Mathf.Floor(supportHalfSideSize);
            int rightSupportLength = (int)Mathf.Ceil(supportHalfSideSize);

            var pinX = TextureTensorData.Pin(X, 1);
            var pinO = PinAsSame(O, pinX);

            func.SetTensor(k_TensorPropertiesX, pinX);

            func.SetInt(k_ID_X_channels, numChannels);
            func.SetInt(k_ID_X_channelsDiv4, ComputeHelper.IDivC(numChannels, 4));
            func.SetInt(k_ID_X_strideC, channelStride);

            func.SetInt(k_ID_LeftSupportLength, leftSupportLength);
            func.SetInt(k_ID_RightSupportLength, rightSupportLength);
            func.SetInt(k_ID_RightSupportLengthCeilDiv4, ComputeHelper.IDivC(rightSupportLength, 4));

            func.SetFloat(k_ID_AlphaDivSupportLength, alpha / (float)(supportLength));

            func.SetFloat(k_ID_Bias, bias);
            func.SetFloat(k_ID_Beta, beta);

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void InstanceNormalization(Tensor<float> X, Tensor<float> S, Tensor<float> B, Tensor<float> O, float epsilon)
        {
            var spatialSize = X.shape.Strides(1);
            var pinX = TextureTensorData.Pin(X, 1);
            var pooledShape = ShapeInference.GlobalPool(X.shape);
            var A = AllocTensor(pooledShape, DataType.Float);
            var K = AllocTensor(pooledShape, DataType.Float);
            var pinA = PinAsSame(A, pinX, false);
            var pinK = PinAsSame(K, pinX, false);

            {
                var func = new PixelFunc("Hidden/Sentis/GlobalPool");
                func.EnableKeyword("AVGPOOL");

                func.SetTensor(k_TensorPropertiesX, pinX);
                func.SetInt(k_ID_SpatialSizeX, spatialSize);
                func.SetInt(k_ID_DimAxis, pinX.blockedShape[1]);
                func.SetFloat(k_ID_Normalization, 1.0f / spatialSize);

                func.Dispatch(pinA);
            }

            {
                var func = new PixelFunc("Hidden/Sentis/GlobalPool");
                func.EnableKeyword("AVGSQUAREPOOL");

                func.SetTensor(k_TensorPropertiesX, pinX);
                func.SetInt(k_ID_SpatialSizeX, spatialSize);
                func.SetInt(k_ID_DimAxis, pinX.blockedShape[1]);
                func.SetFloat(k_ID_Normalization, 1.0f / spatialSize);

                func.Dispatch(pinK);
            }

            {
                var pinO = TextureTensorData.Pin(O, 1);
                var pinS = TextureTensorData.Pin(S, 0);
                var pinB = TextureTensorData.Pin(B, 0);
                var func = new PixelFunc("Hidden/Sentis/InstanceNormalizationTail");

                func.SetTensor(k_TensorPropertiesX, pinX);
                func.SetTensor(k_TensorPropertiesS, pinS);
                func.SetTensor(k_TensorPropertiesA, pinA);
                func.SetTensor(k_TensorPropertiesB, pinB);
                func.SetTensor(k_TensorPropertiesK, pinK);
                func.SetInt(k_ID_StrideAxis, spatialSize);
                func.SetInt(k_ID_O_channelsDiv4, pinO.blockedShape[1]);
                func.SetFloat(k_ID_epsilon, epsilon);

                func.Dispatch(pinO);
            }

            ReleaseTensor(A);
            ReleaseTensor(K);
        }

        /// <inheritdoc/>
        public void LayerNormalization(Tensor<float> X, Tensor<float> S, Tensor<float> B, Tensor<float> O, float epsilon)
        {
            var axis = X.shape.Axis(-1);
            var reducedShape = X.shape.Reduce(axis);
            var A = AllocTensor(reducedShape, DataType.Float);
            var K = AllocTensor(reducedShape, DataType.Float);

            Reduce(X, A, axis, "ReduceMean");
            Reduce(X, K, axis, "ReduceMeanSquare");

            var pinX = PinBlockAny(X);
            var pinA = PinAsSame(A, pinX);
            var pinK = PinAsSame(K, pinX);
            var pinS = TextureTensorData.Pin(S, -1);
            var pinB = TextureTensorData.Pin(B, -1);
            var pinO = PinAsSame(O, pinX);
            var func = new PixelFunc("Hidden/Sentis/LayerNormalizationTail");

            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetTensor(k_TensorPropertiesS, pinS);
            func.SetTensor(k_TensorPropertiesA, pinA);
            func.SetTensor(k_TensorPropertiesB, pinB);
            func.SetTensor(k_TensorPropertiesK, pinK);
            func.SetInt(k_ID_reduceLength, pinO.shape[-1]);
            func.SetFloat(k_ID_epsilon, epsilon);

            func.Dispatch(pinO);

            ReleaseTensor(A);
            ReleaseTensor(K);
        }

        /// <inheritdoc/>
        public void RoiAlign(Tensor<float> X, Tensor<float> rois, Tensor<int> indices, Tensor<float> O, Layers.RoiPoolingMode mode, int outputHeight, int outputWidth, int samplingRatio, float spatialScale, Layers.RoiCoordinateTransformationMode coordinateTransformationMode)
        {
            var pinX = TextureTensorData.Pin(X, 1);
            var pinB = PinBlockAny(indices);
            var pinS = TextureTensorData.Pin(rois, 1);
            var pinO = PinAsSame(O, pinX, false);

            var func = new PixelFunc("Hidden/Sentis/RoiAlign");
            func.EnableKeyword(mode == Layers.RoiPoolingMode.Avg ? "RoiAlignAvg" : "RoiAlignMax");
            func.SetKeyword("HALFPIXEL", coordinateTransformationMode == Layers.RoiCoordinateTransformationMode.HalfPixel);

            func.SetFloat(k_ID_spatialScale, spatialScale);
            func.SetInt(k_ID_numRois, rois.shape[0]);
            func.SetFloat(k_ID_normalizeOHeight, 1.0f / outputHeight);
            func.SetFloat(k_ID_normalizeOWidth, 1.0f / outputWidth);
            func.SetInt(k_ID_samplingRatio, samplingRatio);

            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetTensor(k_TensorPropertiesB, pinB);
            func.SetTensorBlockStride(k_TensorPropertiesB, pinB);
            func.SetTensor(k_TensorPropertiesS, pinS);

            func.SetInt(k_ID_O_width, pinO.shape[-1]);
            func.SetInt(k_ID_O_height, pinO.shape[-2]);
            func.SetInt(k_ID_O_channelsDiv4, pinO.dimAxisDiv4);
            func.SetInt(k_ID_X_width, pinX.shape[-1]);
            func.SetInt(k_ID_X_height, pinX.shape[-2]);
            func.SetInt(k_ID_X_channelsDiv4, pinX.dimAxisDiv4);

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void RandomUniform(Tensor<float> O, float low, float high, int? seed)
        {
            var pinO = PinBlockAny(O, false);

            var func = new PixelFunc("Hidden/Sentis/Random");
            func.EnableKeyword("RandomUniform");
            func.SetInt(k_ID_seed, (int)Random.GetSeed(seed));
            func.SetFloat(k_ID_low, low);
            func.SetFloat(k_ID_high, high);
            func.SetTensorBlockStride(k_TensorPropertiesO, pinO);

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void RandomNormal(Tensor<float> O, float mean, float scale, int? seed)
        {
            var pinO = PinBlockAny(O, false);

            var func = new PixelFunc("Hidden/Sentis/Random");
            func.EnableKeyword("RandomNormal");
            func.SetInt(k_ID_seed, (int)Random.GetSeed(seed));
            func.SetFloat(k_ID_mean, mean);
            func.SetFloat(k_ID_scale, scale);
            func.SetTensorBlockStride(k_TensorPropertiesO, pinO);

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void Bernoulli(Tensor<float> X, Tensor O, int? seed)
        {
            var func = new PixelFunc("Hidden/Sentis/Random");
            func.EnableKeyword(O.dataType == DataType.Int ? "BernoulliInt" : "Bernoulli");

            var pinX = PinBlockAny(X);
            var pinO = PinAsSame(O, pinX);

            func.SetInt(k_ID_seed, (int)Random.GetSeed(seed));
            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetTensorBlockStride(k_TensorPropertiesO, pinO);

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void Range(Tensor<float> O, float start, float delta)
        {
            var pinO = PinBlockAny(O);

            var func = new PixelFunc("Hidden/Sentis/Range");
            func.SetFloat(k_ID_rangeStartFloat, start);
            func.SetFloat(k_ID_rangeDeltaFloat, delta);
            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void Range(Tensor<int> O, int start, int delta)
        {
            var pinO = PinBlockAny(O);

            var func = new PixelFunc("Hidden/Sentis/Range");
            func.EnableKeyword("INT");
            func.SetInt(k_ID_rangeStartInt, start);
            func.SetInt(k_ID_rangeDeltaInt, delta);
            func.Dispatch(pinO);
        }

        void Trilu(Tensor X, Tensor O, int k, bool upper)
        {
            var pinX = PinBlockAny(X);
            var pinO = PinAsSame(O, pinX, false);

            var func = new PixelFunc("Hidden/Sentis/Trilu");
            if (X.dataType == DataType.Int)
                func.EnableKeyword("INT");
            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetTensorBlockStride(k_TensorPropertiesO, pinO);
            func.SetInt(k_ID_width, X.shape[-1]);
            func.SetInt(k_ID_height, X.shape[-2]);
            func.SetInt(k_ID_direction, upper ? 1 : -1);
            func.SetInt(k_ID_offset, k);
            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void Tril(Tensor X, Tensor O, int k)
        {
            Trilu(X, O, k, false);
        }

        /// <inheritdoc/>
        public void Triu(Tensor X, Tensor O, int k)
        {
            Trilu(X, O, k, true);
        }

        void CumSum(Tensor X, Tensor O, DataType dataType, int axis, bool reverse, bool exclusive)
        {
            axis = X.shape.Axis(axis);

            var pinX = PinBlockOther(X, nonBlockAxis: axis);
            var pinO = PinAsSame(O, pinX, false);

            var func = new PixelFunc("Hidden/Sentis/CumSum");
            if (dataType == DataType.Int)
                func.EnableKeyword("INT");
            func.EnableKeyword(reverse ? "REVERSE" : "FORWARD");
            func.EnableKeyword(exclusive ? "EXCLUSIVE" : "INCLUSIVE");

            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetInt(k_ID_StrideAxis, pinX.blockedShape.Strides(axis));
            func.SetInt(k_ID_DimAxis, pinX.blockedShape[axis]);
            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void CumSum(Tensor<float> X, Tensor<float> O, int axis, bool reverse, bool exclusive)
        {
            CumSum(X, O, DataType.Float, axis, reverse, exclusive);
        }

        /// <inheritdoc/>
        public void CumSum(Tensor<int> X, Tensor<int> O, int axis, bool reverse, bool exclusive)
        {
            CumSum(X, O, DataType.Int, axis, reverse, exclusive);
        }

        /// <inheritdoc/>
        public void MemCopy(Tensor X, Tensor O)
        {
            var pinX = PinBlockAny(X);
            var pinO = PinAsSame(O, pinX);
            var func = new PixelFunc("Hidden/Sentis/Copy");
            if (X.dataType == DataType.Int)
                func.EnableKeyword("INT");
            func.SetTensor(k_TensorPropertiesX, pinX);
            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void Reshape(Tensor X, Tensor O)
        {
            TextureTensorData pinO;

            if (X.dataOnBackend is TextureTensorData pinX)
            {
                // try and pin O in a layout that can be read in float4 blocks from x
                var blockAxis = O.shape.rank - 1;
                var strideO = 1;
                for (; blockAxis >= 0; blockAxis--)
                {
                    if (strideO >= pinX.strideAxis)
                        break;
                    strideO *= O.shape[blockAxis];
                }

                pinO = TextureTensorData.Pin(O, blockAxis, false);
            }
            else
            {
                pinX = PinBlockAny(X);
                pinO = PinAsSame(O, pinX, false);
            }

            var func = new PixelFunc("Hidden/Sentis/Reshape");
            if (X.dataType == DataType.Int)
                func.EnableKeyword("INT");
            func.SetTensor(k_TensorPropertiesX, pinX);
            if (pinX.strideAxis == pinO.strideAxis && (pinX.dimAxis == pinO.dimAxis) || (pinX.dimAxis % 4 == 0 && pinO.dimAxis % 4 == 0))
            {
                func.EnableKeyword("BLOCKWISE");
            }
            else
            {
                func.SetTensorBlockStride(k_TensorPropertiesO, pinO);
                func.SetTensorBlockStride(k_TensorPropertiesX, pinX);
            }

            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void TopP(Tensor<float> X, Tensor<float> random, Tensor<int> O)
        {
            var axis = 1;
            var pinX = PinBlockOther(X, nonBlockAxis: axis);
            var pinB = PinAsSame(random, pinX, false);
            var pinO = PinAsSame(O, pinX, false);

            var func = new PixelFunc("Hidden/Sentis/TopP");

            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetTensor(k_TensorPropertiesB, pinB);
            func.SetInt(k_TensorPropertiesX.k_ID_StrideAxis, pinX.blockedShape.Strides(axis));
            func.SetInt(k_TensorPropertiesX.k_ID_DimAxis, pinX.blockedShape[axis]);
            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void LSTM(Tensor<float> X, Tensor<float> W, Tensor<float> R, Tensor<float> B, Tensor<int> sequenceLens, Tensor<float> initialH, Tensor<float> initialC, Tensor<float> P, Tensor<float> Y, Tensor<float> Yh, Tensor<float> Yc, Layers.RnnDirection direction, Layers.RnnActivation[] activations, float[] activationAlpha, float[] activationBeta, bool inputForget, float clip, Layers.RnnLayout layout)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void TopK(Tensor<float> X, Tensor<float> values, Tensor<int> indices, int k, int axis, bool largest)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void TopK(Tensor<int> X, Tensor<int> values, Tensor<int> indices, int k, int axis, bool largest)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void Einsum(Tensor<float>[] inputTensors, Tensor<float> O, TensorIndex[] operandIndices, TensorIndex outputIndices, TensorIndex sumIndices, TensorShape sumShape)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void MemCopyStride(Tensor X, Tensor O, int strideX, int strideO, int length, int count, int offsetX, int offsetO)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void CompressWithIndices(Tensor X, Tensor<int> indices, Tensor O, int numIndices, int axis)
        {
            var pinX = PinBlockAny(X);
            var pinB = PinBlockAny(indices);
            var pinO = PinBlockAny(O, false);

            var func = new PixelFunc("Hidden/Sentis/Gather");
            if (X.dataType == DataType.Int)
                func.EnableKeyword("GatherInt");
            func.SetInt(k_ID_endLength, X.shape.Strides(axis));
            func.SetInt(k_ID_indicesLength, numIndices);
            func.SetInt(k_ID_axisDim, X.shape[axis]);
            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetTensorBlockStride(k_TensorPropertiesX, pinX);
            func.SetTensor(k_TensorPropertiesB, pinB);
            func.SetTensorBlockStride(k_TensorPropertiesB, pinB);
            func.SetTensorBlockStride(k_TensorPropertiesO, pinO);
            func.Dispatch(pinO);
        }

        enum STFTPixelConfigs
        {
            ComplexSignal_4cOnRealIma,
            ComplexSignal_4cOnTime, // For complex, output real/ima axis on 4c fragment and no split row matrix here
            RealSignal_Out4cOnRealIma_NoSplitRowMatrix,
            RealSignal_Out4cOnFreq_SplitRowMatrix,
            RealSignal_Out4cOnFreq_NoSplitRowMatrix,
        }

        //
        // Note we allow independent specification of the dftLength as dftLength can be >= inputFrameLength: this generates a matrix that does the equivalent of zero-padding the input signal
        // If dftLength != inputFrameLength ie dftLength > inputFrameLength, then alternateRealImaOnRows = true is only a valid configuration when expected input signal is real
        // The matrix is expected to be tightly packed 4 values per texel regardless of shape as only used in STFT which expects it that way, so shape stays 1D
        void WindowedDFTMatrix(Tensor<float> window, Tensor<float> windowedDFTMatrix, int dftLength, int inputFrameLength, bool inverse, bool onesided, bool alternateRealImaOnRows)
        {
            Logger.AssertIsTrue(dftLength >= inputFrameLength, "WindowedDFTMatrix: dftLength should be >= inputFrameLength.");

            int outputXformSignalLength = onesided ? dftLength / 2 + 1 : dftLength;

            // Sanity check
            int numRows = outputXformSignalLength;
            if (alternateRealImaOnRows)
                numRows *= 2;
            int numCols = alternateRealImaOnRows ? inputFrameLength : inputFrameLength * 2;

            var twiddleMatrixShape = new TensorShape(numRows, numCols);
            Logger.AssertIsTrue(windowedDFTMatrix.shape.length == twiddleMatrixShape.length, "windowedDFTMatrix has unexpected shape vs given parameters to generate it.");

            var genfn = new PixelFunc("Hidden/Sentis/WindowedDFTMatrix");

            bool noWindowGiven = window == null;
            if (!noWindowGiven)
            {
                var pinW = TextureTensorData.Pin(window, 0);
                genfn.SetTensor(k_TensorPropertiesW, pinW);
                genfn.SetTensorBlockStride(k_TensorPropertiesW, pinW);
            }

            genfn.SetKeyword("NO_WINDOW", noWindowGiven);
            genfn.SetKeyword("INVERSE_DFT", inverse);
            genfn.SetKeyword("SPLIT_REAL_IMA_ON_ALTERNATE_ROWS", alternateRealImaOnRows);

            // We want the dft matrix packed as 4-chunked TextureTensorData, packing
            // 4 values per texel in the generation shader and extracting packed-values also in the STFT shader.
            // Otherwise, with a rank > 1, the automatic 4-channel chunking ("blocked axis") system we currently have,
            // any axis used will create unecessary waste.
            var twiddleMatrixFlatShape = new TensorShape(twiddleMatrixShape.length);
            windowedDFTMatrix.shape = twiddleMatrixFlatShape;
            var pinTwiddleOut = TextureTensorData.Pin(windowedDFTMatrix, 0);
            genfn.SetTensor(k_TensorPropertiesO, pinTwiddleOut);

            // k_ID_IDFTNormalizer: prefer to do the *(1/outputXformSignalLength) normalizing after the DFT matmul, better precision

            genfn.SetInt(k_ID_O_width, inputFrameLength);
            // cb.SetComputeIntParam(genfn.shader, k_ID_O_height, outputXformSignalLength * (alternateRealImaOnRows ? 2 : 1)); // if signal is real, we double the number of rows as we split the real and imaginary parts on separate rows
            genfn.SetFloat(k_ID_DFTFundamentalFreq, 1.0f / ((float)dftLength));

            genfn.Dispatch(pinTwiddleOut);
        }

        public void STFT(Tensor<float> signal, Tensor<float> window, Tensor<float> O, int frameStep, int frameLength, Tensor<float> windowedDFTMatrix, bool inverse, bool onesided, float scale)
        {
            // Available paths:
            // X complex, X blocked on last(real / ima, axis = -1)
            // X complex, X blocked on time(axis = -2)
            //     (here out blocked on last axis, matrix NO SPLIT_REAL_IMA_ON_ALTERNATE_ROWS)
            // X real, out blocked on last axis, matrix NO SPLIT_REAL_IMA_ON_ALTERNATE_ROWS
            // X real, out blocked on dft freq, matrix SPLIT_REAL_IMA_ON_ALTERNATE_ROWS
            // X real, out blocked on dft freq, matrix NO SPLIT_REAL_IMA_ON_ALTERNATE_ROWS

            //const STFTPixelConfigs cfgComplexSignal = STFTPixelConfigs.ComplexSignal_4cOnRealIma;
            const STFTPixelConfigs cfgComplexSignal = STFTPixelConfigs.ComplexSignal_4cOnTime;

            //const STFTPixelConfigs cfgRealSignal = STFTPixelConfigs.RealSignal_Out4cOnRealIma_NoSplitRowMatrix;
            const STFTPixelConfigs cfgRealSignal = STFTPixelConfigs.RealSignal_Out4cOnFreq_SplitRowMatrix;
            //const STFTPixelConfigs cfgRealSignal = STFTPixelConfigs.RealSignal_Out4cOnFreq_NoSplitRowMatrix;

            bool signalIsReal = signal.shape[-1] == 1;

            bool haveDFTMatrix = windowedDFTMatrix != null;

            bool splitRowMatrix;
            bool signalTexelChansOnTime; // else on real/ima
            bool outputTexelChansOnFreq; // else on real/ima

            if (signalIsReal)
            {
                signalTexelChansOnTime = true; // real signals are always packed with texel channel # == time
                splitRowMatrix = haveDFTMatrix || cfgRealSignal == STFTPixelConfigs.RealSignal_Out4cOnFreq_SplitRowMatrix;
                // the given windowedDFTMatrix is assumed to have real/ima split on rows when made for a real signal,
                // so the static config is overridden.
                outputTexelChansOnFreq = cfgRealSignal != STFTPixelConfigs.RealSignal_Out4cOnRealIma_NoSplitRowMatrix;
            }
            else
            {
                signalTexelChansOnTime = cfgComplexSignal == STFTPixelConfigs.ComplexSignal_4cOnTime;

                // Filter config above based on how textureTensorData is already organized on the signal side:
                var sigTextureData = signal.dataOnBackend as TextureTensorData;
                if (sigTextureData != null)
                {
                    if (sigTextureData.blockAxis == (signal.shape.rank - 2))
                        signalTexelChansOnTime = true;
                    else if (sigTextureData.blockAxis == (signal.shape.rank - 1))
                        signalTexelChansOnTime = false;
                }
                splitRowMatrix = false;
                outputTexelChansOnFreq = false; // for complex signal, output is always packed with texel channel # == real/imaginary pair (2 channels unused)
            }

            var twiddleMatrixShape = splitRowMatrix ? new TensorShape(O.shape[-2] * 2, frameLength) : new TensorShape(O.shape[-2], frameLength * 2);
            // ...ie  (onesided ? frameLength / 2 + 1 : frameLength, frameLength),
            // and * 2 for the real and imaginary parts on alternating separate rows.
            var twiddleMatrixFlatShape = new TensorShape(twiddleMatrixShape.length);

            if (haveDFTMatrix)
            {
                var dftMatrixTextureData = windowedDFTMatrix.dataOnBackend as TextureTensorData;
                bool reformatDFTMatrix = dftMatrixTextureData != null && windowedDFTMatrix.shape.rank > 1;

                Logger.AssertIsTrue(windowedDFTMatrix.shape.length == twiddleMatrixShape.length, "windowedDFTMatrix has unexpected shape vs given parameters to generate it.");
                if (reformatDFTMatrix)
                {
                    haveDFTMatrix = false;
                    var windowedDFTMatrixOrig = windowedDFTMatrix;
                    windowedDFTMatrix = AllocTensor(twiddleMatrixFlatShape, DataType.Float) as Tensor<float>;
                    Reshape(windowedDFTMatrixOrig, windowedDFTMatrix);
                }
                else
                {
                    // either texturedata is null or rank is already 1, either way just set the flatshape
                    windowedDFTMatrix.shape = twiddleMatrixFlatShape;
                }
            }
            else
            {
                windowedDFTMatrix = AllocTensor(twiddleMatrixFlatShape, DataType.Float) as Tensor<float>;
                WindowedDFTMatrix(window, windowedDFTMatrix, dftLength: frameLength, inputFrameLength: frameLength, inverse, onesided, alternateRealImaOnRows: splitRowMatrix);
            }

            var func = new PixelFunc("Hidden/Sentis/STFT");

            //int pass = outputTexelChansOnFreq ? 0 : 1; // pass 1 has ROP configured with only channels RG enabled
            // RG pass doesn't work sometimes, todo, investigate why

            ComputeFunctions.STFTPixelSetVariantOnMaterial(func.material, signalIsReal, splitRowMatrix, signalTexelChansOnTime, outputTexelChansOnFreq);

            var pinO = TextureTensorData.Pin(O, outputTexelChansOnFreq ? O.shape.rank - 2 : O.shape.rank - 1); // 4-chunk on DFT N-dian freq bin or real/ima part
            var pinX = TextureTensorData.Pin(signal, signalTexelChansOnTime ? signal.shape.rank - 2 : signal.shape.rank - 1); // 4-chunk on time or real/ima part

            // At this point windowedDFTMatrix should be 1D
            var pinK = TextureTensorData.Pin(windowedDFTMatrix, 0);

            func.SetKeyword("FINAL_SCALAR_MUL", inverse);

            func.SetTensor(k_TensorPropertiesO, pinO);
            func.SetTensor(k_TensorPropertiesX, pinX);
            func.SetTensor(k_TensorPropertiesK, pinK);

            func.SetInt(k_ID_O_width, O.shape[1]); // number of frames
            func.SetInt(k_ID_X_width, signal.shape[1]);
            func.SetInt(k_ID_X_widthDiv4, (signal.shape[1] + 3) / 4);
            func.SetInt(k_ID_K_width, frameLength);
            func.SetInt(k_ID_maxK, pinK.blockedShape.length);
            func.SetInt(k_ID_maxX, pinX.blockedShape.length);
            func.SetInt(k_ID_O_channels, O.shape[-2]);
            func.SetInt(k_ID_O_channelsDiv4, (O.shape[-2] + 3) / 4);

            func.SetInt(k_ID_StrideX, frameStep);
            func.SetFloat(k_ID_Scale, scale); // used if inverse == true

            // func.Dispatch(pinO, pass);
            // RG pass doesn't work sometimes, todo, investigate why
            func.Dispatch(pinO, 0);

            if (!haveDFTMatrix)
                ReleaseTensor(windowedDFTMatrix);
        }

        /// <inheritdoc/>
        public void STFT(Tensor<float> signal, Tensor<float> window, Tensor<float> O, int frameStep, int frameLength, Tensor<float> windowedDFTMatrix, bool onesided)
        {
            STFT(signal, window, O, frameStep, frameLength, windowedDFTMatrix, inverse: false, onesided, scale: 1.0f);
        }

        /// <inheritdoc/>
        public void DFT(Tensor<float> input, Tensor<float> O, int dftLength, int axis, Tensor<float> dftMatrix, bool inverse, bool onesided)
        {
            bool SetShapeRetTrueIfNoDataElseSetShapeIfIsLayoutIdentical(Tensor X, TensorShape newShape, bool forceRelayout, int? newBlockedAxis = null)
            {
                var textureTensorData = X.dataOnBackend as TextureTensorData;
                if (textureTensorData == null)
                {
                    X.shape = newShape;
                    return true;
                }
                int blockedAxis = newBlockedAxis ?? textureTensorData.blockAxis;
                if (!forceRelayout)
                {
                    if (textureTensorData.IsLayoutIdentical(newShape, blockedAxis))
                    {
                        textureTensorData.SetShape(newShape, blockedAxis);
                        X.shape = newShape;
                        return true;
                    }
                }
                else
                {
                    textureTensorData.SwitchBlockedLayout(newShape, blockedAxis);
                    X.shape = newShape;
                }

                return false;
            }

            bool signalIsReal = input.shape[-1] == 1;
            int signalFrameLengthToUse = Math.Min(dftLength, input.shape[axis]);
            int outputXformSignalLength = O.shape[axis]; // onesided ? dftLength / 2 + 1 : dftLength;

            bool haveDFTMatrix = dftMatrix != null;
            var twiddleMatrixShape = signalIsReal ? new TensorShape(outputXformSignalLength * 2, signalFrameLengthToUse) : new TensorShape(outputXformSignalLength, signalFrameLengthToUse * 2);
            // * 2 for the real and imaginary parts on alternating separate rows.
            var twiddleMatrixFlatShape = new TensorShape(twiddleMatrixShape.length);

            if (!haveDFTMatrix)
            {
                dftMatrix = AllocTensorFloat(twiddleMatrixFlatShape);
                WindowedDFTMatrix(window: null, dftMatrix, dftLength, signalFrameLengthToUse, inverse, onesided, alternateRealImaOnRows: signalIsReal);
            }
            // if we already have the dftMatrix from a pre-calculated constant,
            // we will let STFT handle any reformating if necessary.

            bool needSlicing = signalFrameLengthToUse != input.shape[axis];
            Tensor<float> sliceOut = input;
            if (needSlicing)
            {
                var sliceOutShape = new TensorShape(input.shape);
                sliceOutShape[axis] = signalFrameLengthToUse;
                sliceOut = AllocTensorFloat(sliceOutShape);
                Span<int> starts = stackalloc int[1] { 0 };
                Span<int> ends = stackalloc int[1] { signalFrameLengthToUse };
                Span<int> axes = stackalloc int[1] { axis };
                Span<int> steps = stackalloc int[1] { 1 };
                ShapeInference.Slice(sliceOut.shape, starts, ends, axes, steps, ref starts, ref ends, ref axes, ref steps);
                Slice(input, sliceOut, starts, axes, steps);
            }

            int opNumPreSTFT = 0;

            bool needTranspose = input.shape.Axis(axis) != (input.shape.rank - 2);
            Tensor<float> transposeOut = sliceOut;
            Span<int> permutationsTmp = stackalloc int[TensorShape.maxRank];
            TensorShape stftOutShape = O.shape;
            if (needTranspose)
            {
                opNumPreSTFT++;

                for (int i = 0; i < (input.shape.rank - 2); i++)
                {
                    if (i < axis)
                        permutationsTmp[i] = i;
                    else
                        permutationsTmp[i] = i + 1;
                }
                permutationsTmp[input.shape.rank - 1] = input.shape.rank - 1; // this is the complex tuple innermost axis, doesn't change
                permutationsTmp[input.shape.rank - 2] = axis;

                var permutations = permutationsTmp.Slice(0, input.shape.rank);

                var transposedShape = sliceOut.shape.Transpose(permutations);
                transposeOut = AllocTensorFloat(transposedShape);

                Transpose(sliceOut, transposeOut, permutations);

                stftOutShape = O.shape.Transpose(permutations);
            }

            var transposeOutShapeBack = transposeOut.shape;
            var stftOutShapePostReshape = stftOutShape;

            var inputPreSTFTFlatShape = new TensorShape(1, transposeOut.shape.Length(0) / transposeOut.shape[-1], transposeOut.shape[-1]);
            var stftOutFlatShape = new TensorShape(1, stftOutShape.Length(0) / stftOutShape.Length(-2), stftOutShape[-2], stftOutShape[-1]);

            Tensor<float> reshapeOut = transposeOut;
            Tensor<float> stftOut = null;

            bool needTrueReshape = false == SetShapeRetTrueIfNoDataElseSetShapeIfIsLayoutIdentical(reshapeOut, inputPreSTFTFlatShape, forceRelayout: false, inputPreSTFTFlatShape.rank - 2);
            if (needTrueReshape)
            {
                opNumPreSTFT++;
                // Need true reshape
                reshapeOut = AllocTensorFloat(inputPreSTFTFlatShape);
                Reshape(transposeOut, reshapeOut);
                stftOut = AllocTensorFloat(stftOutFlatShape);
            }
            else
            {
                // flatten everything into one big signal of numFrames * frameLength
                // numFrames = stftOut.shape.Length(0) / stftOut.shape.Length(-2))
                // frameLength = stftOut.shape[-2]

                // reshapeOut.shape = inputPreSTFTFlatShape;
                // effect on underlying textureTensorData already handled by SetShapeRetTrueIfNoDataElseSetShapeIfIsLayoutIdentical()
                // note: here we might have reshapeOut = transposeOut = sliceOut = input

                // even without reshape, if need transpose after STFT, stftOut can't be directly O
                if (opNumPreSTFT > 0)
                {
                    stftOut = AllocTensorFloat(stftOutFlatShape);
                }
                else
                {
                    // opNumPreSTFT == 0, so no transpose, no true reshape, at this point O shouldn't have any data in it,
                    // switch its layout by only switching its shape:
                    stftOut = O;
                    if (stftOut.dataOnBackend != null)
                        stftOut.dataOnBackend.Dispose();
                    stftOut.shape = stftOutFlatShape;
                }
            }

            STFT(reshapeOut, window: null, stftOut, frameStep: signalFrameLengthToUse, frameLength: signalFrameLengthToUse, dftMatrix, inverse, onesided, scale: !inverse ? 1.0f : 1.0f / (float)(dftLength));
            // ...note scale could be outputXformSignalLength, since onesided only valid for DFT not IDFT anyway.

            int opNumLeftPostSTFT = opNumPreSTFT;

            Tensor<float> reshapeOutPost = stftOut;
            int opNumLeftPostSTFT_atNeedReshape = opNumLeftPostSTFT;
            if (needTrueReshape)
            {
                opNumLeftPostSTFT--;
                opNumLeftPostSTFT_atNeedReshape = opNumLeftPostSTFT;
                // Need true reshape: but if no need transpose after, give directly O
                reshapeOutPost = (opNumLeftPostSTFT <= 0) ? O : AllocTensorFloat(stftOutShapePostReshape);
                Reshape(stftOut, reshapeOutPost);
            }
            else
            {
                // important: restore shape of input to STFT, reshapeOut,
                // as could have been reshapeOut = transposeOut = sliceOut = input
                //reshapeOut.shape = transposeOutShapeBack;
                if (reshapeOut == input)
                    SetShapeRetTrueIfNoDataElseSetShapeIfIsLayoutIdentical(reshapeOut, transposeOutShapeBack, forceRelayout: true);

                // restore shape of reshapeOutPost = stftOut
                // (could be = O, but even if not, our shape-only reshape is needed unless stftOutFlatShape == stftOutShapePostReshape already)
                //reshapeOutPost.shape = stftOutShapePostReshape;
                SetShapeRetTrueIfNoDataElseSetShapeIfIsLayoutIdentical(reshapeOutPost, stftOutShapePostReshape, forceRelayout: true, stftOutShapePostReshape.rank - 1);
            }

            // Reverse the transposition if needed:
            if (needTranspose)
            {
                opNumLeftPostSTFT--;
                Logger.AssertIsTrue(opNumLeftPostSTFT == 0, "GPUPixel DFT - transpose should be last op in chain");

                for (int i = 0; i < (O.shape.rank - 2); i++)
                {
                    if (i < axis)
                        permutationsTmp[i] = i;
                    else
                        permutationsTmp[i] = i - 1;
                }
                permutationsTmp[O.shape.rank - 1] = O.shape.rank - 1; // this is the complex tuple innermost axis, doesn't change
                permutationsTmp[axis] = O.shape.rank - 2;

                var permutations = permutationsTmp.Slice(0, O.shape.rank);

                Transpose(reshapeOutPost, O, permutations);
            }

            // possible aliases:
            //      reshapeOut =(if !reshape)= transposeOut =(if !transpose)= sliceOut =(if !slice)= input
            //      reshapeOutPost =(if !reshape)= stftOut =(if !reshape !transpose)= O
            if (needSlicing)
                ReleaseTensorFloat(sliceOut);
            if (opNumPreSTFT > 0)
                ReleaseTensorFloat(stftOut);
            if (needTranspose)
                ReleaseTensorFloat(transposeOut);
            if (needTrueReshape && opNumLeftPostSTFT_atNeedReshape > 0)
                ReleaseTensorFloat(reshapeOutPost);
            if (needTrueReshape)
                ReleaseTensorFloat(reshapeOut);
            if (!haveDFTMatrix)
                ReleaseTensorFloat(dftMatrix);
        }

        /// <inheritdoc/>
        public void BlackmanWindow(Tensor<float> O, bool periodic)
        {
            var pinO = PinBlockAny(O);
            var func = new PixelFunc("Hidden/Sentis/Window");
            func.EnableKeyword("BLACKMAN");
            func.SetFloat(k_ID_N, periodic ? O.shape[0] : O.shape[0] - 1);
            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void HammingWindow(Tensor<float> O, bool periodic)
        {
            var pinO = PinBlockAny(O);
            var func = new PixelFunc("Hidden/Sentis/Window");
            func.EnableKeyword("HAMMING");
            func.SetFloat(k_ID_N, periodic ? O.shape[0] : O.shape[0] - 1);
            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void HannWindow(Tensor<float> O, bool periodic)
        {
            var pinO = PinBlockAny(O);
            var func = new PixelFunc("Hidden/Sentis/Window");
            func.EnableKeyword("HANN");
            func.SetFloat(k_ID_N, periodic ? O.shape[0] : O.shape[0] - 1);
            func.Dispatch(pinO);
        }

        /// <inheritdoc/>
        public void MelWeightMatrix(Tensor<float> O, int dftLength, int sampleRate, float lowerEdgeHertz, float upperEdgeHertz)
        {
            var pinO = TextureTensorData.Pin(O, -1);
            var func = new PixelFunc("Hidden/Sentis/MelWeightMatrix");
            func.SetInt(k_ID_dftLength, dftLength);
            func.SetInt(k_ID_sampleRate, sampleRate);
            var lowerEdgeMel = 2595 * math.log10(1 + lowerEdgeHertz / 700);
            var upperEdgeMel = 2595 * math.log10(1 + upperEdgeHertz / 700);
            var melStep = (upperEdgeMel - lowerEdgeMel) / (O.shape[1] + 2);
            func.SetFloat(k_ID_lowerEdgeMel, lowerEdgeMel);
            func.SetFloat(k_ID_melStep, melStep);
            func.SetInt(k_ID_numSpectrogramBins, O.shape[0]);
            func.SetInt(k_ID_numMelBins, O.shape[1]);
            func.Dispatch(pinO);
        }
    }
}
