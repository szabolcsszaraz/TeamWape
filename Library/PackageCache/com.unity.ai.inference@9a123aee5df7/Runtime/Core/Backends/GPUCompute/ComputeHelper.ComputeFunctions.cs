using System;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.InferenceEngine
{
    static class ComputeFunctions
    {
        // Compute Shaders
        static readonly ComputeShader k_TextureToTensor = Resources.Load<ComputeShader>("Sentis/TextureConversion/TextureToTensor");
        static readonly ComputeShader k_TensorToTexture = Resources.Load<ComputeShader>("Sentis/TextureConversion/TensorToTexture");
        static readonly ComputeShader k_AxisActivations = Resources.Load<ComputeShader>("Sentis/ComputeShaders/AxisActivations");
        static readonly ComputeShader k_CumSum = Resources.Load<ComputeShader>("Sentis/ComputeShaders/CumSum");
        static readonly ComputeShader k_ReferenceImpl = Resources.Load<ComputeShader>("Sentis/ComputeShaders/ReferenceImpl");
        static readonly ComputeShader k_RNN = Resources.Load<ComputeShader>("Sentis/ComputeShaders/RNN");
        static readonly ComputeShader k_LogicalOps = Resources.Load<ComputeShader>("Sentis/ComputeShaders/LogicalOps");
        static readonly ComputeShader k_CompareOps = Resources.Load<ComputeShader>("Sentis/ComputeShaders/CompareOps");
        static readonly ComputeShader k_ConvGeneric = Resources.Load<ComputeShader>("Sentis/ComputeShaders/ConvGeneric");
        static readonly ComputeShader k_DepthwiseConv = Resources.Load<ComputeShader>("Sentis/ComputeShaders/DepthwiseConv");
        static readonly ComputeShader k_Dense = Resources.Load<ComputeShader>("Sentis/ComputeShaders/Dense");
        static readonly ComputeShader k_GemmT = Resources.Load<ComputeShader>("Sentis/ComputeShaders/GemmT");
        static readonly ComputeShader k_Pool = Resources.Load<ComputeShader>("Sentis/ComputeShaders/Pool");
        static readonly ComputeShader k_Normalization = Resources.Load<ComputeShader>("Sentis/ComputeShaders/Normalization");
        static readonly ComputeShader k_LocalResponseNormalizationCS = Resources.Load<ComputeShader>("Sentis/ComputeShaders/LocalResponseNormalization");
        static readonly ComputeShader k_NMS = Resources.Load<ComputeShader>("Sentis/ComputeShaders/NMS");
        static readonly ComputeShader k_ReduceIndices = Resources.Load<ComputeShader>("Sentis/ComputeShaders/ReduceIndices");
        static readonly ComputeShader k_CopyOps = Resources.Load<ComputeShader>("Sentis/ComputeShaders/CopyOps");
        static readonly ComputeShader k_Random = Resources.Load<ComputeShader>("Sentis/ComputeShaders/Random");
        static readonly ComputeShader k_IndexingOps = Resources.Load<ComputeShader>("Sentis/ComputeShaders/IndexingOps");
        static readonly ComputeShader k_SortingOps = Resources.Load<ComputeShader>("Sentis/ComputeShaders/SortingOps");
        static readonly ComputeShader k_ScatterOps = Resources.Load<ComputeShader>("Sentis/ComputeShaders/ScatterOps");
        static readonly ComputeShader k_GridSample = Resources.Load<ComputeShader>("Sentis/ComputeShaders/GridSample");
        static readonly ComputeShader k_Resize = Resources.Load<ComputeShader>("Sentis/ComputeShaders/Resize");
        static readonly ComputeShader k_ImageBased = Resources.Load<ComputeShader>("Sentis/ComputeShaders/ImageBased");
        static readonly ComputeShader k_BroadcastGen = Resources.Load<ComputeShader>("Sentis/ComputeShaders/Compute.Shaders.Broadcast.gen");
        static readonly ComputeShader k_ConvGen = Resources.Load<ComputeShader>("Sentis/ComputeShaders/Compute.Shaders.Conv.gen");
        static readonly ComputeShader k_ConvTransposeGen = Resources.Load<ComputeShader>("Sentis/ComputeShaders/Compute.Shaders.ConvTranspose.gen");
        static readonly ComputeShader k_ReductionGen = Resources.Load<ComputeShader>("Sentis/ComputeShaders/Compute.Shaders.Reduction.gen");
        static readonly ComputeShader k_ReductionUnrolledGen = Resources.Load<ComputeShader>("Sentis/ComputeShaders/Compute.Shaders.ReductionUnrolled.gen");
        static readonly ComputeShader k_PointwiseUnaryGen = Resources.Load<ComputeShader>("Sentis/ComputeShaders/Compute.Shaders.PointwiseUnary.gen");
        static readonly ComputeShader k_GenericA = Resources.Load<ComputeShader>("Sentis/ComputeShaders/ReferenceImpl.GenericA");
        static readonly ComputeShader k_PadA = Resources.Load<ComputeShader>("Sentis/ComputeShaders/ReferenceImpl.PadA");
        static readonly ComputeShader k_PoolA = Resources.Load<ComputeShader>("Sentis/ComputeShaders/ReferenceImpl.PoolA");
        static readonly ComputeShader k_Einsum = Resources.Load<ComputeShader>("Sentis/ComputeShaders/ReferenceImpl.Einsum");
        static readonly ComputeShader k_IndexingOpsA = Resources.Load<ComputeShader>("Sentis/ComputeShaders/ReferenceImpl.IndexingOpsA");
        static readonly ComputeShader k_Logical = Resources.Load<ComputeShader>("Sentis/ComputeShaders/ReferenceImpl.Logical");
        static readonly ComputeShader k_Spectral = Resources.Load<ComputeShader>("Sentis/ComputeShaders/Spectral");
        static readonly ComputeShader k_BitonicSort = Resources.Load<ComputeShader>("Sentis/ComputeShaders/BitonicSort");
        static readonly ComputeShader k_RoiAlignShader = Resources.Load<ComputeShader>("Sentis/ComputeShaders/RoiAlign");
        static readonly ComputeShader k_ClipShader = Resources.Load<ComputeShader>("Sentis/ComputeShaders/Clip");
        static readonly ComputeShader k_WindowedDFTMatrix = Resources.Load<ComputeShader>("Sentis/ComputeShaders/WindowedDFTMatrix");

        // pixel shaders
        static readonly Shader k_STFTshader = Shader.Find("Hidden/Sentis/STFT");

        // Kernels
        public static readonly ComputeFunction k_TextureToTensorExact = new(k_TextureToTensor, "TextureToTensorExact");
        public static readonly ComputeFunction k_TextureToTensorLinear = new(k_TextureToTensor, "TextureToTensorLinear");
        public static readonly ComputeFunction k_TensorToTextureExact = new(k_TensorToTexture, "TensorToTextureExact");
        public static readonly ComputeFunction k_TensorToTextureLinear = new(k_TensorToTexture, "TensorToTextureLinear");
        public static readonly ComputeFunction k_LogSoftmaxEnd = new(k_AxisActivations, "LogSoftmaxEnd");
        public static readonly ComputeFunction k_SoftmaxEnd = new(k_AxisActivations, "SoftmaxEnd");
        public static readonly ComputeFunction k_HardmaxEnd = new(k_AxisActivations, "HardmaxEnd");
        public static readonly ComputeFunction k_CumSumFloatForwardInclusive = new(k_CumSum, "CumSumFloatForwardInclusive");
        public static readonly ComputeFunction k_CumSumFloatForwardExclusive = new(k_CumSum, "CumSumFloatForwardExclusive");
        public static readonly ComputeFunction k_CumSumFloatReverseInclusive = new(k_CumSum, "CumSumFloatReverseInclusive");
        public static readonly ComputeFunction k_CumSumFloatReverseExclusive = new(k_CumSum, "CumSumFloatReverseExclusive");
        public static readonly ComputeFunction k_CumSumIntForwardInclusive = new(k_CumSum, "CumSumIntForwardInclusive");
        public static readonly ComputeFunction k_CumSumIntForwardExclusive = new(k_CumSum, "CumSumIntForwardExclusive");
        public static readonly ComputeFunction k_CumSumIntReverseInclusive = new(k_CumSum, "CumSumIntReverseInclusive");
        public static readonly ComputeFunction k_CumSumIntReverseExclusive = new(k_CumSum, "CumSumIntReverseExclusive");
        public static readonly ComputeFunction k_MatMul = new(k_ReferenceImpl, "MatMul");
        public static readonly ComputeFunction k_LSTMEnd = new(k_RNN, "LSTMEnd");
        public static readonly ComputeFunction k_OrInt = new(k_LogicalOps, "OrInt");
        public static readonly ComputeFunction k_AndInt = new(k_LogicalOps, "AndInt");
        public static readonly ComputeFunction k_XorInt = new(k_LogicalOps, "XorInt");
        public static readonly ComputeFunction k_IsInf = new(k_LogicalOps, "IsInf");
        public static readonly ComputeFunction k_GreaterFloat = new(k_CompareOps, "GreaterFloat");
        public static readonly ComputeFunction k_GreaterInt = new(k_CompareOps, "GreaterInt");
        public static readonly ComputeFunction k_GreaterOrEqualFloat = new(k_CompareOps, "GreaterOrEqualFloat");
        public static readonly ComputeFunction k_GreaterOrEqualInt = new(k_CompareOps, "GreaterOrEqualInt");
        public static readonly ComputeFunction k_LessFloat = new(k_CompareOps, "LessFloat");
        public static readonly ComputeFunction k_LessInt = new(k_CompareOps, "LessInt");
        public static readonly ComputeFunction k_LessOrEqualFloat = new(k_CompareOps, "LessOrEqualFloat");
        public static readonly ComputeFunction k_LessOrEqualInt = new(k_CompareOps, "LessOrEqualInt");
        public static readonly ComputeFunction k_EqualFloat = new(k_CompareOps, "EqualFloat");
        public static readonly ComputeFunction k_EqualInt = new(k_CompareOps, "EqualInt");
        public static readonly ComputeFunction k_NotEqualFloat = new(k_CompareOps, "NotEqualFloat");
        public static readonly ComputeFunction k_NotEqualInt = new(k_CompareOps, "NotEqualInt");

        public static readonly ComputeFunction k_Conv3D_Generic = new(k_ConvGeneric, "Conv3D_Generic");
        public static readonly ComputeFunction k_Conv2D_Generic = new(k_ConvGeneric, "Conv2D_Generic");
        public static readonly ComputeFunction k_Conv1D_Generic = new(k_ConvGeneric, "Conv1D_Generic");
        public static readonly ComputeFunction k_Conv3D_1x1_Generic = new(k_ConvGeneric, "Conv3D_1x1_Generic");
        public static readonly ComputeFunction k_Conv2D_1x1_Generic = new(k_ConvGeneric, "Conv2D_1x1_Generic");
        public static readonly ComputeFunction k_Conv1D_1x1_Generic = new(k_ConvGeneric, "Conv1D_1x1_Generic");

        // for STFT
        public static readonly ComputeFunction k_Conv1D_OutTransposed_4eoc_4esp = new(k_ConvGeneric, "Conv1D_OutTransposed_4eoc_4esp");

        //public static readonly ComputeFunction k_Conv1D_OutTransposed_4eoc_8esp = new ComputeFunction(k_ConvGeneric, "Conv1D_OutTransposed_4eoc_8esp");
        public static readonly ComputeFunction k_Conv1DComplex_OutTransposed_4eoc_4esp = new(k_ConvGeneric, "Conv1DComplex_OutTransposed_4eoc_4esp");
        public static readonly ComputeFunction k_Conv1D_OutTransposedDouble_4eoc_4esp = new(k_ConvGeneric, "Conv1D_OutTransposedDouble_4eoc_4esp");

        public static readonly ComputeFunction k_Conv1D_Scaled_OutTransposed_4eoc_4esp = new(k_ConvGeneric, "Conv1D_Scaled_OutTransposed_4eoc_4esp");
        public static readonly ComputeFunction k_Conv1D_Scaled_Complex_OutTransposed_4eoc_4esp = new(k_ConvGeneric, "Conv1D_Scaled_Complex_OutTransposed_4eoc_4esp");

        public static readonly ComputeFunction k_ConvTranspose3D_Generic = new(k_ConvGeneric, "ConvTranspose3D_Generic");
        public static readonly ComputeFunction k_ConvTranspose2D_Generic = new(k_ConvGeneric, "ConvTranspose2D_Generic");
        public static readonly ComputeFunction k_ConvTranspose1D_Generic = new(k_ConvGeneric, "ConvTranspose1D_Generic");

        public static readonly ComputeFunction k_ConvTranspose3D_1x1_Generic = new(k_ConvGeneric, "ConvTranspose3D_1x1_Generic");
        public static readonly ComputeFunction k_ConvTranspose2D_1x1_Generic = new(k_ConvGeneric, "ConvTranspose2D_1x1_Generic");
        public static readonly ComputeFunction k_ConvTranspose1D_1x1_Generic = new(k_ConvGeneric, "ConvTranspose1D_1x1_Generic");

        public static readonly ComputeFunction k_DepthwiseConv2DDirect = new(k_DepthwiseConv, "DepthwiseConv2DDirect");
        public static readonly ComputeFunction k_DepthwiseConv2DWinograd = new(k_DepthwiseConv, "DepthwiseConv2DWinograd");
        public static readonly ComputeFunction k_KernelWinoExpand = new(k_DepthwiseConv, "KernelWinoExpand");
        public static readonly ComputeFunction k_Dense_T8x8_R4x4 = new(k_Dense, "Dense_T8x8_R4x4");
        public static readonly ComputeFunction k_DenseBatched_T8x8_R4x4 = new(k_Dense, "DenseBatched_T8x8_R4x4");
        public static readonly ComputeFunction k_Gemm_T8x8_R4x4 = new(k_Dense, "Gemm_T8x8_R4x4");
        public static readonly ComputeFunction k_GemmBatched_T8x8_R4x4 = new(k_Dense, "GemmBatched_T8x8_R4x4");
        public static readonly ComputeFunction k_Dense_T16x16_R4x4 = new(k_Dense, "Dense_T16x16_R4x4");
        public static readonly ComputeFunction k_DenseBatched_T16x16_R4x4 = new(k_Dense, "DenseBatched_T16x16_R4x4");
        public static readonly ComputeFunction k_Gemm_T16x16_R4x4 = new(k_Dense, "Gemm_T16x16_R4x4");
        public static readonly ComputeFunction k_GemmBatched_T16x16_R4x4 = new(k_Dense, "GemmBatched_T16x16_R4x4");
        public static readonly ComputeFunction k_Dense_V_L1Cached64 = new(k_Dense, "Dense_V_L1Cached64");
        public static readonly ComputeFunction k_DenseBatched_V_L1Cached64 = new(k_Dense, "DenseBatched_V_L1Cached64");
        public static readonly ComputeFunction k_Gemm_V_L1Cached64 = new(k_Dense, "Gemm_V_L1Cached64");
        public static readonly ComputeFunction k_GemmBatched_V_L1Cached64 = new(k_Dense, "GemmBatched_V_L1Cached64");
        public static readonly ComputeFunction k_GemmT_XT_T8x8_R4x4 = new(k_GemmT, "GemmT_XT_T8x8_R4x4");
        public static readonly ComputeFunction k_GemmT_WT_T8x8_R4x4 = new(k_GemmT, "GemmT_WT_T8x8_R4x4");
        public static readonly ComputeFunction k_GemmT_XT_WT_T8x8_R4x4 = new(k_GemmT, "GemmT_XT_WT_T8x8_R4x4");
        public static readonly ComputeFunction k_AveragePoolReduce = new(k_Pool, "AveragePoolReduce");
        public static readonly ComputeFunction k_MaxPoolReduce = new(k_Pool, "MaxPoolReduce");
        public static readonly ComputeFunction k_GlobalAveragePool = new(k_Pool, "GlobalAveragePool");
        public static readonly ComputeFunction k_GlobalMaxPool = new(k_Pool, "GlobalMaxPool");
        public static readonly ComputeFunction k_AverageVariancePoolReduce = new(k_Pool, "AverageVariancePoolReduce");
        public static readonly ComputeFunction k_GlobalAverageVariancePool = new(k_Pool, "GlobalAverageVariancePool");
        public static readonly ComputeFunction k_ArgMaxReduce = new(k_Pool, "ArgMaxReduce");
        public static readonly ComputeFunction k_GlobalArgMaxReduce = new(k_Pool, "GlobalArgMaxReduce");
        public static readonly ComputeFunction k_LayerNormalizationTail = new(k_Normalization, "LayerNormalizationTail");
        public static readonly ComputeFunction k_RMSNormalizationTail = new(k_Normalization, "RMSNormalizationTail");
        public static readonly ComputeFunction k_BatchNormalization = new(k_Normalization, "BatchNormalization");
        public static readonly ComputeFunction k_LocalResponseNormalization = new(k_LocalResponseNormalizationCS, "LocalResponseNormalization");
        public static readonly ComputeFunction k_ScaleBias = new(k_Normalization, "ScaleBias");
        public static readonly ComputeFunction k_NMSBitmaskCorners = new(k_NMS, "NMSBitmaskCorners");
        public static readonly ComputeFunction k_NMSBitmaskCenter = new(k_NMS, "NMSBitmaskCenter");
        public static readonly ComputeFunction k_NMSSelect = new(k_NMS, "NMSSelect");
        public static readonly ComputeFunction k_NMSCompact = new(k_NMS, "NMSCompact");
        public static readonly ComputeFunction k_ArgMaxFloatFirst = new(k_ReduceIndices, "ArgMaxFloatFirst");
        public static readonly ComputeFunction k_ArgMinFloatFirst = new(k_ReduceIndices, "ArgMinFloatFirst");
        public static readonly ComputeFunction k_ArgMaxFloatLast = new(k_ReduceIndices, "ArgMaxFloatLast");
        public static readonly ComputeFunction k_ArgMinFloatLast = new(k_ReduceIndices, "ArgMinFloatLast");
        public static readonly ComputeFunction k_ArgMaxIntFirst = new(k_ReduceIndices, "ArgMaxIntFirst");
        public static readonly ComputeFunction k_ArgMinIntFirst = new(k_ReduceIndices, "ArgMinIntFirst");
        public static readonly ComputeFunction k_ArgMaxIntLast = new(k_ReduceIndices, "ArgMaxIntLast");
        public static readonly ComputeFunction k_ArgMinIntLast = new(k_ReduceIndices, "ArgMinIntLast");
        public static readonly ComputeFunction k_MemCopy = new(k_CopyOps, "MemCopy");
        public static readonly ComputeFunction k_MemCopyStride = new(k_CopyOps, "MemCopyStride");
        public static readonly ComputeFunction k_MemSet = new(k_CopyOps, "MemSet");
        public static readonly ComputeFunction k_Split = new(k_CopyOps, "Split");
        public static readonly ComputeFunction k_Tril = new(k_CopyOps, "Tril");
        public static readonly ComputeFunction k_Triu = new(k_CopyOps, "Triu");
        public static readonly ComputeFunction k_CastHalfToFloat = new(k_CopyOps, "CastHalfToFloat");
        public static readonly ComputeFunction k_DequantizeUint8 = new(k_CopyOps, "DequantizeUint8");
        public static readonly ComputeFunction k_Transpose2D = new(k_CopyOps, "Transpose2D");
        public static readonly ComputeFunction k_RandomUniform = new(k_Random, "RandomUniform");
        public static readonly ComputeFunction k_RandomNormal = new(k_Random, "RandomNormal");
        public static readonly ComputeFunction k_BernoulliFloat = new(k_Random, "BernoulliFloat");
        public static readonly ComputeFunction k_BernoulliInt = new(k_Random, "BernoulliInt");
        public static readonly ComputeFunction k_TopP = new(k_Random, "TopP");
        public static readonly ComputeFunction k_OneHot = new(k_IndexingOps, "OneHot");
        public static readonly ComputeFunction k_GatherND = new(k_IndexingOps, "GatherND");
        public static readonly ComputeFunction k_SliceSet = new(k_IndexingOps, "SliceSet");
        public static readonly ComputeFunction k_TopK = new(k_SortingOps, "TopK");
        public static readonly ComputeFunction k_ScatterND = new(k_ScatterOps, "ScatterND");
        public static readonly ComputeFunction k_GridSample2D = new(k_GridSample, "GridSample2D");
        public static readonly ComputeFunction k_GridSample3D = new(k_GridSample, "GridSample3D");
        public static readonly ComputeFunction k_Upsample1D_Nearest_Floor = new(k_Resize, "Upsample1D_Nearest_Floor");
        public static readonly ComputeFunction k_Upsample1D_Nearest_Ceil = new(k_Resize, "Upsample1D_Nearest_Ceil");
        public static readonly ComputeFunction k_Upsample1D_Linear_None = new(k_Resize, "Upsample1D_Linear_None");
        public static readonly ComputeFunction k_Upsample2D_Nearest_Floor = new(k_Resize, "Upsample2D_Nearest_Floor");
        public static readonly ComputeFunction k_Upsample2D_Nearest_Ceil = new(k_Resize, "Upsample2D_Nearest_Ceil");
        public static readonly ComputeFunction k_Upsample2D_Linear_None = new(k_Resize, "Upsample2D_Linear_None");
        public static readonly ComputeFunction k_Upsample3D_Nearest_Floor = new(k_Resize, "Upsample3D_Nearest_Floor");
        public static readonly ComputeFunction k_Upsample3D_Nearest_Ceil = new(k_Resize, "Upsample3D_Nearest_Ceil");
        public static readonly ComputeFunction k_Upsample3D_Linear_None = new(k_Resize, "Upsample3D_Linear_None");
        public static readonly ComputeFunction k_Resize1D_Nearest_Floor = new(k_Resize, "Resize1D_Nearest_Floor");
        public static readonly ComputeFunction k_Resize1D_Nearest_Ceil = new(k_Resize, "Resize1D_Nearest_Ceil");
        public static readonly ComputeFunction k_Resize1D_Linear_None = new(k_Resize, "Resize1D_Linear_None");
        public static readonly ComputeFunction k_DepthToSpaceDepthColumnRow = new(k_ImageBased, "DepthToSpaceDepthColumnRow");
        public static readonly ComputeFunction k_DepthToSpaceColumnRowDepth = new(k_ImageBased, "DepthToSpaceColumnRowDepth");
        public static readonly ComputeFunction k_SpaceToDepth = new(k_ImageBased, "SpaceToDepth");
        public static readonly ComputeFunction k_ScalarBroadcastPRelu = new(k_BroadcastGen, "ScalarBroadcastPRelu");
        public static readonly ComputeFunction k_BroadcastPRelu = new(k_BroadcastGen, "BroadcastPRelu");
        public static readonly ComputeFunction k_ElementwisePRelu = new(k_BroadcastGen, "ElementwisePRelu");
        public static readonly ComputeFunction k_ScalarBroadcastPowFloatFloat = new(k_BroadcastGen, "ScalarBroadcastPowFloatFloat");
        public static readonly ComputeFunction k_BroadcastPowFloatFloat = new(k_BroadcastGen, "BroadcastPowFloatFloat");
        public static readonly ComputeFunction k_ElementwisePowFloatFloat = new(k_BroadcastGen, "ElementwisePowFloatFloat");
        public static readonly ComputeFunction k_ScalarBroadcastPowFloatInt = new(k_BroadcastGen, "ScalarBroadcastPowFloatInt");
        public static readonly ComputeFunction k_BroadcastPowFloatInt = new(k_BroadcastGen, "BroadcastPowFloatInt");
        public static readonly ComputeFunction k_ElementwisePowFloatInt = new(k_BroadcastGen, "ElementwisePowFloatInt");
        public static readonly ComputeFunction k_ScalarBroadcastPowIntFloat = new(k_BroadcastGen, "ScalarBroadcastPowIntFloat");
        public static readonly ComputeFunction k_BroadcastPowIntFloat = new(k_BroadcastGen, "BroadcastPowIntFloat");
        public static readonly ComputeFunction k_ElementwisePowIntFloat = new(k_BroadcastGen, "ElementwisePowIntFloat");
        public static readonly ComputeFunction k_ScalarBroadcastPowIntInt = new(k_BroadcastGen, "ScalarBroadcastPowIntInt");
        public static readonly ComputeFunction k_BroadcastPowIntInt = new(k_BroadcastGen, "BroadcastPowIntInt");
        public static readonly ComputeFunction k_ElementwisePowIntInt = new(k_BroadcastGen, "ElementwisePowIntInt");
        public static readonly ComputeFunction k_ScalarBroadcastAddFloat = new(k_BroadcastGen, "ScalarBroadcastAddFloat");
        public static readonly ComputeFunction k_BroadcastAddFloat = new(k_BroadcastGen, "BroadcastAddFloat");
        public static readonly ComputeFunction k_ElementwiseAddFloat = new(k_BroadcastGen, "ElementwiseAddFloat");
        public static readonly ComputeFunction k_ScalarBroadcastAtan2 = new(k_BroadcastGen, "ScalarBroadcastAtan2");
        public static readonly ComputeFunction k_BroadcastAtan2 = new(k_BroadcastGen, "BroadcastAtan2");
        public static readonly ComputeFunction k_ElementwiseAtan2 = new(k_BroadcastGen, "ElementwiseAtan2");
        public static readonly ComputeFunction k_ScalarBroadcastSubFloat = new(k_BroadcastGen, "ScalarBroadcastSubFloat");
        public static readonly ComputeFunction k_BroadcastSubFloat = new(k_BroadcastGen, "BroadcastSubFloat");
        public static readonly ComputeFunction k_ElementwiseSubFloat = new(k_BroadcastGen, "ElementwiseSubFloat");
        public static readonly ComputeFunction k_ScalarBroadcastMulFloat = new(k_BroadcastGen, "ScalarBroadcastMulFloat");
        public static readonly ComputeFunction k_BroadcastMulFloat = new(k_BroadcastGen, "BroadcastMulFloat");
        public static readonly ComputeFunction k_ElementwiseMulFloat = new(k_BroadcastGen, "ElementwiseMulFloat");
        public static readonly ComputeFunction k_ScalarBroadcastDivFloat = new(k_BroadcastGen, "ScalarBroadcastDivFloat");
        public static readonly ComputeFunction k_BroadcastDivFloat = new(k_BroadcastGen, "BroadcastDivFloat");
        public static readonly ComputeFunction k_ElementwiseDivFloat = new(k_BroadcastGen, "ElementwiseDivFloat");
        public static readonly ComputeFunction k_ScalarBroadcastTruncDivFloat = new(k_BroadcastGen, "ScalarBroadcastTruncDivFloat");
        public static readonly ComputeFunction k_BroadcastTruncDivFloat = new(k_BroadcastGen, "BroadcastTruncDivFloat");
        public static readonly ComputeFunction k_ElementwiseTruncDivFloat = new(k_BroadcastGen, "ElementwiseTruncDivFloat");
        public static readonly ComputeFunction k_ScalarBroadcastFloorDivFloat = new(k_BroadcastGen, "ScalarBroadcastFloorDivFloat");
        public static readonly ComputeFunction k_BroadcastFloorDivFloat = new(k_BroadcastGen, "BroadcastFloorDivFloat");
        public static readonly ComputeFunction k_ElementwiseFloorDivFloat = new(k_BroadcastGen, "ElementwiseFloorDivFloat");
        public static readonly ComputeFunction k_ScalarBroadcastMinFloat = new(k_BroadcastGen, "ScalarBroadcastMinFloat");
        public static readonly ComputeFunction k_BroadcastMinFloat = new(k_BroadcastGen, "BroadcastMinFloat");
        public static readonly ComputeFunction k_ElementwiseMinFloat = new(k_BroadcastGen, "ElementwiseMinFloat");
        public static readonly ComputeFunction k_ScalarBroadcastMaxFloat = new(k_BroadcastGen, "ScalarBroadcastMaxFloat");
        public static readonly ComputeFunction k_BroadcastMaxFloat = new(k_BroadcastGen, "BroadcastMaxFloat");
        public static readonly ComputeFunction k_ElementwiseMaxFloat = new(k_BroadcastGen, "ElementwiseMaxFloat");
        public static readonly ComputeFunction k_ScalarBroadcastModFloat = new(k_BroadcastGen, "ScalarBroadcastModFloat");
        public static readonly ComputeFunction k_BroadcastModFloat = new(k_BroadcastGen, "BroadcastModFloat");
        public static readonly ComputeFunction k_ElementwiseModFloat = new(k_BroadcastGen, "ElementwiseModFloat");
        public static readonly ComputeFunction k_ScalarBroadcastFModFloat = new(k_BroadcastGen, "ScalarBroadcastFModFloat");
        public static readonly ComputeFunction k_BroadcastFModFloat = new(k_BroadcastGen, "BroadcastFModFloat");
        public static readonly ComputeFunction k_ElementwiseFModFloat = new(k_BroadcastGen, "ElementwiseFModFloat");
        public static readonly ComputeFunction k_ScalarBroadcastAddInt = new(k_BroadcastGen, "ScalarBroadcastAddInt");
        public static readonly ComputeFunction k_BroadcastAddInt = new(k_BroadcastGen, "BroadcastAddInt");
        public static readonly ComputeFunction k_ElementwiseAddInt = new(k_BroadcastGen, "ElementwiseAddInt");
        public static readonly ComputeFunction k_ScalarBroadcastSubInt = new(k_BroadcastGen, "ScalarBroadcastSubInt");
        public static readonly ComputeFunction k_BroadcastSubInt = new(k_BroadcastGen, "BroadcastSubInt");
        public static readonly ComputeFunction k_ElementwiseSubInt = new(k_BroadcastGen, "ElementwiseSubInt");
        public static readonly ComputeFunction k_ScalarBroadcastMulInt = new(k_BroadcastGen, "ScalarBroadcastMulInt");
        public static readonly ComputeFunction k_BroadcastMulInt = new(k_BroadcastGen, "BroadcastMulInt");
        public static readonly ComputeFunction k_ElementwiseMulInt = new(k_BroadcastGen, "ElementwiseMulInt");
        public static readonly ComputeFunction k_ScalarBroadcastTruncDivInt = new(k_BroadcastGen, "ScalarBroadcastTruncDivInt");
        public static readonly ComputeFunction k_BroadcastTruncDivInt = new(k_BroadcastGen, "BroadcastTruncDivInt");
        public static readonly ComputeFunction k_ElementwiseTruncDivInt = new(k_BroadcastGen, "ElementwiseTruncDivInt");
        public static readonly ComputeFunction k_ScalarBroadcastFloorDivInt = new(k_BroadcastGen, "ScalarBroadcastFloorDivInt");
        public static readonly ComputeFunction k_BroadcastFloorDivInt = new(k_BroadcastGen, "BroadcastFloorDivInt");
        public static readonly ComputeFunction k_ElementwiseFloorDivInt = new(k_BroadcastGen, "ElementwiseFloorDivInt");
        public static readonly ComputeFunction k_ScalarBroadcastMinInt = new(k_BroadcastGen, "ScalarBroadcastMinInt");
        public static readonly ComputeFunction k_BroadcastMinInt = new(k_BroadcastGen, "BroadcastMinInt");
        public static readonly ComputeFunction k_ElementwiseMinInt = new(k_BroadcastGen, "ElementwiseMinInt");
        public static readonly ComputeFunction k_ScalarBroadcastMaxInt = new(k_BroadcastGen, "ScalarBroadcastMaxInt");
        public static readonly ComputeFunction k_BroadcastMaxInt = new(k_BroadcastGen, "BroadcastMaxInt");
        public static readonly ComputeFunction k_ElementwiseMaxInt = new(k_BroadcastGen, "ElementwiseMaxInt");
        public static readonly ComputeFunction k_ScalarBroadcastModInt = new(k_BroadcastGen, "ScalarBroadcastModInt");
        public static readonly ComputeFunction k_BroadcastModInt = new(k_BroadcastGen, "BroadcastModInt");
        public static readonly ComputeFunction k_ElementwiseModInt = new(k_BroadcastGen, "ElementwiseModInt");
        public static readonly ComputeFunction k_ScalarBroadcastFModInt = new(k_BroadcastGen, "ScalarBroadcastFModInt");
        public static readonly ComputeFunction k_BroadcastFModInt = new(k_BroadcastGen, "BroadcastFModInt");
        public static readonly ComputeFunction k_ElementwiseFModInt = new(k_BroadcastGen, "ElementwiseFModInt");
        public static readonly ComputeFunction k_ScalarBroadcastBitwiseAnd = new(k_BroadcastGen, "ScalarBroadcastBitwiseAnd");
        public static readonly ComputeFunction k_BroadcastBitwiseAnd = new(k_BroadcastGen, "BroadcastBitwiseAnd");
        public static readonly ComputeFunction k_ElementwiseBitwiseAnd = new(k_BroadcastGen, "ElementwiseBitwiseAnd");
        public static readonly ComputeFunction k_ScalarBroadcastBitwiseOr = new(k_BroadcastGen, "ScalarBroadcastBitwiseOr");
        public static readonly ComputeFunction k_BroadcastBitwiseOr = new(k_BroadcastGen, "BroadcastBitwiseOr");
        public static readonly ComputeFunction k_ElementwiseBitwiseOr = new(k_BroadcastGen, "ElementwiseBitwiseOr");
        public static readonly ComputeFunction k_ScalarBroadcastBitwiseXor = new(k_BroadcastGen, "ScalarBroadcastBitwiseXor");
        public static readonly ComputeFunction k_BroadcastBitwiseXor = new(k_BroadcastGen, "BroadcastBitwiseXor");
        public static readonly ComputeFunction k_ElementwiseBitwiseXor = new(k_BroadcastGen, "ElementwiseBitwiseXor");
        public static readonly ComputeFunction k_Conv2D_KxK = new(k_ConvGen, "Conv2D_KxK");
        public static readonly ComputeFunction k_Conv2D_1x1 = new(k_ConvGen, "Conv2D_1x1");
        public static readonly ComputeFunction k_Conv1D_KxK = new(k_ConvGen, "Conv1D_KxK");
        public static readonly ComputeFunction k_Conv1D_1x1 = new(k_ConvGen, "Conv1D_1x1");
        public static readonly ComputeFunction k_ConvTranspose2D_KxK = new(k_ConvTransposeGen, "ConvTranspose2D_KxK");
        public static readonly ComputeFunction k_ConvTranspose1D_KxK = new(k_ConvTransposeGen, "ConvTranspose1D_KxK");
        public static readonly ComputeFunction k_ReduceMaxFloat = new(k_ReductionGen, "ReduceMaxFloat");
        public static readonly ComputeFunction k_GlobalReduceMaxFloat = new(k_ReductionGen, "GlobalReduceMaxFloat");
        public static readonly ComputeFunction k_ReduceMinFloat = new(k_ReductionGen, "ReduceMinFloat");
        public static readonly ComputeFunction k_GlobalReduceMinFloat = new(k_ReductionGen, "GlobalReduceMinFloat");
        public static readonly ComputeFunction k_ReduceSumFloat = new(k_ReductionGen, "ReduceSumFloat");
        public static readonly ComputeFunction k_GlobalReduceSumFloat = new(k_ReductionGen, "GlobalReduceSumFloat");
        public static readonly ComputeFunction k_ReduceSumSquareFloat = new(k_ReductionGen, "ReduceSumSquareFloat");
        public static readonly ComputeFunction k_GlobalReduceSumSquareFloat = new(k_ReductionGen, "GlobalReduceSumSquareFloat");
        public static readonly ComputeFunction k_ReduceMeanFloat = new(k_ReductionGen, "ReduceMeanFloat");
        public static readonly ComputeFunction k_ReduceMeanSquareFloat = new(k_ReductionGen, "ReduceMeanSquareFloat");
        public static readonly ComputeFunction k_GlobalReduceMeanFloat = new(k_ReductionGen, "GlobalReduceMeanFloat");
        public static readonly ComputeFunction k_GlobalReduceMeanSquareFloat = new(k_ReductionGen, "GlobalReduceMeanSquareFloat");
        public static readonly ComputeFunction k_ReduceProdFloat = new(k_ReductionGen, "ReduceProdFloat");
        public static readonly ComputeFunction k_GlobalReduceProdFloat = new(k_ReductionGen, "GlobalReduceProdFloat");
        public static readonly ComputeFunction k_ReduceL1Float = new(k_ReductionGen, "ReduceL1Float");
        public static readonly ComputeFunction k_GlobalReduceL1Float = new(k_ReductionGen, "GlobalReduceL1Float");
        public static readonly ComputeFunction k_ReduceL2Float = new(k_ReductionGen, "ReduceL2Float");
        public static readonly ComputeFunction k_GlobalReduceL2Float = new(k_ReductionGen, "GlobalReduceL2Float");
        public static readonly ComputeFunction k_ReduceSqrtFloat = new(k_ReductionGen, "ReduceSqrtFloat");
        public static readonly ComputeFunction k_GlobalReduceSqrtFloat = new(k_ReductionGen, "GlobalReduceSqrtFloat");
        public static readonly ComputeFunction k_ReduceLogSumFloat = new(k_ReductionGen, "ReduceLogSumFloat");
        public static readonly ComputeFunction k_GlobalReduceLogSumFloat = new(k_ReductionGen, "GlobalReduceLogSumFloat");
        public static readonly ComputeFunction k_ReduceLogSumExpFloat = new(k_ReductionGen, "ReduceLogSumExpFloat");
        public static readonly ComputeFunction k_GlobalReduceLogSumExpFloat = new(k_ReductionGen, "GlobalReduceLogSumExpFloat");
        public static readonly ComputeFunction k_ReduceSumExpFloat = new(k_ReductionGen, "ReduceSumExpFloat");
        public static readonly ComputeFunction k_GlobalReduceSumExpFloat = new(k_ReductionGen, "GlobalReduceSumExpFloat");
        public static readonly ComputeFunction k_ReduceMaxInt = new(k_ReductionGen, "ReduceMaxInt");
        public static readonly ComputeFunction k_GlobalReduceMaxInt = new(k_ReductionGen, "GlobalReduceMaxInt");
        public static readonly ComputeFunction k_ReduceMinInt = new(k_ReductionGen, "ReduceMinInt");
        public static readonly ComputeFunction k_GlobalReduceMinInt = new(k_ReductionGen, "GlobalReduceMinInt");
        public static readonly ComputeFunction k_ReduceSumInt = new(k_ReductionGen, "ReduceSumInt");
        public static readonly ComputeFunction k_GlobalReduceSumInt = new(k_ReductionGen, "GlobalReduceSumInt");
        public static readonly ComputeFunction k_ReduceSumSquareInt = new(k_ReductionGen, "ReduceSumSquareInt");
        public static readonly ComputeFunction k_GlobalReduceSumSquareInt = new(k_ReductionGen, "GlobalReduceSumSquareInt");
        public static readonly ComputeFunction k_ReduceProdInt = new(k_ReductionGen, "ReduceProdInt");
        public static readonly ComputeFunction k_GlobalReduceProdInt = new(k_ReductionGen, "GlobalReduceProdInt");
        public static readonly ComputeFunction k_ReduceL1Int = new(k_ReductionGen, "ReduceL1Int");
        public static readonly ComputeFunction k_GlobalReduceL1Int = new(k_ReductionGen, "GlobalReduceL1Int");
        public static readonly ComputeFunction k_UnrolledReduceMaxFloat = new(k_ReductionUnrolledGen, "UnrolledReduceMaxFloat");
        public static readonly ComputeFunction k_UnrolledReduceMinFloat = new(k_ReductionUnrolledGen, "UnrolledReduceMinFloat");
        public static readonly ComputeFunction k_UnrolledReduceSumFloat = new(k_ReductionUnrolledGen, "UnrolledReduceSumFloat");
        public static readonly ComputeFunction k_UnrolledReduceSumSquareFloat = new(k_ReductionUnrolledGen, "UnrolledReduceSumSquareFloat");
        public static readonly ComputeFunction k_UnrolledReduceMeanFloat = new(k_ReductionUnrolledGen, "UnrolledReduceMeanFloat");
        public static readonly ComputeFunction k_UnrolledReduceMeanSquareFloat = new(k_ReductionUnrolledGen, "UnrolledReduceMeanSquareFloat");
        public static readonly ComputeFunction k_UnrolledReduceProdFloat = new(k_ReductionUnrolledGen, "UnrolledReduceProdFloat");
        public static readonly ComputeFunction k_UnrolledReduceL1Float = new(k_ReductionUnrolledGen, "UnrolledReduceL1Float");
        public static readonly ComputeFunction k_UnrolledReduceL2Float = new(k_ReductionUnrolledGen, "UnrolledReduceL2Float");
        public static readonly ComputeFunction k_UnrolledReduceSqrtFloat = new(k_ReductionUnrolledGen, "UnrolledReduceSqrtFloat");
        public static readonly ComputeFunction k_UnrolledReduceLogSumFloat = new(k_ReductionUnrolledGen, "UnrolledReduceLogSumFloat");
        public static readonly ComputeFunction k_UnrolledReduceLogSumExpFloat = new(k_ReductionUnrolledGen, "UnrolledReduceLogSumExpFloat");
        public static readonly ComputeFunction k_UnrolledReduceSumExpFloat = new(k_ReductionUnrolledGen, "UnrolledReduceSumExpFloat");
        public static readonly ComputeFunction k_UnrolledReduceMaxInt = new(k_ReductionUnrolledGen, "UnrolledReduceMaxInt");
        public static readonly ComputeFunction k_UnrolledReduceMinInt = new(k_ReductionUnrolledGen, "UnrolledReduceMinInt");
        public static readonly ComputeFunction k_UnrolledReduceSumInt = new(k_ReductionUnrolledGen, "UnrolledReduceSumInt");
        public static readonly ComputeFunction k_UnrolledReduceSumSquareInt = new(k_ReductionUnrolledGen, "UnrolledReduceSumSquareInt");
        public static readonly ComputeFunction k_UnrolledReduceProdInt = new(k_ReductionUnrolledGen, "UnrolledReduceProdInt");
        public static readonly ComputeFunction k_UnrolledReduceL1Int = new(k_ReductionUnrolledGen, "UnrolledReduceL1Int");
        public static readonly ComputeFunction k_LeakyRelu = new(k_PointwiseUnaryGen, "LeakyRelu");
        public static readonly ComputeFunction k_Swish = new(k_PointwiseUnaryGen, "Swish");
        public static readonly ComputeFunction k_Relu = new(k_PointwiseUnaryGen, "Relu");
        public static readonly ComputeFunction k_Relu6 = new(k_PointwiseUnaryGen, "Relu6");
        public static readonly ComputeFunction k_Mish = new(k_PointwiseUnaryGen, "Mish");
        public static readonly ComputeFunction k_Tanh = new(k_PointwiseUnaryGen, "Tanh");
        public static readonly ComputeFunction k_Sigmoid = new(k_PointwiseUnaryGen, "Sigmoid");
        public static readonly ComputeFunction k_GeluFast = new(k_PointwiseUnaryGen, "GeluFast");
        public static readonly ComputeFunction k_HardSigmoid = new(k_PointwiseUnaryGen, "HardSigmoid");
        public static readonly ComputeFunction k_Gelu = new(k_PointwiseUnaryGen, "Gelu");
        public static readonly ComputeFunction k_Erf = new(k_PointwiseUnaryGen, "Erf");
        public static readonly ComputeFunction k_Celu = new(k_PointwiseUnaryGen, "Celu");
        public static readonly ComputeFunction k_Shrink = new(k_PointwiseUnaryGen, "Shrink");
        public static readonly ComputeFunction k_ThresholdedRelu = new(k_PointwiseUnaryGen, "ThresholdedRelu");
        public static readonly ComputeFunction k_Elu = new(k_PointwiseUnaryGen, "Elu");
        public static readonly ComputeFunction k_Selu = new(k_PointwiseUnaryGen, "Selu");
        public static readonly ComputeFunction k_Softplus = new(k_PointwiseUnaryGen, "Softplus");
        public static readonly ComputeFunction k_Ceil = new(k_PointwiseUnaryGen, "Ceil");
        public static readonly ComputeFunction k_Floor = new(k_PointwiseUnaryGen, "Floor");
        public static readonly ComputeFunction k_Trunc = new(k_PointwiseUnaryGen, "Trunc");
        public static readonly ComputeFunction k_Round = new(k_PointwiseUnaryGen, "Round");
        public static readonly ComputeFunction k_Reciprocal = new(k_PointwiseUnaryGen, "Reciprocal");
        public static readonly ComputeFunction k_Exp = new(k_PointwiseUnaryGen, "Exp");
        public static readonly ComputeFunction k_Expm1 = new(k_PointwiseUnaryGen, "Expm1");
        public static readonly ComputeFunction k_Log = new(k_PointwiseUnaryGen, "Log");
        public static readonly ComputeFunction k_Log10 = new(k_PointwiseUnaryGen, "Log10");
        public static readonly ComputeFunction k_Log1p = new(k_PointwiseUnaryGen, "Log1p");
        public static readonly ComputeFunction k_Log2 = new(k_PointwiseUnaryGen, "Log2");
        public static readonly ComputeFunction k_Rsqrt = new(k_PointwiseUnaryGen, "Rsqrt");
        public static readonly ComputeFunction k_Sqrt = new(k_PointwiseUnaryGen, "Sqrt");
        public static readonly ComputeFunction k_Acos = new(k_PointwiseUnaryGen, "Acos");
        public static readonly ComputeFunction k_Acosh = new(k_PointwiseUnaryGen, "Acosh");
        public static readonly ComputeFunction k_Asin = new(k_PointwiseUnaryGen, "Asin");
        public static readonly ComputeFunction k_Asinh = new(k_PointwiseUnaryGen, "Asinh");
        public static readonly ComputeFunction k_Atan = new(k_PointwiseUnaryGen, "Atan");
        public static readonly ComputeFunction k_Atanh = new(k_PointwiseUnaryGen, "Atanh");
        public static readonly ComputeFunction k_Cos = new(k_PointwiseUnaryGen, "Cos");
        public static readonly ComputeFunction k_Cosh = new(k_PointwiseUnaryGen, "Cosh");
        public static readonly ComputeFunction k_Sin = new(k_PointwiseUnaryGen, "Sin");
        public static readonly ComputeFunction k_Sinh = new(k_PointwiseUnaryGen, "Sinh");
        public static readonly ComputeFunction k_Tan = new(k_PointwiseUnaryGen, "Tan");
        public static readonly ComputeFunction k_Softsign = new(k_PointwiseUnaryGen, "Softsign");
        public static readonly ComputeFunction k_HardSwish = new(k_PointwiseUnaryGen, "HardSwish");
        public static readonly ComputeFunction k_AbsInt = new(k_PointwiseUnaryGen, "AbsInt");
        public static readonly ComputeFunction k_AbsFloat = new(k_PointwiseUnaryGen, "AbsFloat");
        public static readonly ComputeFunction k_NegInt = new(k_PointwiseUnaryGen, "NegInt");
        public static readonly ComputeFunction k_NegFloat = new(k_PointwiseUnaryGen, "NegFloat");
        public static readonly ComputeFunction k_SquareInt = new(k_PointwiseUnaryGen, "SquareInt");
        public static readonly ComputeFunction k_SquareFloat = new(k_PointwiseUnaryGen, "SquareFloat");
        public static readonly ComputeFunction k_IsNaN = new(k_PointwiseUnaryGen, "IsNaN");
        public static readonly ComputeFunction k_CastIntToFloat = new(k_PointwiseUnaryGen, "CastIntToFloat");
        public static readonly ComputeFunction k_CastFloatToInt = new(k_PointwiseUnaryGen, "CastFloatToInt");
        public static readonly ComputeFunction k_SignFloat = new(k_PointwiseUnaryGen, "SignFloat");
        public static readonly ComputeFunction k_SignInt = new(k_PointwiseUnaryGen, "SignInt");
        public static readonly ComputeFunction k_Not = new(k_PointwiseUnaryGen, "Not");
        public static readonly ComputeFunction k_BitwiseNot = new(k_PointwiseUnaryGen, "BitwiseNot");
        public static readonly ComputeFunction k_HardTanh = new(k_PointwiseUnaryGen, "HardTanh");
        public static readonly ComputeFunction k_ScalarMadFloat = new(k_PointwiseUnaryGen, "ScalarMadFloat");
        public static readonly ComputeFunction k_ScalarMadInt = new(k_PointwiseUnaryGen, "ScalarMadInt");
        public static readonly ComputeFunction k_RangeFloat = new(k_PointwiseUnaryGen, "RangeFloat");
        public static readonly ComputeFunction k_RangeInt = new(k_PointwiseUnaryGen, "RangeInt");
        public static readonly ComputeFunction k_Transpose = new(k_GenericA, "Transpose");
        public static readonly ComputeFunction k_InstanceNormalizationTail = new(k_GenericA, "InstanceNormalizationTail");
        public static readonly ComputeFunction k_PadBorderND = new(k_PadA, "PadBorderND");
        public static readonly ComputeFunction k_PadReflectND = new(k_PadA, "PadReflectND");
        public static readonly ComputeFunction k_PadSymmetricND = new(k_PadA, "PadSymmetricND");
        public static readonly ComputeFunction k_PadEdgeND = new(k_PadA, "PadEdgeND");
        public static readonly ComputeFunction k_PadWrapND = new(k_PadA, "PadWrapND");
        public static readonly ComputeFunction k_MaxPool3D = new(k_PoolA, "MaxPool3D");
        public static readonly ComputeFunction k_MaxPool2D = new(k_PoolA, "MaxPool2D");
        public static readonly ComputeFunction k_MaxPool1D = new(k_PoolA, "MaxPool1D");
        public static readonly ComputeFunction k_AveragePool3D = new(k_PoolA, "AveragePool3D");
        public static readonly ComputeFunction k_AveragePool2D = new(k_PoolA, "AveragePool2D");
        public static readonly ComputeFunction k_AveragePool1D = new(k_PoolA, "AveragePool1D");
        public static readonly ComputeFunction k_EinsumOne = new(k_Einsum, "EinsumOne");
        public static readonly ComputeFunction k_EinsumTwo = new(k_Einsum, "EinsumTwo");
        public static readonly ComputeFunction k_Tile = new(k_IndexingOpsA, "Tile");
        public static readonly ComputeFunction k_Gather = new(k_IndexingOpsA, "Gather");
        public static readonly ComputeFunction k_GatherElementsFast = new(k_IndexingOpsA, "GatherElementsFast");
        public static readonly ComputeFunction k_GatherElements = new(k_IndexingOpsA, "GatherElements");
        public static readonly ComputeFunction k_ScatterElementsFast = new(k_IndexingOpsA, "ScatterElementsFast");
        public static readonly ComputeFunction k_ScatterElements = new(k_IndexingOpsA, "ScatterElements");
        public static readonly ComputeFunction k_Expand = new(k_IndexingOpsA, "Expand");
        public static readonly ComputeFunction k_Slice = new(k_IndexingOpsA, "Slice");
        public static readonly ComputeFunction k_Where = new(k_Logical, "Where");
        public static readonly ComputeFunction k_BlackmanWindow = new(k_Spectral, "BlackmanWindow");
        public static readonly ComputeFunction k_HammingWindow = new(k_Spectral, "HammingWindow");
        public static readonly ComputeFunction k_HannWindow = new(k_Spectral, "HannWindow");
        public static readonly ComputeFunction k_MelWeightMatrix = new(k_Spectral, "MelWeightMatrix");
        public static readonly ComputeFunction k_BitonicSortStep = new(k_BitonicSort, "BitonicSortStep");
        public static readonly ComputeFunction k_BitonicSortKeyStep = new(k_BitonicSort, "BitonicSortKeyStep");
        public static readonly ComputeFunction k_RoiAlign = new(k_RoiAlignShader, "RoiAlign");
        public static readonly ComputeFunction k_Clip = new(k_ClipShader, "Clip");

        // Windowed DFT Matrix
        public static readonly ComputeFunction k_WindowedDFTMatrixSplitReImTo2Rows_Half_32x32Data_256T = new(k_WindowedDFTMatrix, "k_WindowedDFTMatrixSplitReImTo2Rows_Half_32x32Data_256T");
        public static readonly ComputeFunction k_WindowedDFTMatrixPackedComplex_Half_32x32Data_256T = new(k_WindowedDFTMatrix, "k_WindowedDFTMatrixPackedComplex_Half_32x32Data_256T");

        public static readonly ComputeFunction k_IDFTMatrixSplitReImTo2Rows_Half_32x32Data_256T = new(k_WindowedDFTMatrix, "k_IDFTMatrixSplitReImTo2Rows_Half_32x32Data_256T");
        public static readonly ComputeFunction k_IDFTMatrixPackedComplex_Half_32x32Data_256T = new(k_WindowedDFTMatrix, "k_IDFTMatrixPackedComplex_Half_32x32Data_256T");
        static readonly int[] k_AvailableMinDivVariants =
        {
            1, // GROUP_OC_PER_GROUP_LT_ANYVARIANTS
            //int.MaxValue, // MinDivEqual2, not available for now
            //int.MaxValue, // MinDivEqual4, not available for now
            //int.MaxValue, // MinDivEqual8, not available for now
            //int.MaxValue, // MinDivEqual16, not available for now
            //int.MaxValue, // MinDivEqual32, not available for now
            64 // MinDivEqual64
        };

        static readonly LocalKeyword[] k_ConvGroupSizeBoundKeywords =
        {
            new(k_ConvGeneric, "GROUP_OC_PER_GROUP_LT_ANYVARIANTS"),

            //new(k_ConvGeneric, "GROUP_OC_PER_GROUP_MINDIV_2"),
            //new(k_ConvGeneric, "GROUP_OC_PER_GROUP_MINDIV_4"),
            //new(k_ConvGeneric, "GROUP_OC_PER_GROUP_MINDIV_8"),
            //new(k_ConvGeneric, "GROUP_OC_PER_GROUP_MINDIV_16"),
            //new(k_ConvGeneric, "GROUP_OC_PER_GROUP_MINDIV_32"),
            new(k_ConvGeneric, "GROUP_OC_PER_GROUP_MINDIV_64")
        };
        static readonly LocalKeyword[] k_STFTPixelKeywords =
        {
            new(k_STFTshader, "SIG_REAL_OUT_4PACK_ON_REALIMA"),
            new(k_STFTshader, "SIG_REAL_OUT_4PACK_ON_FREQ_MAT_SPLIT_REAL_IMA_ON_ROWS"),
            new(k_STFTshader, "SIG_REAL_OUT_4PACK_ON_FREQ"),
            new(k_STFTshader, "SIG_COMPLEX_4PACK_ON_REALIMA"),
            new(k_STFTshader, "SIG_COMPLEX_4PACK_ON_TIME")
        };

        static STFTPixelVariantKeyword STFTPixelGetVariant(bool signalIsReal, bool splitRowMatrix, bool signalTexelChansOnTime, bool outputTexelChansOnFreq)
        {
            if (splitRowMatrix)
            {
                Logger.AssertIsTrue(signalIsReal && signalTexelChansOnTime, "Invalid STFT pixel shader variant requested");
                return STFTPixelVariantKeyword.SigRealOut4PackOnFreqMatSlitRealImaOnRows;
            }

            // !splitRowMatrix from here on:
            if (signalIsReal && !outputTexelChansOnFreq)
                return STFTPixelVariantKeyword.SigRealOut4PackOnRealIma;

            if (signalIsReal && outputTexelChansOnFreq)
                return STFTPixelVariantKeyword.SigRealOut4PackOnFreq;
            ;

            // from here on:
            // signalIsReal == false
            // outputTexelChansOnFreq = false as for complex signal, output is always packed with texel channel # == real/imaginary pair (2 channels unused)
            Logger.AssertIsTrue(!signalIsReal && !outputTexelChansOnFreq, "Invalid STFT pixel shader variant requested, complex signal expected and output channels on real/ima axis.");

            if (signalTexelChansOnTime)
                return STFTPixelVariantKeyword.SigComplex4PackOnTtime;
            return STFTPixelVariantKeyword.SigComplex4PackOnRealIma;
        }

        static void STFTPixelSetVariantKeywordOnMaterial(Material mat, STFTPixelVariantKeyword kw)
        {
            mat.SetKeyword(k_STFTPixelKeywords[(int)STFTPixelVariantKeyword.SigRealOut4PackOnFreqMatSlitRealImaOnRows], kw == STFTPixelVariantKeyword.SigRealOut4PackOnFreqMatSlitRealImaOnRows);
            mat.SetKeyword(k_STFTPixelKeywords[(int)STFTPixelVariantKeyword.SigRealOut4PackOnRealIma], kw == STFTPixelVariantKeyword.SigRealOut4PackOnRealIma);
            mat.SetKeyword(k_STFTPixelKeywords[(int)STFTPixelVariantKeyword.SigRealOut4PackOnFreq], kw == STFTPixelVariantKeyword.SigRealOut4PackOnFreq);
            mat.SetKeyword(k_STFTPixelKeywords[(int)STFTPixelVariantKeyword.SigComplex4PackOnTtime], kw == STFTPixelVariantKeyword.SigComplex4PackOnTtime);
            mat.SetKeyword(k_STFTPixelKeywords[(int)STFTPixelVariantKeyword.SigComplex4PackOnRealIma], kw == STFTPixelVariantKeyword.SigComplex4PackOnRealIma);
        }

        public static void STFTPixelSetVariantOnMaterial(Material mat, bool signalIsReal, bool splitRowMatrix, bool signalTexelChansOnTime, bool outputTexelChansOnFreq)
        {
            STFTPixelSetVariantKeywordOnMaterial(mat, STFTPixelGetVariant(signalIsReal, splitRowMatrix, signalTexelChansOnTime, outputTexelChansOnFreq));
        }

        static ConvNumOutChannelPerGroupMinDivKeyword GroupedConvGenericGroupGetMinDivVariant(uint numOcPerGroup)
        {
            // maxval is used for an absent variant

            var i = k_AvailableMinDivVariants.Length - 1;
            for (; i >= 0; i--)
                if (k_AvailableMinDivVariants[i] <= numOcPerGroup && numOcPerGroup % k_AvailableMinDivVariants[i] == 0)
                    break;

            // i will be -1 only if numOcPerGroup == 0, which means no groups,
            // 1 means LowerThanAnyVariants, pick default variant, etc.
            var keywEnum = (ConvNumOutChannelPerGroupMinDivKeyword)i;
            return keywEnum;
        }

        static void GroupedConvGenericSetGroupMinDivVariantKeyword(CommandBuffer cb, ConvNumOutChannelPerGroupMinDivKeyword variant)
        {
            cb.SetKeyword(k_ConvGeneric, k_ConvGroupSizeBoundKeywords[(int)ConvNumOutChannelPerGroupMinDivKeyword.LowerThanAnyVariants], ConvNumOutChannelPerGroupMinDivKeyword.LowerThanAnyVariants == variant);

            // Re-enable those as needed when more variants are defined
            //cb.SetKeyword(sh, k_ConvGroupSizeBoundKeywords[(int)ConvNumOutChannelPerGroupMinDivKeyword.MinDivEqual2], ConvNumOutChannelPerGroupMinDivKeyword.MinDivEqual2 == variant);
            //cb.SetKeyword(sh, k_ConvGroupSizeBoundKeywords[(int)ConvNumOutChannelPerGroupMinDivKeyword.MinDivEqual4], ConvNumOutChannelPerGroupMinDivKeyword.MinDivEqual4 == variant);
            //cb.SetKeyword(sh, k_ConvGroupSizeBoundKeywords[(int)ConvNumOutChannelPerGroupMinDivKeyword.MinDivEqual8], ConvNumOutChannelPerGroupMinDivKeyword.MinDivEqual8 == variant);
            //cb.SetKeyword(sh, k_ConvGroupSizeBoundKeywords[(int)ConvNumOutChannelPerGroupMinDivKeyword.MinDivEqual16], ConvNumOutChannelPerGroupMinDivKeyword.MinDivEqual16 == variant);
            //cb.SetKeyword(sh, k_ConvGroupSizeBoundKeywords[(int)ConvNumOutChannelPerGroupMinDivKeyword.MinDivEqual32], ConvNumOutChannelPerGroupMinDivKeyword.MinDivEqual32 == variant);
            cb.SetKeyword(k_ConvGeneric, k_ConvGroupSizeBoundKeywords[(int)ConvNumOutChannelPerGroupMinDivKeyword.MinDivEqual64], ConvNumOutChannelPerGroupMinDivKeyword.MinDivEqual64 == variant);
        }

        public static void GroupedConvGenericSetGroupMinDivVariantKeyword(CommandBuffer cb, int numOutputChannelsPerGroup)
        {
            GroupedConvGenericSetGroupMinDivVariantKeyword(cb, GroupedConvGenericGroupGetMinDivVariant((uint)numOutputChannelsPerGroup));
        }

        public static void GroupedConvGenericDisableGroups(CommandBuffer cb)
        {
            GroupedConvGenericSetGroupMinDivVariantKeyword(cb, ConvNumOutChannelPerGroupMinDivKeyword.NoGroups);
        }

        // ConvGeneric variants
        enum ConvNumOutChannelPerGroupMinDivKeyword
        {
            NoGroups = -1,
            LowerThanAnyVariants = 0,

            //MinDivEqual2, // MinDivEqual2, not available for now
            //MinDivEqual4, // MinDivEqual4, not available for now
            //MinDivEqual8, // MinDivEqual8, not available for now
            //MinDivEqual16,// MinDivEqual16, not available for now
            //MinDivEqual32,// MinDivEqual32, not available for now
            MinDivEqual64
        }

        // STFT fragment shader variants:
        enum STFTPixelVariantKeyword
        {
            SigRealOut4PackOnRealIma,
            SigRealOut4PackOnFreqMatSlitRealImaOnRows,
            SigRealOut4PackOnFreq,
            SigComplex4PackOnRealIma,
            SigComplex4PackOnTtime
        }
    }

    class ComputeFunction
    {
        public int kernelIndex;
        public ProfilerMarker profilerMarker;
        public ComputeShader shader;
        public uint threadGroupSizeX;
        public uint threadGroupSizeY;
        public uint threadGroupSizeZ;

        public ComputeFunction(ComputeShader shader, string kernelName)
        {
            this.shader = shader;
            kernelIndex = shader.FindKernel(kernelName);
            this.shader.GetKernelThreadGroupSizes(kernelIndex, out threadGroupSizeX, out threadGroupSizeY, out threadGroupSizeZ);
            profilerMarker = new ProfilerMarker(kernelName);
        }
    }
}
