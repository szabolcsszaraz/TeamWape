using UnityEngine;
using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using static Unity.InferenceEngine.CPUTensorData;

namespace Unity.InferenceEngine
{
    partial class CPUBackend
    {
        static FencedMemoryAlloc s_tmpMemBlock0 = new ();
        static BLASPlugin s_BLAS = BLASPluginFactory.CreateNativeBLASPlugin();

        // Do we need this class or operate on CPUTensorData instead?
        TensorClassPool<Tensor<float>> m_TensorFloatPool = new ();
        TensorClassPool<Tensor<int>> m_TensorIntPool = new ();
        TensorDataPool<CPUTensorData> m_MemoryPool = new ();

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticsOnLoad()
        {
            s_tmpMemBlock0 = new ();
            s_BLAS = BLASPluginFactory.CreateNativeBLASPlugin();
        }
#endif

        /// <summary>
        /// Initializes and returns an instance of `CPUBackend`.
        /// </summary>
        public CPUBackend() { }

        /// <summary>
        /// Disposes of the ops and any associated memory.
        /// </summary>
        public void Dispose()
        {
            m_MemoryPool?.Dispose();
            m_MemoryPool = null;
        }

        Tensor<float> AllocTensorFloat(TensorShape shape)
        {
            CPUTensorData data = m_MemoryPool.AdoptFromPool(shape.length);
            if (data == null)
                data = new CPUTensorData(shape.length);
            var tensor = m_TensorFloatPool.AdoptFromPool();
            if (tensor == null)
                tensor = new Tensor<float>(shape, data: null);

            tensor.shape = shape;
            tensor.count = shape.length;
            tensor.dataOnBackend = data;
            return tensor;
        }

        Tensor<int> AllocTensorInt(TensorShape shape)
        {
            CPUTensorData data = m_MemoryPool.AdoptFromPool(shape.length);
            if (data == null)
                data = new CPUTensorData(shape.length);
            var tensor = m_TensorIntPool.AdoptFromPool();
            if (tensor == null)
                tensor = new Tensor<int>(shape, data: null);

            tensor.shape = shape;
            tensor.count = shape.length;
            tensor.dataOnBackend = data;
            return tensor;
        }

        void ReleaseTensorFloat(Tensor<float> tensor)
        {
            if (tensor == null)
                return;
            m_MemoryPool.ReleaseToPool(tensor.dataOnBackend as CPUTensorData);
            tensor.dataOnBackend = null;
            m_TensorFloatPool.ReleaseToPool(tensor as Tensor<float>);
        }

        void ReleaseTensorInt(Tensor<int> tensor)
        {
            if (tensor == null)
                return;
            m_MemoryPool.ReleaseToPool(tensor.dataOnBackend as CPUTensorData);
            tensor.dataOnBackend = null;
            m_TensorIntPool.ReleaseToPool(tensor as Tensor<int>);
        }

        void ReleaseTensor(Tensor tensor)
        {
            if (tensor is Tensor<int> intTensor)
                ReleaseTensorInt(intTensor);
            else
                ReleaseTensorFloat(tensor as Tensor<float>);
        }

        unsafe void ScheduleSGEMM(
            Tensor X, int XM, int XN,
            Tensor Y, int YM, int YN,
            Tensor O, int OM, int ON,
            bool transposeA = false, bool transposeB = false, bool accumulateC = false, int offsetX = 0, int offsetY = 0, int offsetO = 0)
        {
            var pinX = Pin(X);
            var pinY = Pin(Y);
            var pinO = Pin(O, clearOnInit: accumulateC);
            var data = new BatchMatrixMultiplyHelper();
            data.A = (float*)pinX.rawPtr + offsetX;
            data.B = (float*)pinY.rawPtr + offsetY;
            data.C = (float*)pinO.rawPtr + offsetO;
            data.M = transposeA ? XN : XM;
            data.N = transposeB ? YM : YN;
            data.K = transposeA ? XM : XN;
            data.lda = XN;
            data.ldb = YN;
            data.ldc = ON;
            data.transposeA = transposeA;
            data.transposeB = transposeB;
            data.accumulateC = accumulateC;
            data.batchCount = 1;

            JobHandle dependsOn = JobHandle.CombineDependencies(pinO.reuse, pinX.fence, pinY.fence);
            JobHandle jobFence;

            if (s_BLAS != null)
            {
                var job = new BatchMatrixMultiplyWithPluginJob();
                job.data = data;
                jobFence = job.Schedule(dependsOn);
            }
            else
            {
                var job = new BatchMatrixMultiplyJob();
                job.data = data;
                jobFence = job.Schedule(dependsOn);
            }

            pinO.fence = pinX.reuse = pinY.reuse = jobFence;
        }

        /// <inheritdoc/>
        public void MatMul2D(Tensor<float> X, Tensor<float> Y, Tensor<float> O, bool xTranspose, bool yTranspose)
        {
            ScheduleSGEMM(
                X, X.shape[0], X.shape[1],
                Y, Y.shape[0], Y.shape[1],
                O, O.shape[0], O.shape[1],
                transposeA: xTranspose, transposeB: yTranspose);
        }

        /// <inheritdoc/>
        public void MatMul(Tensor<float> X, Tensor<float> Y, Tensor<float> O)
        {
            var pinX = Pin(X);
            var pinY = Pin(Y);
            var pinO = Pin(O);

            var xShape = X.shape.rank == 1 ? new TensorShape(1, X.shape[0]) : X.shape;
            var yShape = Y.shape.rank == 1 ? new TensorShape(Y.shape[0], 1) : Y.shape;

            var data = new BatchMatrixMultiplyHelper();
            unsafe
            {
                data.A = (float*)pinX.rawPtr;
                data.B = (float*)pinY.rawPtr;
                data.C = (float*)pinO.rawPtr;
                data.M = xShape[-2];
                data.N = yShape[-1];
                data.K = xShape[-1];
                data.lda = data.K;
                data.ldb = data.N;
                data.ldc = data.N;
                data.Prepare(xShape, yShape);
            }

            JobHandle dependsOn = JobHandle.CombineDependencies(pinO.reuse, pinX.fence, pinY.fence);
            JobHandle jobFence;

            if (s_BLAS != null)
            {
                var job = new BatchMatrixMultiplyWithPluginJob();
                job.data = data;
                jobFence = job.Schedule(dependsOn);
            }
            else
            {
                var job = new BatchMatrixMultiplyJob();
                job.data = data;
                jobFence = job.Schedule(dependsOn);
            }

            pinO.fence = pinX.reuse = pinY.reuse = jobFence;
        }

        /// <inheritdoc/>
        public void Dense(Tensor<float> X, Tensor<float> W, Tensor<float> B, Tensor<float> O, Layers.FusableActivation fusedActivation)
        {
            var Otmp = (fusedActivation != Layers.FusableActivation.None) ? AllocTensorFloat(O.shape) : O;

            var pinB = Pin(B);
            var pinO = Pin(Otmp);

            { // O = broadcast(B)
                // @TODO: move broadcast B directly into MatrixMultiplyJob
                var job = new VectorBroadcast1DJob();
                job.length = B.shape[-1];
                job.repeat = Otmp.shape.length / B.shape[-1];
                job.ScheduleXO(pinB, pinO);
            }

            ScheduleSGEMM(
                X, X.shape.Length(0, -1), X.shape[-1],
                W, W.shape[0], W.shape[1],
                Otmp, O.shape.Length(0, -1), O.shape[-1], accumulateC: true);

            if (fusedActivation != Layers.FusableActivation.None)
            {
                ApplyFusedActivation(Otmp, O, fusedActivation);
                ReleaseTensorFloat(Otmp);
            }
        }

        /// <inheritdoc/>
        public void DenseBatched(Tensor<float> X, Tensor<float> W, Tensor<float> B, Tensor<float> O, Layers.FusableActivation fusedActivation)
        {
            // TODO: optimize, move add and relu into sgemm
            var Otmp = AllocTensorFloat(O.shape);
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
            ReleaseTensorFloat(Otmp);
        }

        void ApplyFusedActivation(Tensor<float> X, Tensor<float> O, Layers.FusableActivation fusedActivation)
        {
            switch (fusedActivation)
            {
                case Layers.FusableActivation.None:
                    return;
                case Layers.FusableActivation.Relu:
                    Relu(X, O);
                    return;
                default:
                    throw new NotImplementedException();
            }
        }

        /// <inheritdoc/>
        public void Conv(Tensor<float> X, Tensor<float> K, Tensor<float> B, Tensor<float> O, int groups, Span<int> strides, Span<int> pads, Span<int> dilations, Layers.FusableActivation fusedActivation)
        {
            var job = new ConvJob();
            int arrayLength = job.Prepare(X.shape, K.shape, O.shape, groups, strides, pads, dilations, fusedActivation);
            if (B != null)
            {
                job.useBias = true;
                job.ScheduleXSBO(Pin(X), Pin(K), Pin(B), Pin(O), arrayLength, 1);
            }
            else
            {
                job.useBias = false;
                var pinX = Pin(X);
                var pinW = Pin(K);
                var pinO = Pin(O);
                var fenceBeforeJobStart = JobHandle.CombineDependencies(pinX.fence, pinW.fence, pinO.reuse);
                unsafe
                {
                    job.X = new ReadOnlyMemResource { ptr = pinX.rawPtr };
                    job.S = new ReadOnlyMemResource { ptr = pinW.rawPtr };
                    job.O = new ReadWriteMemResource { ptr = pinO.rawPtr };
                }
                var jobFence = job.Schedule(arrayLength, 1, fenceBeforeJobStart);
                pinX.reuse = jobFence;
                pinW.reuse = jobFence;
                pinO.fence = jobFence;
            }
        }

        /// <inheritdoc/>
        public void ConvTranspose(Tensor<float> X, Tensor<float> W, Tensor<float> B, Tensor<float> O, int groups, Span<int> strides, Span<int> pads, Span<int> dilations, Span<int> outputPadding, Layers.FusableActivation fusedActivation)
        {
            if (X.shape.rank >= 6)
                throw new NotImplementedException();

            var Otmp = (fusedActivation != Layers.FusableActivation.None) ? AllocTensorFloat(O.shape) : O;

            var batchCount = X.shape[0];
            var numInputChannelsPerGroup = X.shape[1] / groups;
            var numOutputChannelsPerGroup = O.shape[1] / groups;

            bool singleInnerChannel = (numInputChannelsPerGroup == 1);
            if (singleInnerChannel)
            {
                var job = new ConvTransposeJobNoInnerProduct();
                ScheduleConvTranspose(ref job, strides, pads, dilations);
            }
            else
            {
                var job = new ConvTransposeJob();
                ScheduleConvTranspose(ref job, strides, pads, dilations);
            }

            // We use this local generic trampoline to do static polymorphism on the job ie without the interface causing boxing:
            void ScheduleConvTranspose<T>(ref T job, Span<int> strides, Span<int> pads, Span<int> dilations) where T : struct, IConvTransposeJobCommon
            {
                job.Prepare(X.shape, W.shape, O.shape, groups, numOutputChannelsPerGroup, strides, pads, dilations);

                var columnBufferOrX = singleInnerChannel ? X : AllocTensorFloat(new TensorShape(groups * numOutputChannelsPerGroup * job.kernelSpatialSize, job.inputSpatialSize));
                var pinCBorX = Pin(columnBufferOrX);

                for (var batchIndex = 0; batchIndex < batchCount; batchIndex++)
                {
                    if (!singleInnerChannel)
                    {
                        for (int groupId = 0; groupId < groups; groupId++)
                        {
                            var offsetKernelW = groupId * numInputChannelsPerGroup * numOutputChannelsPerGroup * job.kernelSpatialSize;
                            var offsetInputX = (batchIndex * groups + groupId) * numInputChannelsPerGroup * job.inputSpatialSize;
                            var offsetO = groupId * numOutputChannelsPerGroup * job.kernelSpatialSize * job.inputSpatialSize;
                            ScheduleSGEMM(
                                X: W, numInputChannelsPerGroup, numOutputChannelsPerGroup * job.kernelSpatialSize,
                                Y: X, numInputChannelsPerGroup, job.inputSpatialSize,
                                O: columnBufferOrX, numOutputChannelsPerGroup * job.kernelSpatialSize, job.inputSpatialSize,
                                transposeA: true, offsetX: offsetKernelW, offsetY: offsetInputX, offsetO: offsetO);
                        }
                    }

                    job.offsetO = batchIndex * groups * numOutputChannelsPerGroup * job.outputSpatialSize;
                    job.offsetX = singleInnerChannel ? batchIndex * groups * numInputChannelsPerGroup * job.inputSpatialSize : 0;
                    if (B != null)
                    {
                        job.useBias = true;
                        // Note: W is only used in the case where singleInnerChannel is used: in that case, pinCBorX is X
                        job.ScheduleXSBO(pinCBorX, Pin(W), Pin(B), Pin(Otmp), numOutputChannelsPerGroup * groups, 1);
                    }
                    else
                    {
                        job.useBias = false;
                        var pinO = Pin(Otmp);
                        var pinW = singleInnerChannel ? Pin(W) : null;
                        var fenceBeforeJobStart = singleInnerChannel ? JobHandle.CombineDependencies(pinCBorX.fence, pinW.fence, pinO.reuse) : JobHandle.CombineDependencies(pinCBorX.fence, pinO.reuse);
                        unsafe
                        {
                            job.X = new ReadOnlyMemResource { ptr = pinCBorX.rawPtr };
                            job.S = new ReadOnlyMemResource { ptr = (pinW != null) ? pinW.rawPtr : null }; // note: .? doesn't work when RHS is void*
                            job.O = new ReadWriteMemResource { ptr = pinO.rawPtr };
                        }
                        // TODO innerloop adapted to number of threads and/or handle manually a workItemNum size
                        // per index scheduled.
                        var jobFence = job.Schedule(numOutputChannelsPerGroup * groups, 1, fenceBeforeJobStart);
                        pinCBorX.reuse = jobFence;
                        if (singleInnerChannel)
                            pinW.reuse = jobFence;
                        pinO.fence = jobFence;
                    }
                }

                if (fusedActivation != Layers.FusableActivation.None)
                {
                    ApplyFusedActivation(Otmp, O, fusedActivation);
                    ReleaseTensorFloat(Otmp);
                }

                if (!singleInnerChannel)
                    ReleaseTensorFloat(columnBufferOrX);
            }
        }

        void ResizeNDPerAxis(Tensor<float> X, Tensor<float> O, ReadOnlySpan<float> scale, Layers.InterpolationMode interpolationMode, Layers.NearestMode nearestMode, Layers.CoordTransformMode coordTransformMode)
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

            var job = new Resize1DJob();
            for (var axis = firstScaleAxis; axis <= lastScaleAxis; axis++)
            {
                if (scale[axis] == 1f)
                    continue;
                var oCur = axis == lastScaleAxis ? O : AllocTensorFloat(ShapeInference.Resize(X.shape, axis, scale[axis]));
                {
                    OpsUtils.GetScaleAndBias(X.shape[axis], oCur.shape[axis], scale[axis], coordTransformMode, interpolationMode, nearestMode, out float outputScale, out float outputBias);
                    if (interpolationMode == Layers.InterpolationMode.Nearest)
                    {
                        switch (nearestMode)
                        {
                            case Layers.NearestMode.RoundPreferFloor:
                            case Layers.NearestMode.Ceil:
                                job.mode = Resize1DJob.Mode.NearestCeil;
                                break;
                            case Layers.NearestMode.RoundPreferCeil:
                            case Layers.NearestMode.Floor:
                                job.mode = Resize1DJob.Mode.NearestFloor;
                                break;
                            default:
                                throw new NotImplementedException();
                        }
                    }
                    else
                    {
                        job.mode = Resize1DJob.Mode.Linear;
                    }

                    var pinX = Pin(X);
                    var pinOcur = Pin(oCur);

                    job.inputWidth = X.shape[axis];
                    job.outputWidth = oCur.shape[axis];
                    job.scale = outputScale;
                    job.bias = outputBias;

                    job.innerLength = oCur.shape.Strides(axis);
                    job.outerLength = oCur.shape.Length(0, axis);

                    job.ScheduleBatchXO(pinX, pinOcur, job.outerLength * job.innerLength * oCur.shape[axis], 128);
                }
                if (axis != firstScaleAxis)
                    ReleaseTensorFloat(X);
                X = oCur;
            }
        }

        void ResizeInnerMost1D2D3D(Tensor<float> X, Tensor<float> O, ReadOnlySpan<float> scale, Layers.InterpolationMode interpolationMode, Layers.NearestMode nearestMode, Layers.CoordTransformMode coordTransformMode)
        {
            int rankX = X.shape.rank;

            var pinX = Pin(X);
            var pinO = Pin(O);

            // for 1D case insert dim 1 at axis 2
            var xShape = rankX == 3 ? X.shape.Unsqueeze(2) : X.shape;
            var oShape = rankX == 3 ? O.shape.Unsqueeze(2) : O.shape;
            var scales = scale.Slice(rankX == 3 ? 1 : 2);

            // Fast path for 2D nearest mode resize where the scales are integers.
            if (rankX == 4 && interpolationMode == Layers.InterpolationMode.Nearest &&
                scales[0] == math.floor(scales[0]) && scales[1] == math.floor(scales[1]) &&
                ((coordTransformMode == Layers.CoordTransformMode.Asymmetric && nearestMode == Layers.NearestMode.Floor) ||
                    coordTransformMode != Layers.CoordTransformMode.Asymmetric && (nearestMode == Layers.NearestMode.RoundPreferFloor || nearestMode == Layers.NearestMode.RoundPreferCeil)))
            {
                var job = new UpsampleNearest2DJob();
                job.inputHeight = xShape[2];
                job.inputWidth = xShape[3];
                job.scaleHeight = (int)scales[0];
                job.scaleWidth = (int)scales[1];

                job.ScheduleBatchXO(pinX, pinO, xShape.length, 32);

                return;
            }

            FencedMemoryAlloc tables = s_tmpMemBlock0;
            int spatialDims = xShape.rank - 2;

            // Compute the number of output dimension indices for the helper tables constructed below.
            int tableElementCount = 0;
            for (int i = 2; i < xShape.rank; i++)
                tableElementCount += oShape[i];

            if (interpolationMode == Layers.InterpolationMode.Linear)
            {
                // For each output dimension index, cache the lower and upper input indices and the
                // fractional distribution for the linear interpolation.
                tables.Allocate(tableElementCount * 3, JobsUtility.CacheLineSize, Allocator.TempJob);

                var initTablesJob = new ResizeLinearInitTablesJob();
                var resizeJob = new ResizeLinearJob();

                unsafe
                {
                    initTablesJob.spatialDims = spatialDims;
                    resizeJob.spatialDims = spatialDims;
                    resizeJob.inputSize = 1;

                    for (int i = 0; i < spatialDims; i++)
                    {
                        initTablesJob.inputShape[i] = xShape[2 + i];
                        initTablesJob.outputShape[i] = oShape[2 + i];
                        resizeJob.inputSize *= xShape[2 + i];
                        resizeJob.outputShape[i] = oShape[2 + i];

                        OpsUtils.GetScaleAndBias(xShape[2 + i], oShape[2 + i], scales[i], coordTransformMode, interpolationMode, nearestMode, out float outputScale, out float outputBias);
                        initTablesJob.scales[i] = outputScale;
                        initTablesJob.biases[i] = outputBias;
                    }
                }

                initTablesJob.ScheduleO(tables);
                resizeJob.ScheduleBatchXBO(pinX, tables, pinO, O.shape.length, 32);
            }
            else
            {
                // For each output dimension index, cache the nearest input index.
                tables.Allocate(tableElementCount * 1, JobsUtility.CacheLineSize, Allocator.TempJob);

                var initTablesJob = new ResizeNearestInitTablesJob();
                var resizeJob = new ResizeNearestJob();

                unsafe
                {
                    initTablesJob.spatialDims = spatialDims;
                    if (nearestMode == Layers.NearestMode.RoundPreferCeil)
                        initTablesJob.nearestMode = Layers.NearestMode.Floor;
                    else if (nearestMode == Layers.NearestMode.RoundPreferFloor)
                        initTablesJob.nearestMode = Layers.NearestMode.Ceil;
                    else
                        initTablesJob.nearestMode = nearestMode;
                    resizeJob.spatialDims = spatialDims;
                    resizeJob.inputSize = 1;

                    for (int i = 0; i < spatialDims; i++)
                    {
                        initTablesJob.inputShape[i] = xShape[2 + i];
                        initTablesJob.outputShape[i] = oShape[2 + i];
                        resizeJob.inputSize *= xShape[2 + i];
                        resizeJob.outputShape[i] = oShape[2 + i];

                        OpsUtils.GetScaleAndBias(xShape[2 + i], oShape[2 + i], scales[i], coordTransformMode, interpolationMode, nearestMode, out float outputScale, out float outputBias);
                        initTablesJob.scales[i] = outputScale;
                        initTablesJob.biases[i] = outputBias;
                    }
                }

                initTablesJob.ScheduleO(tables);
                resizeJob.ScheduleBatchXBO(pinX, tables, pinO, O.shape.length, 32);
            }

            unsafe
            {
                var job = new MemFreeJob();
                job.allocator = Allocator.TempJob;
                job.buffer0 = tables.rawPtr;
                job.Schedule(pinO.fence);
            }

            tables.ClearState();
        }

        /// <inheritdoc/>
        public void Resize(Tensor<float> X, Tensor<float> O, ReadOnlySpan<float> scale, Layers.InterpolationMode interpolationMode, Layers.NearestMode nearestMode, Layers.CoordTransformMode coordTransformMode)
        {
            int rankX = X.shape.rank;

            // Handle only the common cases of NCHW or NCDHW resizes where NC is not scaled and delegate
            // the uncommon cases to a slower per axis implementation.
            if (rankX < 3 || rankX > 5 || scale[0] != 1.0f || scale[1] != 1.0f)
            {
                ResizeNDPerAxis(X, O, scale, interpolationMode, nearestMode, coordTransformMode);
                return;
            }

            ResizeInnerMost1D2D3D(X, O, scale, interpolationMode, nearestMode, coordTransformMode);
        }

        /// <inheritdoc/>
        public void GridSample(Tensor<float> X, Tensor<float> grid, Tensor<float> O, Layers.InterpolationMode mode, Layers.PaddingMode paddingMode, bool alignCorners)
        {
            int n = O.shape[0]; int c = O.shape[1];
            int oH = O.shape[-2]; int oW = O.shape[-1];
            int xH = X.shape[-2]; int xW = X.shape[-1];
            int oSpatialDim = oH * oW;
            int xSpatialDim = xH * xW;

            var spatialDims = O.shape.rank - 2;
            switch (spatialDims)
            {
                case 2:
                {
                    var job = new GridSample2DJob
                    {
                        mode = mode, paddingMode = paddingMode, alignCorners = alignCorners,
                        inHeight = xH, inWidth = xW,
                        inSpatialSize = xSpatialDim,
                        outBatch = n, outChannels = c,
                        outSpatialSize = oSpatialDim
                    };
                    job.ScheduleBatchXBO(Pin(X), Pin(grid), Pin(O), O.shape.length, 32);
                    break;
                }
                case 3:
                {
                    int oD = O.shape[2];
                    int xD = X.shape[2];
                    oSpatialDim *= oD;
                    xSpatialDim *= xD;
                    var job = new GridSample3DJob
                    {
                        mode = mode, paddingMode = paddingMode, alignCorners = alignCorners,
                        inDepth = xD, inHeight = xH, inWidth = xW,
                        inSpatialSize = xSpatialDim,
                        outBatch = n, outChannels = c,
                        outSpatialSize = oSpatialDim
                    };
                    job.ScheduleBatchXBO(Pin(X), Pin(grid), Pin(O), O.shape.length, 32);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(spatialDims));
            }
        }

        static readonly int[] permutationsDepthToSpaceDCR = new int[] { 0, 3, 4, 1, 5, 2 };
        static readonly int[] permutationsDepthToSpaceCRD = new int[] { 0, 1, 4, 2, 5, 3 };
        static readonly int[] permutationsSpaceToDepth = new int[] { 0, 3, 5, 1, 2, 4 };

        /// <inheritdoc/>
        public void DepthToSpace(Tensor X, Tensor O, int blocksize, Layers.DepthToSpaceMode mode)
        {
            int[] permutations;
            int dim1, dim3;

            if (mode == Layers.DepthToSpaceMode.DepthColumnRow)
            {
                permutations = permutationsDepthToSpaceDCR;
                dim1 = blocksize;
                dim3 = O.shape[1];
            }
            else
            {
                permutations = permutationsDepthToSpaceCRD;
                dim1 = O.shape[1];
                dim3 = blocksize;
            }

            var reshape = new TensorShape(X.shape[0], dim1, blocksize, dim3, X.shape[2], X.shape[3]);

            var job = new TransposeJob();
            job.iteratorX.Prepare(reshape, permutations);
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void SpaceToDepth(Tensor X, Tensor O, int blocksize)
        {
            var reshape = new TensorShape(X.shape[0], X.shape[1], X.shape[2] / blocksize, blocksize, X.shape[3] / blocksize, blocksize);

            var job = new TransposeJob();
            job.iteratorX.Prepare(reshape, permutationsSpaceToDepth);
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void MaxPool(Tensor<float> X, Tensor<float> O, int[] kernelShape, int[] strides, int[] pads)
        {
            if (X.shape.rank == 5)
            {
                var job = new MaxPool3DJob();
                job.inputDepth = X.shape[2];
                job.inputHeight = X.shape[3];
                job.inputWidth = X.shape[4];
                job.outputDepth = O.shape[2];
                job.outputHeight = O.shape[3];
                job.outputWidth = O.shape[4];
                job.poolDepth = kernelShape[0];
                job.poolHeight = kernelShape[1];
                job.poolWidth = kernelShape[2];
                job.strideDepth = strides[0];
                job.strideHeight = strides[1];
                job.strideWidth = strides[2];
                job.padDepth = pads[0];
                job.padHeight = pads[1];
                job.padWidth = pads[2];
                job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
            }
            else if (X.shape.rank == 4)
            {
                var job = new MaxPool2DJob();
                job.inputHeight = X.shape[2];
                job.inputWidth = X.shape[3];
                job.outputHeight = O.shape[2];
                job.outputWidth = O.shape[3];
                job.poolHeight = kernelShape[0];
                job.poolWidth = kernelShape[1];
                job.strideHeight = strides[0];
                job.strideWidth = strides[1];
                job.padHeight = pads[0];
                job.padWidth = pads[1];
                job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
            }
            else
            {
                var job = new MaxPool2DJob();
                job.inputHeight = 1;
                job.inputWidth = X.shape[2];
                job.outputHeight = 1;
                job.outputWidth = O.shape[2];
                job.poolHeight = 1;
                job.poolWidth = kernelShape[0];
                job.strideHeight = 1;
                job.strideWidth = strides[0];
                job.padHeight = 0;
                job.padWidth = pads[0];
                job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
            }
        }

        /// <inheritdoc/>
        public void AveragePool(Tensor<float> X, Tensor<float> O, int[] kernelShape, int[] strides, int[] pads)
        {
            if (X.shape.rank == 5)
            {
                var job = new AveragePool3DJob();
                job.inputDepth = X.shape[2];
                job.inputHeight = X.shape[3];
                job.inputWidth = X.shape[4];
                job.outputDepth = O.shape[2];
                job.outputHeight = O.shape[3];
                job.outputWidth = O.shape[4];
                job.poolDepth = kernelShape[0];
                job.poolHeight = kernelShape[1];
                job.poolWidth = kernelShape[2];
                job.strideDepth = strides[0];
                job.strideHeight = strides[1];
                job.strideWidth = strides[2];
                job.padDepth = pads[0];
                job.padHeight = pads[1];
                job.padWidth = pads[2];
                job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
            }
            else if (X.shape.rank == 4)
            {
                var job = new AveragePool2DJob();
                job.inputHeight = X.shape[2];
                job.inputWidth = X.shape[3];
                job.outputHeight = O.shape[2];
                job.outputWidth = O.shape[3];
                job.poolHeight = kernelShape[0];
                job.poolWidth = kernelShape[1];
                job.strideHeight = strides[0];
                job.strideWidth = strides[1];
                job.padHeight = pads[0];
                job.padWidth = pads[1];
                job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
            }
            else
            {
                var job = new AveragePool2DJob();
                job.inputHeight = 1;
                job.inputWidth = X.shape[2];
                job.outputHeight = 1;
                job.outputWidth = O.shape[2];
                job.poolHeight = 1;
                job.poolWidth = kernelShape[0];
                job.strideHeight = 1;
                job.strideWidth = strides[0];
                job.padHeight = 0;
                job.padWidth = pads[0];
                job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
            }
        }

        /// <inheritdoc/>
        public void GlobalMaxPool(Tensor<float> X, Tensor<float> O)
        {
            var job = new ReduceMaxFloatJob();
            job.innerLength = 1;
            job.reduceLength = X.shape.Strides(1);
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void GlobalAveragePool(Tensor<float> X, Tensor<float> O)
        {
            int strideX = X.shape.Strides(1);

            var job = new ReduceMeanFloatJob();
            job.innerLength = 1;
            job.reduceLength = strideX;
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <summary>
        /// Calculates an output tensor by pooling the mean and variance values of the input tensor across the spatial dimensions from a given axis. The spatial dimensions of the output are size 1.
        /// </summary>
        /// <param name="X">The input tensor.</param>
        /// <param name="O">The output tensor to be computed and filled.</param>
        /// <param name="axis">The axis from which to pool.</param>
        public void GlobalAverageVariancePool(Tensor<float> X, Tensor<float> O, int axis)
        {
            if (O.shape.HasZeroDims())
                return;

            var job = new GlobalAverageVariancePoolJob();
            job.spatialDims = X.shape.Length(axis);

            job.ScheduleXO(Pin(X), Pin(O), O.shape.length / 2, 32);
        }

        /// <inheritdoc/>
        public void InstanceNormalization(Tensor<float> X, Tensor<float> S, Tensor<float> B, Tensor<float> O, float epsilon)
        {
            var pinX = Pin(X);
            var pinS = Pin(S);
            var pinB = Pin(B);
            var pinO = Pin(O);

            // Allocate memory
            var reduceOpShape = ShapeInference.GlobalAverageVariancePool(X.shape);
            var meanVariance = AllocTensorFloat(reduceOpShape);

            GlobalAverageVariancePool(X, meanVariance, 2);

            var job = new InstanceNormalizationTailJob();
            job.channels = X.shape[1];
            job.spatialDims = X.shape.length / (X.shape[0] * X.shape[1]);
            job.epsilon = epsilon;

            job.ScheduleXSBWO(pinX, pinS, pinB, Pin(meanVariance), pinO, O.shape.length, 32);
            ReleaseTensorFloat(meanVariance);
        }

        /// <inheritdoc/>
        public void LayerNormalization(Tensor<float> X, Tensor<float> S, Tensor<float> B, Tensor<float> O, float epsilon)
        {
            int axis = X.shape.Axis(-1);

            var reducedShape = X.shape.Reduce(axis);
            reducedShape[axis] = 2;

            int axisDim = X.shape[axis];
            int outerLength = X.shape.Length(0, -1);

            var meanVariance = AllocTensorFloat(reducedShape);
            GlobalAverageVariancePool(X, meanVariance, -1);

            var job = new LayerNormalizationTailJob();
            job.axisDim = axisDim;
            job.outerLength = outerLength;
            job.epsilon = epsilon;

            job.ScheduleXSBWO(Pin(X), Pin(S), Pin(B), Pin(meanVariance), Pin(O), outerLength, 32);
            ReleaseTensorFloat(meanVariance);
        }

        /// <inheritdoc/>
        public void RMSNormalization(Tensor<float> X, Tensor<float> S, Tensor<float> O, float epsilon)
        {
            int axis = X.shape.Axis(-1);

            var reducedShape = X.shape.Reduce(axis);
            int axisDim = X.shape[axis];
            int outerLength = X.shape.Length(0, -1);
            Span<int> axes = stackalloc int[1];
            axes[0] = -1;

            var meanSquared = AllocTensorFloat(reducedShape);
            ReduceMeanSquare(X, meanSquared, axes);

            var jobTail = new RMSNormalizationTailJob();
            jobTail.axisDim = axisDim;
            jobTail.outerLength = outerLength;
            jobTail.epsilon = epsilon;
            jobTail.ScheduleXSBO(Pin(X), Pin(S), Pin(meanSquared), Pin(O), outerLength, 32);
            ReleaseTensorFloat(meanSquared);
        }

        /// <inheritdoc/>
        public void ScaleBias(Tensor<float> X, Tensor<float> S, Tensor<float> B, Tensor<float> O)
        {
            var job = new ScaleBiasJob();
            job.channels = X.shape[1];
            job.spatialLength = X.shape.Length(2);

            job.ScheduleBatchXSBO(Pin(X), Pin(S), Pin(B), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void BatchNormalization(Tensor<float> X, Tensor<float> S, Tensor<float> B, Tensor<float> mean, Tensor<float> variance, Tensor<float> O, float epsilon)
        {
            var pinX = Pin(X);
            var pinS = Pin(S);
            var pinB = Pin(B);
            var pinM = Pin(mean);
            var pinV = Pin(variance);
            var pinO = Pin(O);

            var job = new BatchNormalizationJob();
            job.channels = X.shape.rank == 1 ? 1 : X.shape[1];
            job.spatialLength = X.shape.Length(2);
            job.epsilon = epsilon;
            unsafe
            {
                job.Xptr = (float*)pinX.rawPtr;
                job.Sptr = (float*)pinS.rawPtr;
                job.Bptr = (float*)pinB.rawPtr;
                job.Mptr = (float*)pinM.rawPtr;
                job.Vptr = (float*)pinV.rawPtr;
                job.Optr = (float*)pinO.rawPtr;
            }

            pinO.fence = pinX.reuse = pinS.reuse = pinB.reuse = pinM.reuse = pinV.reuse =
                job.ScheduleBatch(O.shape.length, 1024, JobHandle.CombineDependencies(pinO.reuse, pinX.fence, JobHandle.CombineDependencies(pinS.fence, pinB.fence, JobHandle.CombineDependencies(pinM.fence, pinV.fence))));
        }

        /// <inheritdoc/>
        public void LocalResponseNormalization(Tensor<float> X, Tensor<float> O, int supportLength, float bias, float alpha, float beta)
        {
            int channelStride = X.shape.Length(2);
            int numChannels = X.shape[1];
            int batchStride = numChannels * channelStride;

            float supportHalfSideSize = (supportLength - 1.0f) / 2.0f;
            int leftSupportLength = (int)Mathf.Floor(supportHalfSideSize);
            int rightSupportLength = (int)Mathf.Ceil(supportHalfSideSize);

            var job = new LocalResponseNormalizationJob();

            job.numChannels = numChannels;
            job.channelsStride = channelStride;
            job.batchStride = batchStride;
            job.leftSupportLength = leftSupportLength;
            job.rightSupportLength = rightSupportLength;
            job.alphaDivSupportLength = alpha / (float)(supportLength);

            job.bias = bias;
            job.beta = beta;

            var pinX = Pin(X);
            var pinO = Pin(O);

            unsafe
            {
                job.Xptr = (float*)pinX.rawPtr;
                job.Optr = (float*)pinO.rawPtr;
            }

            pinO.fence = pinX.reuse = job.ScheduleBatch(O.shape.length, Mathf.Clamp(numChannels, 512, 1024), JobHandle.CombineDependencies(pinO.reuse, pinX.fence));
        }

        /// <inheritdoc/>
        public void Cast(Tensor<float> X, Tensor<int> O)
        {
            var job = new CastFloatToIntJob();
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void Cast(Tensor<int> X, Tensor<float> O)
        {
            var job = new CastIntToFloatJob();
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void Cast(Tensor<short> X, Tensor<float> O)
        {
            var job = new CastHalfToFloatJob();
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void DequantizeLinear(Tensor<byte> X, Tensor<float> O, float scale, byte zeroPoint)
        {
            var job = new DequantizeUint8Job();
            job.scale = scale;
            job.zeroPoint = zeroPoint;
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void IsInf(Tensor<float> X, Tensor<int> O, bool detectNegative, bool detectPositive)
        {
            var job = new IsInfJob();
            job.detectNegative = detectNegative;
            job.detectPositive = detectPositive;
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void PRelu(Tensor<float> X, Tensor<float> S, Tensor<float> O)
        {
            var job = new PReluJob();
            var outputLength = job.broadcast.Prepare(X.shape, S.shape);
            job.ScheduleBatchXBO(Pin(X), Pin(S), Pin(O), outputLength, 32);
        }

        /// <inheritdoc/>
        public void LeakyRelu(Tensor<float> X, Tensor<float> O, float alpha)
        {
            var job = new LeakyReluJob();
            job.weight = 0.5f * (1f + alpha);
            job.absWeight = 0.5f * (1f - alpha);
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void HardSigmoid(Tensor<float> X, Tensor<float> O, float alpha, float beta)
        {
            var job = new HardSigmoidJob();
            job.alpha = alpha;
            job.beta = beta;
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void Elu(Tensor<float> X, Tensor<float> O, float alpha)
        {
            var job = new EluJob();
            job.alpha = alpha;
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void Gelu(Tensor<float> X, Tensor<float> O)
        {
            var job = new GeluJob();
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void GeluFast(Tensor<float> X, Tensor<float> O)
        {
            var job = new GeluFastJob();
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void Selu(Tensor<float> X, Tensor<float> O, float alpha, float gamma)
        {
            var job = new SeluJob();
            job.alpha = alpha;
            job.gamma = gamma;
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void HardTanh(Tensor<float> X, Tensor<float> O, float minVal, float maxVal)
        {
            var job = new HardTanhJob();
            job.minVal = minVal;
            job.maxVal = maxVal;
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void Clip(Tensor<float> X, Tensor<float> min, Tensor<float> max, Tensor<float> O)
        {
            if (min != null)
            {
                if (max != null)
                {
                    var job = new ClipMinMaxFloatJob();
                    job.ScheduleBatchXSBO(Pin(X), Pin(min), Pin(max), Pin(O), O.shape.length, 32);
                }
                else
                {
                    var job = new ClipMinFloatJob();
                    job.ScheduleBatchXBO(Pin(X), Pin(min), Pin(O), O.shape.length, 32);
                }
            }
            else
            {
                if (max != null)
                {
                    var job = new ClipMaxFloatJob();
                    job.ScheduleBatchXBO(Pin(X), Pin(max), Pin(O), O.shape.length, 32);
                }
                else
                {
                    MemCopy(X, O);
                }
            }
        }

        /// <inheritdoc/>
        public void Clip(Tensor<int> X, Tensor<int> min, Tensor<int> max, Tensor<int> O)
        {
            if (min != null)
            {
                if (max != null)
                {
                    var job = new ClipMinMaxIntJob();
                    job.ScheduleBatchXSBO(Pin(X), Pin(min), Pin(max), Pin(O), O.shape.length, 32);
                }
                else
                {
                    var job = new ClipMinIntJob();
                    job.ScheduleBatchXBO(Pin(X), Pin(min), Pin(O), O.shape.length, 32);
                }
            }
            else
            {
                if (max != null)
                {
                    var job = new ClipMaxIntJob();
                    job.ScheduleBatchXBO(Pin(X), Pin(max), Pin(O), O.shape.length, 32);
                }
                else
                {
                    MemCopy(X, O);
                }
            }
        }

        /// <inheritdoc/>
        public void Celu(Tensor<float> X, Tensor<float> O, float alpha)
        {
            var job = new CeluJob();
            job.alpha = alpha;
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void Swish(Tensor<float> X, Tensor<float> O, float alpha)
        {
            var job = new SwishJob();
            job.alpha = alpha;
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void Shrink(Tensor<float> X, Tensor<float> O, float bias, float lambd)
        {
            var job = new ShrinkJob();
            job.bias = bias;
            job.lambd = lambd;
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void ThresholdedRelu(Tensor<float> X, Tensor<float> O, float alpha)
        {
            var job = new ThresholdedReluJob();
            job.alpha = alpha;
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void Einsum(Tensor<float>[] inputTensors, Tensor<float> O, TensorIndex[] operandIndices, TensorIndex outputIndices, TensorIndex sumIndices, TensorShape sumShape)
        {
            switch (inputTensors.Length)
            {
                case 1:
                {
                    var job = new EinsumOneJob();

                    unsafe
                    {
                        EinsumHelper.PinOperandStrides(inputTensors[0].shape, operandIndices[0], outputIndices, sumIndices, job.outStridesA, job.sumStridesA);
                        OpsUtils.PinTensorShapeStrides(O.shape, job.outLengths, job.outStrides);
                        OpsUtils.PinTensorShapeStrides(sumShape, job.sumLengths, job.sumStrides);
                    }

                    job.sumSize = sumShape.length;
                    job.sumRank = sumShape.rank;
                    job.outRank = O.shape.rank;

                    job.ScheduleXO(Pin(inputTensors[0]), Pin(O), O.shape.length, 32);
                    return;
                }
                case 2:
                {
                    var job = new EinsumTwoJob();

                    unsafe
                    {
                        EinsumHelper.PinOperandStrides(inputTensors[0].shape, operandIndices[0], outputIndices, sumIndices, job.outStridesA, job.sumStridesA);
                        EinsumHelper.PinOperandStrides(inputTensors[1].shape, operandIndices[1], outputIndices, sumIndices, job.outStridesB, job.sumStridesB);
                        OpsUtils.PinTensorShapeStrides(O.shape, job.outLengths, job.outStrides);
                        OpsUtils.PinTensorShapeStrides(sumShape, job.sumLengths, job.sumStrides);
                    }

                    job.sumSize = sumShape.length;
                    job.sumRank = sumShape.rank;
                    job.outRank = O.shape.rank;

                    job.ScheduleXBO(Pin(inputTensors[0]), Pin(inputTensors[1]), Pin(O), O.shape.length, 32);
                    return;
                }
            }
        }

        /// <summary>
        /// Performs `NonMaxSuppression` on boxes with scores.
        /// </summary>
        /// <param name="boxes">The boxes input tensor containing the position and dimensions of the boxes for each batch.</param>
        /// <param name="scores">The scores input tensor containing the score for each box per class per batch.</param>
        /// <param name="O">The output tensor to be computed and filled (and truncated) with the batch, class and index of the boxes in decreasing order of score.</param>
        /// <param name="maxOutputBoxesPerClass">The maximum number of output boxes per class.</param>
        /// <param name="iouThreshold">Boxes with intersect-over-union with a selected box above this threshold are discarded.</param>
        /// <param name="scoreThreshold">Boxes with a score below this threshold are discarded.</param>
        /// <param name="centerPointBox">The types of the box coordinates, either [x1, y1, x2, y2] or [x, y, w, h].</param>
        public void NonMaxSuppression(Tensor<float> boxes, Tensor<float> scores, Tensor<int> O, int maxOutputBoxesPerClass, float iouThreshold, float scoreThreshold, Layers.CenterPointBox centerPointBox)
        {
            // based on https://github.com/pytorch/vision/blob/main/torchvision/csrc/ops/cpu/nms_kernel.cpp
            // extended to onnx multiple class multiple batch inputs as here
            // https://github.com/microsoft/onnxruntime/blob/main/onnxruntime/core/providers/cpu/object_detection/non_max_suppression.cc

            var numBatches = scores.shape[0];
            var numClasses = scores.shape[1];
            var numBoxes = scores.shape[2];

            Pin(boxes);
            Pin(scores);
            Pin(O);
            var maxNumOutput = O.shape[0];

            var selected = AllocTensorInt(new TensorShape(numBatches * numClasses * numBoxes));
            var orderAll = AllocTensorInt(new TensorShape(numBatches * numClasses * numBoxes));

            var bitmask = AllocTensorInt(new TensorShape(ComputeHelper.IDivC(numBatches * numBoxes * numBoxes, 4)));

            var jobBitMask = new NMSBitmaskJob
            {
                numBoxes = numBoxes,
                iouThreshold = iouThreshold,
                centerPointBox = centerPointBox
            };
            jobBitMask.ScheduleBatchXO(Pin(boxes), Pin(bitmask), numBatches * numBoxes * numBoxes, 32);

            var jobSortSelect = new NMSSortSelectJob
            {
                numBoxes = numBoxes,
                numClasses = numClasses,
                maxOutputBoxesPerClass = maxOutputBoxesPerClass,
                scoreThreshold = scoreThreshold
            };
            jobSortSelect.ScheduleXSBO(Pin(bitmask), Pin(scores), Pin(orderAll), Pin(selected), numBatches * numClasses, 32);

            // compaction
            int numOutput = 0;
            unsafe
            {
                var jobCompact = new NMSCompactJob
                {
                    numBatches = numBatches,
                    numBoxes = numBoxes,
                    numClasses = numClasses,
                    maxNumOutput = maxNumOutput,
                    maxOutputBoxesPerClass = maxOutputBoxesPerClass,
                    numOutputPtr = &numOutput
                };
                var fence = jobCompact.ScheduleXO(Pin(selected), Pin(O));
                fence.Complete();
            }

            ReleaseTensorInt(orderAll);
            ReleaseTensorInt(selected);
            ReleaseTensorInt(bitmask);

            O.Reshape(new TensorShape(numOutput, 3));
        }

        /// <inheritdoc/>
        public void SliceSet(Tensor X, Tensor O, int axis, int start, int step)
        {
            var strideX = X.shape.Length(axis);
            var strideO = O.shape.Length(axis) * step;
            var length = strideX;
            var count = X.shape.Length(0, axis);
            MemCopyStride(X, O, strideX, strideO, length, count, 0, O.shape.Strides(axis) * start);
        }

        /// <inheritdoc/>
        public void Slice(Tensor X, Tensor O, ReadOnlySpan<int> starts, ReadOnlySpan<int> axes, ReadOnlySpan<int> steps)
        {
            var job = new SliceJob();
            job.sliceParams.Prepare(X.shape, O.shape, starts, axes, steps);
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void SliceSet(Tensor X, Tensor values, Tensor O, ReadOnlySpan<int> starts, ReadOnlySpan<int> axes, ReadOnlySpan<int> steps)
        {
            MemCopy(X, O);
            var job = new SliceSetJob();
            job.sliceParams.Prepare(O.shape, values.shape, starts, axes, steps);
            job.ScheduleBatchXO(Pin(values), Pin(O), values.shape.length, 32);
        }

        /// <inheritdoc/>
        public void AsStrided(Tensor X, Tensor O, ReadOnlySpan<int> strides, int offset)
        {
            var job = new SliceJob();
            job.sliceParams.PrepareAsStrided(O.shape, strides, offset);
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void Split(Tensor X, Tensor O, int axis, int start)
        {
            var job = new SliceJob();
            job.sliceParams.Prepare(X.shape, O.shape, stackalloc int[] { start }, stackalloc int[] { axis }, stackalloc int[] { 1 });

            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void Pad(Tensor<float> X, Tensor<float> O, ReadOnlySpan<int> pad, Layers.PadMode padMode, float constant)
        {
            var job = new PadJob();
            job.padMode = padMode;
            job.constant = math.asint(constant);
            job.padParams.Prepare(X.shape, pad, padMode);
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void Pad(Tensor<int> X, Tensor<int> O, ReadOnlySpan<int> pad, Layers.PadMode padMode, int constant)
        {
            var job = new PadJob();
            job.padMode = padMode;
            job.constant = constant;
            job.padParams.Prepare(X.shape, pad, padMode);
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void Transpose(Tensor X, Tensor O)
        {
            var job = new TransposeJob();
            job.iteratorX.Prepare(X.shape, Array.Empty<int>());
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void Transpose(Tensor X, Tensor O, ReadOnlySpan<int> permutations)
        {
            var job = new TransposeJob();
            job.iteratorX.Prepare(X.shape, permutations);
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void Where(Tensor<int> C, Tensor A, Tensor B, Tensor O)
        {
            var job = new WhereJob();
            unsafe
            {
                OpsUtils.PinTensorShapeStrides(O.shape, job.shapeO, job.stridesO);
                OpsUtils.PinTensorShapeStrides(C.shape, job.shapeC, job.stridesC);
                OpsUtils.PinTensorShapeStrides(A.shape, job.shapeA, job.stridesA);
                OpsUtils.PinTensorShapeStrides(B.shape, job.shapeB, job.stridesB);
            }
            job.rank = O.shape.rank;

            job.ScheduleXSBO(Pin(C), Pin(A), Pin(B), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void Tile(Tensor X, Tensor O, ReadOnlySpan<int> repeats)
        {
            var job = new TileJob();
            unsafe
            {
                OpsUtils.PinTensorShapeStrides(O.shape, job.shapeO, job.stridesO);
                OpsUtils.PinTensorShapeStrides(X.shape, job.shapeX, job.stridesX);
            }
            job.rank = O.shape.rank;

            job.ScheduleXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void MemClear(Tensor O)
        {
            var job = new ClearJob();
            job.length = O.shape.length;
            job.ScheduleO(Pin(O));
        }

        /// <inheritdoc/>
        public void MemSet(Tensor<float> O, float value)
        {
            var job = new SetJob();
            job.memValue = math.asint(value);
            job.ScheduleBatchO(Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void MemSet(Tensor<int> O, int value)
        {
            var job = new SetJob();
            job.memValue = value;
            job.ScheduleBatchO(Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void Expand(Tensor X, Tensor O)
        {
            var job = new ExpandJob();
            unsafe
            {
                OpsUtils.PinTensorShapeStrides(O.shape, job.shapeO, job.stridesO);
                OpsUtils.PinTensorShapeStrides(X.shape, job.shapeX, job.stridesX);
            }
            job.rank = O.shape.rank;
            job.ScheduleXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void CompressWithIndices(Tensor X, Tensor<int> indices, Tensor O, int numIndices, int axis)
        {
            var job = new GatherJob();
            job.innerLength = X.shape.Strides(axis);
            job.indicesLength = numIndices;
            job.axisDim = X.shape[axis];

            job.ScheduleBatchXBO(Pin(X), Pin(indices), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void Gather(Tensor X, Tensor<int> indices, Tensor O, int axis)
        {
            var job = new GatherJob();
            job.innerLength = X.shape.Strides(axis);
            job.indicesLength = indices.shape.length;
            job.axisDim = X.shape[axis];
            job.ScheduleBatchXBO(Pin(X), Pin(indices), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void GatherElements(Tensor X, Tensor<int> indices, Tensor O, int axis)
        {
            // See ScatterElements and compute code for more info
            axis = X.shape.Axis(axis); // note: this is safe since the ranks all match

            bool fastPathPossible = ShapeInference.ScatterGatherElementsSupportsFastPath(indices.shape, X.shape, axis);
            if (fastPathPossible)
            {
                var job = new GatherElementsFastJob();
                job.inputAxisSize = X.shape[axis];
                job.indicesAxisElementStride = indices.shape.Strides(axis);
                job.inputAxisElementStride = X.shape.Strides(axis);
                job.indicesAxisMinusOneElementStride = indices.shape[axis] * indices.shape.Strides(axis);

                job.ScheduleXBO(Pin(X), Pin(indices), Pin(O), indices.shape.length, 32);
            }
            else
            {
                var job = new GatherElementsJob();
                job.inputAxisSize = X.shape[axis];

                unsafe
                {
                    OpsUtils.PinTensorStridesCompact(X.shape, job.stridesX);
                    OpsUtils.PinTensorStridesCompact(indices.shape, job.stridesO);
                }
                job.posAxis = axis;
                job.rank = X.shape.rank;

                job.ScheduleXBO(Pin(X), Pin(indices), Pin(O), indices.shape.length, 32);
            }
        }

        /// <inheritdoc/>
        public void GatherND(Tensor X, Tensor<int> indices, Tensor O, int batchDims)
        {
            var job = new GatherNDJob();
            job.rankX = X.shape.rank;
            job.rankO = O.shape.rank;
            job.rankIndices = indices.shape.rank;
            job.indexSize = indices.shape[-1];
            job.batchDims = batchDims;

            unsafe
            {
                OpsUtils.PinTensorShapeStrides(O.shape, job.shapeO, job.stridesO);
                OpsUtils.PinTensorShapeStrides(X.shape, job.shapeX, job.stridesX);
                OpsUtils.PinTensorShapeStrides(indices.shape, job.shapeIndices, job.stridesIndices);
            }

            job.ScheduleXBO(Pin(X), Pin(indices), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void ScatterElements(Tensor X, Tensor<int> indices, Tensor updates, Tensor O, int axis, Layers.ScatterReductionMode reduction)
        {
            MemCopy(X, O);
            axis = X.shape.Axis(axis); // note: this is safe since the ranks of X and indices match

            bool fastPathPossible = ShapeInference.ScatterGatherElementsSupportsFastPath(indices.shape, X.shape, axis);
            if (fastPathPossible)
            {
                if (X.dataType == DataType.Float)
                {
                    var job = new ScatterElementsFloatFastJob();
                    job.outAxisSize = X.shape[axis];
                    job.indicesAxisElementStride = indices.shape.Strides(axis);
                    job.outAxisElementStride = X.shape.Strides(axis);
                    job.indicesAxisMinusOneElementStride = indices.shape[axis] * indices.shape.Strides(axis);
                    job.reductionType = reduction;

                    // When reduction != ScatterReductionMode.None, the reduction is allowed to have duplicate output
                    // indices. To avoid race conditions updating the output tensor, force these reduction modes to run
                    // on a single worker by setting the inner loop length to int.MaxValue.

                    // TODO: please refactor this to a single worker job instead of relying on this hack?
                    job.ScheduleXBO(Pin(updates), Pin(indices), Pin(O), indices.shape.length,
                        (reduction == Layers.ScatterReductionMode.None) ? 32 : int.MaxValue);
                }
                else
                {
                    var job = new ScatterElementsIntFastJob();
                    job.outAxisSize = X.shape[axis];
                    job.indicesAxisElementStride = indices.shape.Strides(axis);
                    job.outAxisElementStride = X.shape.Strides(axis);
                    job.indicesAxisMinusOneElementStride = indices.shape[axis] * indices.shape.Strides(axis);
                    job.reductionType = reduction;

                    // When reduction != ScatterReductionMode.None, the reduction is allowed to have duplicate output
                    // indices. To avoid race conditions updating the output tensor, force these reduction modes to run
                    // on a single worker by setting the inner loop length to int.MaxValue.

                    // TODO: please refactor this to a single worker job instead of relying on this hack?
                    job.ScheduleXBO(Pin(updates), Pin(indices), Pin(O), indices.shape.length,
                        (reduction == Layers.ScatterReductionMode.None) ? 32 : int.MaxValue);
                }
            }
            else
            {
                if (X.dataType == DataType.Float)
                {
                    var job = new ScatterElementsFloatJob();
                    job.outAxisSize = X.shape[axis];
                    job.reductionType = reduction;

                    unsafe
                    {
                        OpsUtils.PinTensorStridesCompact(X.shape, job.stridesO);
                        OpsUtils.PinTensorStridesCompact(indices.shape, job.stridesX); // WARNING: Remember that X in the shader code is updates, but here X is the input tensor!
                    }

                    job.posAxis = axis;
                    job.rank = X.shape.rank;

                    // TODO: please refactor this to a single worker job instead of relying on this hack?
                    job.ScheduleXBO(Pin(updates), Pin(indices), Pin(O), indices.shape.length,
                        (reduction == Layers.ScatterReductionMode.None) ? 32 : int.MaxValue);
                }
                else
                {
                    var job = new ScatterElementsIntJob();
                    job.outAxisSize = X.shape[axis];
                    job.reductionType = reduction;

                    unsafe
                    {
                        OpsUtils.PinTensorStridesCompact(X.shape, job.stridesO);
                        OpsUtils.PinTensorStridesCompact(indices.shape, job.stridesX); // WARNING: Remember that X in the shader code is updates, but here X is the input tensor!
                    }

                    job.posAxis = axis;
                    job.rank = X.shape.rank;

                    // TODO: please refactor this to a single worker job instead of relying on this hack?
                    job.ScheduleXBO(Pin(updates), Pin(indices), Pin(O), indices.shape.length,
                        (reduction == Layers.ScatterReductionMode.None) ? 32 : int.MaxValue);
                }
            }
        }

        /// <inheritdoc/>
        public void ScatterND(Tensor<float> X, Tensor<int> indices, Tensor<float> updates, Tensor<float> O, Layers.ScatterReductionMode reduction)
        {
            MemCopy(X, O);

            int indexRemapDim = indices.shape[-1];
            int indicesLength = indices.shape.Length(0, -1);
            int updatesLength = updates.shape.length / indicesLength;

            var job = new ScatterNDFloatJob();
            job.updatesLength = updatesLength;
            job.indexRemapDim = indexRemapDim;
            job.reduction = reduction;
            unsafe
            {
                int trailing = 1;
                for (int j = (indexRemapDim - 1); j >= 0; j--)
                {
                    job.trailing[j] = trailing;
                    job.shapeX[j] = X.shape[j];
                    trailing *= X.shape[j];
                }
            }
            job.ScheduleXSBO(Pin(X), Pin(indices), Pin(updates), Pin(O), updatesLength * indicesLength, 32);
        }

        /// <inheritdoc/>
        public void ScatterND(Tensor<int> X, Tensor<int> indices, Tensor<int> updates, Tensor<int> O, Layers.ScatterReductionMode reduction)
        {
            MemCopy(X, O);

            int indexRemapDim = indices.shape[-1];
            int indicesLength = indices.shape.Length(0, -1);
            int updatesLength = updates.shape.length / indicesLength;

            var job = new ScatterNDIntJob();
            job.updatesLength = updatesLength;
            job.indexRemapDim = indexRemapDim;
            job.reduction = reduction;
            unsafe
            {
                int trailing = 1;
                for (int j = (indexRemapDim - 1); j >= 0; j--)
                {
                    job.trailing[j] = trailing;
                    job.shape[j] = X.shape[j];
                    trailing *= X.shape[j];
                }
            }
            job.ScheduleXSBO(Pin(X), Pin(indices), Pin(updates), Pin(O), updatesLength * indicesLength, 32);
        }

        /// <inheritdoc/>
        public void OneHot(Tensor<int> X, Tensor<int> O, int axis, int depth, int offValue, int onValue, bool allowNegativeIndexes)
        {
            var job = new OneHotJob();
            job.negativeIndexOffset = allowNegativeIndexes ? depth : 0;
            job.offValue = offValue;
            job.onValue = onValue;
            job.stridesToAxis = O.shape.Strides(axis);
            job.axisDim = depth;

            job.ScheduleXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void OneHot(Tensor<int> X, Tensor<float> O, int axis, int depth, float offValue, float onValue, bool allowNegativeIndexes)
        {
            var job = new OneHotJob();
            job.negativeIndexOffset = allowNegativeIndexes ? depth : 0;
            job.offValue = math.asint(offValue);
            job.onValue = math.asint(onValue);
            job.stridesToAxis = O.shape.Strides(axis);
            job.axisDim = depth;

            job.ScheduleXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void TopK(Tensor<float> X, Tensor<float> values, Tensor<int> indices, int k, int axis, bool largest)
        {
            int reduceLength = X.shape[axis];
            int innerLength = X.shape.Strides(axis);
            int outerLength = X.shape.length / (reduceLength * innerLength);

            var pinX = Pin(X);
            var pinV = Pin(values);
            var pinI = Pin(indices);
            var job = new TopKFloatJob();
            job.innerLength = innerLength;
            job.reduceLength = reduceLength;
            job.maxK = k;
            job.direction = largest ? 1 : -1;
            unsafe
            {
                job.Xptr = (float*)pinX.rawPtr;
                job.Valuesptr = (float*)pinV.rawPtr;
                job.Indicesptr = (int*)pinI.rawPtr;
            }
            pinX.reuse = pinV.fence = pinI.fence = job.Schedule(outerLength * innerLength, 32, JobHandle.CombineDependencies(pinX.fence, pinV.reuse, pinI.reuse));
        }

        /// <inheritdoc/>
        public void TopK(Tensor<int> X, Tensor<int> values, Tensor<int> indices, int k, int axis, bool largest)
        {
            int reduceLength = X.shape[axis];
            int innerLength = X.shape.Strides(axis);
            int outerLength = X.shape.length / (reduceLength * innerLength);

            var pinX = Pin(X);
            var pinV = Pin(values);
            var pinI = Pin(indices);
            var job = new TopKIntJob();
            job.innerLength = innerLength;
            job.reduceLength = reduceLength;
            job.maxK = k;
            job.direction = largest ? 1 : -1;
            unsafe
            {
                job.Xptr = (int*)pinX.rawPtr;
                job.Valuesptr = (int*)pinV.rawPtr;
                job.Indicesptr = (int*)pinI.rawPtr;
            }
            pinX.reuse = pinV.fence = pinI.fence = job.Schedule(outerLength * innerLength, 32, JobHandle.CombineDependencies(pinX.fence, pinV.reuse, pinI.reuse));
        }

        /// <inheritdoc/>
        public void RoiAlign(Tensor<float> X, Tensor<float> rois, Tensor<int> indices, Tensor<float> O, Layers.RoiPoolingMode mode, int outputHeight, int outputWidth, int samplingRatio, float spatialScale, Layers.RoiCoordinateTransformationMode coordinateTransformationMode)
        {
            var job = new RoiAlignJob();
            job.numRois = rois.shape[0];
            job.inputChannels = X.shape[1];
            job.inputHeight = X.shape[2];
            job.inputWidth = X.shape[3];
            job.inputSpatialSize = X.shape[2] * X.shape[3];
            job.inputBatchOffset = X.shape[1] * X.shape[2] * X.shape[3];
            job.outputHeight = outputHeight;
            job.outputWidth = outputWidth;
            job.normalizeOHeight = 1.0f / outputHeight;
            job.normalizeOWidth = 1.0f / outputWidth;
            job.samplingRatio = samplingRatio;
            job.spatialScale = spatialScale;
            job.mode = mode;
            job.halfPixel = coordinateTransformationMode == Layers.RoiCoordinateTransformationMode.HalfPixel;
            job.ScheduleBatchXSBO(Pin(X), Pin(rois), Pin(indices), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void RandomNormal(Tensor<float> O, float mean, float scale, int? seed)
        {
            var job = new RandomNormalJob();
            job.seed = Random.GetSeed(seed);
            job.mean = mean;
            job.scale = scale;
            job.ScheduleBatchO(Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void RandomUniform(Tensor<float> O, float low, float high, int? seed)
        {
            var job = new RandomUniformJob();
            job.seed = Random.GetSeed(seed);
            job.low = low;
            job.high = high;
            job.ScheduleBatchO(Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void Bernoulli(Tensor<float> X, Tensor O, int? seed)
        {
            var job = new BernoulliJob();
            job.seed = Random.GetSeed(seed);
            job.dataType = O.dataType;
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void TopP(Tensor<float> X, Tensor<float> random, Tensor<int> O)
        {
            var batch = O.shape.length;
            var job = new TopPJob();
            job.count = O.shape[-1];
            job.innerLength = X.shape[-1];
            job.outerLength = batch;
            job.ScheduleXBO(Pin(X), Pin(random), Pin(O), batch, 32);
        }

        /// <inheritdoc/>
        public void MemCopy(Tensor X, Tensor O)
        {
            MemCopy(X, O, X.shape.length, 0, 0);
        }

        void MemCopy(Tensor X, Tensor O, int count, int offsetX, int offsetO)
        {
            var job = new CopyJob();
            job.offsetX = offsetX;
            job.offsetO = offsetO;
            job.length = count;
            job.ScheduleXO(Pin(X), Pin(O));
        }

        /// <inheritdoc/>
        public void MemCopyStride(Tensor X, Tensor O, int strideX, int strideO, int length, int count, int offsetX, int offsetO)
        {
            if (length == 0 || count == 0)
                return;
            if (count == 1 || (strideX == length && strideO == length))
            {
                // contiguous memory can be copied together
                MemCopy(X, O, length * count, offsetX, offsetO);
                return;
            }
            Logger.AssertIsTrue(length > 0, "MemCopy.InputError: copy stride length must be greater than 0");
            Logger.AssertIsTrue(count > 0, "MemCopy.InputError: copy stride count must be greater than 0");
            Logger.AssertIsTrue(offsetX >= 0, "MemCopy.BoundsError: copy stride out of bounds for tensor X");
            Logger.AssertIsTrue(offsetX + (count - 1) * strideX + length <= X.shape.length, "MemCopy.BoundsError: copy stride out of bounds for tensor X");
            Logger.AssertIsTrue(offsetO >= 0, "MemCopy.BoundsError: copy stride out of bounds for tensor O");
            Logger.AssertIsTrue(offsetO + (count - 1) * strideO + length <= O.shape.length, "MemCopy.BoundsError: copy stride out of bounds for tensor O");
            var job = new CopyStrideJob();
            job.offsetX = offsetX;
            job.offsetO = offsetO;
            job.strideX = strideX;
            job.strideO = strideO;
            job.length = length;
            job.ScheduleXO(Pin(X), Pin(O), count, 32);
        }

        /// <inheritdoc/>
        public void Reshape(Tensor X, Tensor O)
        {
            MemCopy(X, O);
        }

        /// <inheritdoc/>
        public void ScalarMad(Tensor<float> X, Tensor<float> O, float s, float b)
        {
            var job = new ScalarMadFloatJob();
            job.s = s;
            job.b = b;
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void ScalarMad(Tensor<int> X, Tensor<int> O, int s, int b)
        {
            var job = new ScalarMadIntJob();
            job.s = s;
            job.b = b;
            job.ScheduleBatchXO(Pin(X), Pin(O), O.shape.length, 32);
        }

        /// <summary>
        /// Computes a single pass LSTM either forward or reverse
        /// dirIndex and layout are used to calculate where to index the various
        /// tensors in bidirectional and batch first layout passes
        /// X has given layout
        /// W, R are cropped to single direction
        /// P, B are full number of directions
        /// Y has given layout and full number of directions (matches output of Layer)
        /// Y_h, Y_c are SequenceFirst layout and cropped to single direction
        /// HtxRT and XsixWT are temp vectors of the correct dimension for the intermediate results of the matmuls
        /// activations, activationAlpha and activationBeta have full number of dimensions
        /// </summary>
        /// <param name="X">The input tensor.</param>
        /// <param name="W">The weights tensor.</param>
        /// <param name="R">The recurrence weights tensor.</param>
        /// <param name="B">The bias tensor.</param>
        /// <param name="sequenceLens">Optional tensor specifying lengths of the sequences in a batch.</param>
        /// <param name="P">The weight tensor for the peepholes.</param>
        /// <param name="Y">The output tensor.</param>
        /// <param name="Y_h">The output tensor for the last hidden.</param>
        /// <param name="Y_c">The output tensor for the last cell.</param>
        /// <param name="activations">The activations.</param>
        /// <param name="activationAlpha">The activation alpha value.</param>
        /// <param name="activationBeta">The activation beta value.</param>
        /// <param name="inputForget">Whether to couple the input and forget gates.</param>
        /// <param name="clip">The cell clip threshold.</param>
        /// <param name="isReverse">Whether the direction is reverse.</param>
        /// <param name="dirIndex">Which pass this is in a bidirectional LSTM.</param>
        /// <param name="layout">The layout of the tensors.</param>
        protected void SinglePassLSTM(Tensor<float> X, Tensor<float> W, Tensor<float> R, Tensor<float> B, Tensor<int> sequenceLens, Tensor<float> P, Tensor<float> Y, Tensor<float> Y_h, Tensor<float> Y_c, Layers.RnnActivation[] activations, float[] activationAlpha, float[] activationBeta, bool inputForget, float clip, bool isReverse, int dirIndex, Layers.RnnLayout layout)
        {
            var pinY = Pin(Y);

            var pinB = Pin(B);
            var pinP = Pin(P);
            var pinY_h = Pin(Y_h);
            var pinY_c = Pin(Y_c);
            var pinSequenceLens = Pin(sequenceLens);

            var numDirections = B.shape[0];
            var inputSize = X.shape[2];
            var hiddenSize = R.shape[2];

            var seqLength = X.shape[0];
            var batchSize = X.shape[1];

            var xStrideSeq = batchSize * 4 * hiddenSize;
            var xStrideBatch = 4 * hiddenSize;

            var yStrideDir = batchSize * hiddenSize;
            var yStrideSeq = numDirections * batchSize * hiddenSize;
            var yStrideBatch = hiddenSize;

            if (layout == Layers.RnnLayout.BatchFirst)
            {
                seqLength = X.shape[1];
                batchSize = X.shape[0];

                xStrideSeq = 4 * hiddenSize;
                xStrideBatch = seqLength * 4 * hiddenSize;

                yStrideDir = hiddenSize;
                yStrideSeq = numDirections * hiddenSize;
                yStrideBatch = seqLength * numDirections * hiddenSize;
            }

            var HtxRT = AllocTensorFloat(new TensorShape(batchSize * 4 * hiddenSize));
            var XsixWT = AllocTensorFloat(new TensorShape(seqLength * batchSize * 4 * hiddenSize));

            var pinHtxRT = Pin(HtxRT);
            var pinXsixWT = Pin(XsixWT);

            ScheduleSGEMM(X, seqLength * batchSize, inputSize, W, 4 * hiddenSize, inputSize, XsixWT, seqLength * batchSize, 4 * hiddenSize, transposeB: true);

            var endJob = new LSTMEndJob();
            endJob.fActivation = activations[3 * dirIndex + 0];
            endJob.fAlpha = activationAlpha[3 * dirIndex + 0];
            endJob.fBeta = activationBeta[3 * dirIndex + 0];
            endJob.gActivation = activations[3 * dirIndex + 1];
            endJob.gAlpha = activationAlpha[3 * dirIndex + 1];
            endJob.gBeta = activationBeta[3 * dirIndex + 1];
            endJob.hActivation = activations[3 * dirIndex + 2];
            endJob.hAlpha = activationAlpha[3 * dirIndex + 2];
            endJob.hBeta = activationBeta[3 * dirIndex + 2];
            endJob.hiddenSize = hiddenSize;
            endJob.xStride = xStrideBatch;
            endJob.yStride = yStrideBatch;
            endJob.inputForget = inputForget;
            endJob.clip = clip;
            unsafe
            {
                endJob.YHptr = (float*)pinY_h.rawPtr;
                endJob.YCptr = (float*)pinY_c.rawPtr;
                endJob.Bptr = (float*)pinB.rawPtr + dirIndex * 8 * hiddenSize;
                endJob.Pptr = (float*)pinP.rawPtr + dirIndex * 3 * hiddenSize;
                endJob.HtxRTptr = (float*)pinHtxRT.rawPtr;
                endJob.SequenceLensptr = (int*)pinSequenceLens.rawPtr;
            }

            for (var i = 0; i < seqLength; i++)
            {
                var seqIndex = isReverse ? seqLength - 1 - i : i;

                ScheduleSGEMM(Y_h, batchSize, hiddenSize, R, 4 * hiddenSize, hiddenSize, HtxRT, batchSize, 4 * hiddenSize, transposeB: true);

                unsafe
                {
                    endJob.seqIndex = seqIndex;
                    endJob.Yptr = (float*)pinY.rawPtr + dirIndex * yStrideDir + seqIndex * yStrideSeq;
                    endJob.XsixWTptr = (float*)pinXsixWT.rawPtr + seqIndex * xStrideSeq;

                    pinY_h.fence = pinY_c.fence = pinY.fence = pinP.reuse = pinB.reuse = pinXsixWT.reuse = pinHtxRT.reuse = pinSequenceLens.reuse =
                        endJob.Schedule(batchSize, 1, JobHandle.CombineDependencies(pinY.reuse, pinY_h.reuse, JobHandle.CombineDependencies(pinY_c.reuse, pinP.fence, JobHandle.CombineDependencies(pinB.fence, pinXsixWT.fence, JobHandle.CombineDependencies(pinHtxRT.fence, pinSequenceLens.fence)))));
                }
            }

            ReleaseTensorFloat(HtxRT);
            ReleaseTensorFloat(XsixWT);
        }

        /// <summary>
        /// Sets final output tensor for W, R, initialH and initialC from provided input tensors
        /// if no input is provided the tensor is cleared to 0 as a default
        /// otherwise if the input tensor can be used directly in the calculation this will early out
        /// </summary>
        void SetRnnInput(Tensor<float> X, Tensor<float> O, int index, int count, int length, int strideX)
        {
            if (X == O)
                return;
            if (X == null)
                MemClear(O);
            else
                MemCopyStride(X, O, strideX, length, length, count, index * length, 0);
        }

        /// <summary>
        /// Sets intermediate input tensors for Y_h and Y_c from intermediate output tensor
        /// if the calculation is single direction and sequenceFirst layout then the output
        /// tensor will be used directly and this command early outs
        /// </summary>
        void SetRnnOutput(Tensor<float> X, Tensor<float> O, int index, int count, int length, int strideO)
        {
            if (X == O)
                return;
            MemCopyStride(X, O, length, strideO, length, count, 0, index * length);
        }

        /// <inheritdoc/>
        public void LSTM(Tensor<float> X, Tensor<float> W, Tensor<float> R, Tensor<float> B, Tensor<int> sequenceLens, Tensor<float> initialH, Tensor<float> initialC, Tensor<float> P, Tensor<float> Y, Tensor<float> Yh, Tensor<float> Yc, Layers.RnnDirection direction, Layers.RnnActivation[] activations, float[] activationAlpha, float[] activationBeta, bool inputForget, float clip, Layers.RnnLayout layout)
        {
            var seqLength = X.shape[layout == Layers.RnnLayout.SequenceFirst ? 0 : 1];
            var batchSize = X.shape[layout == Layers.RnnLayout.SequenceFirst ? 1 : 0];
            var inputSize = X.shape[2];
            var hiddenSize = R.shape[2];
            var numDirections = W.shape[0];

            var W1 = numDirections == 2 ? AllocTensorFloat(new TensorShape(1, 4 * hiddenSize, inputSize)) : W;
            var R1 = numDirections == 2 ? AllocTensorFloat(new TensorShape(1, 4 * hiddenSize, hiddenSize)) : R;

            var Bi = B;
            if (B == null)
            {
                Bi = AllocTensorFloat(new TensorShape(numDirections, 8 * hiddenSize));
                MemClear(Bi);
            }
            var sequenceLensi = sequenceLens;
            if (sequenceLens == null)
            {
                sequenceLensi = AllocTensorInt(new TensorShape(batchSize));
                MemSet(sequenceLensi, math.asint(seqLength));
            }
            var Pi = P;
            if (P == null)
            {
                Pi = AllocTensorFloat(new TensorShape(numDirections, 3 * hiddenSize));
                MemClear(Pi);
            }

            var Y_h1 = layout == Layers.RnnLayout.SequenceFirst ? (numDirections == 2 ? AllocTensorFloat(new TensorShape(1, batchSize, hiddenSize)) : Yh) : AllocTensorFloat(new TensorShape(batchSize, 1, hiddenSize));
            var Y_c1 = layout == Layers.RnnLayout.SequenceFirst ? (numDirections == 2 ? AllocTensorFloat(new TensorShape(1, batchSize, hiddenSize)) : Yc) : AllocTensorFloat(new TensorShape(batchSize, 1, hiddenSize));

            var Y_hcLower = layout == Layers.RnnLayout.SequenceFirst ? batchSize * hiddenSize : hiddenSize;
            var Y_hcUpper = layout == Layers.RnnLayout.SequenceFirst ? 1 : batchSize;

            for (var i = 0; i < numDirections; i++)
            {
                SetRnnInput(W, W1, i, 1, 4 * hiddenSize * inputSize, 0);
                SetRnnInput(R, R1, i, 1, 4 * hiddenSize * hiddenSize, 0);
                SetRnnInput(initialH, Y_h1, i, Y_hcUpper, Y_hcLower, numDirections * Y_hcLower);
                SetRnnInput(initialC, Y_c1, i, Y_hcUpper, Y_hcLower, numDirections * Y_hcLower);
                var isReverse = direction == Layers.RnnDirection.Reverse || (direction == Layers.RnnDirection.Bidirectional && i == 1);
                SinglePassLSTM(X, W1, R1, Bi, sequenceLensi, Pi, Y, Y_h1, Y_c1, activations, activationAlpha, activationBeta, inputForget, clip, isReverse, i, layout);
                SetRnnOutput(Y_h1, Yh, i, Y_hcUpper, Y_hcLower, numDirections * Y_hcLower);
                SetRnnOutput(Y_c1, Yc, i, Y_hcUpper, Y_hcLower, numDirections * Y_hcLower);
            }

            if (numDirections == 2)
            {
                ReleaseTensorFloat(W1);
                ReleaseTensorFloat(R1);
            }
            if (B == null)
            {
                ReleaseTensorFloat(Bi);
            }
            if (sequenceLens == null)
            {
                ReleaseTensorInt(sequenceLensi);
            }
            if (P == null)
            {
                ReleaseTensorFloat(Pi);
            }
            if (layout != Layers.RnnLayout.SequenceFirst || numDirections == 2)
            {
                ReleaseTensorFloat(Y_h1);
                ReleaseTensorFloat(Y_c1);
            }
        }

        /// <summary>
        /// Sets the elements of a 1D int tensor O to the dimensions of a shape.
        /// </summary>
        public void SetShape(Tensor<int> O, TensorShape shape)
        {
            var job = new SetShapeJob();

            job.rank = shape.rank;
            unsafe
            {
                for (var i = 0; i < shape.rank; i++)
                    job.dims[i] = shape[i];
            }

            job.ScheduleO(Pin(O));
        }

        public void STFT(Tensor<float> signal, Tensor<float> window, Tensor<float> O, int frameStep, int frameLength,
            Tensor<float> windowedDFTMatrix, Tensor<float> windowedDFTMatrixCosPart, Tensor<float> windowedDFTMatrixSinPart,
            bool inverse, bool onesided, float scale)
        {
            bool haveDFTMatrix = windowedDFTMatrix != null;
            bool haveCosSinMatrix = windowedDFTMatrixCosPart != null && windowedDFTMatrixSinPart != null;

            bool signalIsReal = signal.shape[-1] == 1;
            var twiddleMatrixSplitShape = new TensorShape(O.shape[-2], frameLength);
            int numDFTFreq = O.shape[-2];
            int numFrames = O.shape[-3];

            bool freeWindowedCosSinTwiddle = false;

            // CPU prefers cos/sin split matrix but our compiler pass gives us the combined as unknown backend will be used:
            if (!haveCosSinMatrix && haveDFTMatrix)
            {
                // TODO if we want to avoid this splitting job:
                // multi-format WindowedDFTMatrix operator that is only used by the optimizer pass + STFT multi inputs (to feed all possible formats, backend picks most appropriate)

                // We consider the DFT matrix won't have the normalizing scale for inverse DFT,
                // we will apply it here although would be better in the convolution later for precision.
                freeWindowedCosSinTwiddle = true;
                windowedDFTMatrixCosPart = AllocTensorFloat(twiddleMatrixSplitShape);
                windowedDFTMatrixSinPart = AllocTensorFloat(twiddleMatrixSplitShape);
                WindowedDFTMatrixRealImaSplitFromCombined(windowedDFTMatrix, windowedDFTMatrixCosPart, windowedDFTMatrixSinPart, dftLength: frameLength, inputFrameLength: frameLength, inverse, onesided, alternateRealImaOnRows: signalIsReal, scale: 1.0f);
            }
            else if (!haveCosSinMatrix)
            {
                freeWindowedCosSinTwiddle = true;
                windowedDFTMatrixCosPart = AllocTensorFloat(twiddleMatrixSplitShape);
                windowedDFTMatrixSinPart = AllocTensorFloat(twiddleMatrixSplitShape);
                WindowedDFTMatrixRealIma(window, windowedDFTMatrixCosPart, windowedDFTMatrixSinPart, dftLength: frameLength, inputFrameLength: frameLength, inverse: inverse, onesided, alternateRealImaOnRows: signalIsReal, scale: 1.0f);
            }

            // We will make sure the signal shape conforms to what is expected by conv, input channel = trivially 1:
            var signalShapeForConv = new TensorShape(signal.shape[0], 1, signal.shape[1]);

            Tensor<float> signalRealPart = null;
            Tensor<float> signalImaPart = null;
            if (!signalIsReal)
            {
                signalRealPart = AllocTensorFloat(new TensorShape(signal.shape[0], signal.shape[1], 1));
                signalImaPart = AllocTensorFloat(signalRealPart.shape);

                void SliceSetWithSrcOffset(Tensor X, Tensor O, int axis, int srcstart, int srcstep, int dststart, int dststep)
                {
                    var strideX = X.shape.Length(axis);
                    var strideO = O.shape.Length(axis) * dststep;
                    var length = strideX / srcstep;
                    var count = X.shape.Length(0, axis);
                    MemCopyStride(X, O, strideX, strideO, length, count, X.shape.Strides(axis) * srcstart, O.shape.Strides(axis) * dststart);
                }
                SliceSetWithSrcOffset(signal, signalRealPart, axis: -1, srcstart: 0, srcstep: 2, dststart: 0, dststep: 1);
                SliceSetWithSrcOffset(signal, signalImaPart, axis: -1, srcstart: 1, srcstep: 2, dststart: 0, dststep: 1);

                signalRealPart.shape = signalShapeForConv;
                signalImaPart.shape = signalShapeForConv;
            }

            // Convolution output channel will be the N-dian frequency and "spatial" will be the frames, so we have to transpose:
            ReadOnlySpan<int> permutations = stackalloc int[3] { 0, 2, 1 };

            var realPartResult = AllocTensorFloat(new TensorShape(signal.shape[0], numDFTFreq, numFrames));
            var imaPartResult = AllocTensorFloat(realPartResult.shape);

            // For complex signal: the prefix of identifiers is matrix re/im and signal re/im in the conv order of operations:
            Tensor<float> rereResult = null, imimResult = null, reimResult = null, imreResult = null;
            if (!signalIsReal)
            {
                rereResult = AllocTensorFloat(realPartResult.shape);
                imreResult = AllocTensorFloat(realPartResult.shape);
                reimResult = AllocTensorFloat(realPartResult.shape);
                imimResult = AllocTensorFloat(realPartResult.shape);
            }

            Span<int> strides = stackalloc int[4] { frameStep, 0, 0, 0 };
            Span<int> pads = stackalloc int[4] { 0, 0, 0, 0 };
            Span<int> dilations = stackalloc int[4] { 1, 1, 1, 1 };

            var signalShapeBack = signal.shape;
            if (signalIsReal)
                signal.shape = signalShapeForConv;

            // Transform our twiddle matrices in proper kernels: input channel = trivially 1
            windowedDFTMatrixCosPart.shape = windowedDFTMatrixCosPart.shape.Unsqueeze(1);
            windowedDFTMatrixSinPart.shape = windowedDFTMatrixSinPart.shape.Unsqueeze(1);

            Conv(signalIsReal ? signal : signalRealPart, windowedDFTMatrixCosPart, null, signalIsReal ? realPartResult : rereResult, 1, strides, pads, dilations, Layers.FusableActivation.None);
            Conv(signalIsReal ? signal : signalRealPart, windowedDFTMatrixSinPart, null, signalIsReal ? imaPartResult : imreResult, 1, strides, pads, dilations, Layers.FusableActivation.None);

            if (!signalIsReal)
            {
                Conv(signalImaPart, windowedDFTMatrixCosPart, null, reimResult, 1, strides, pads, dilations, Layers.FusableActivation.None);
                Conv(signalImaPart, windowedDFTMatrixSinPart, null, imimResult, 1, strides, pads, dilations, Layers.FusableActivation.None);
            }

            // Restore shapes
            windowedDFTMatrixCosPart.shape = twiddleMatrixSplitShape;
            windowedDFTMatrixSinPart.shape = twiddleMatrixSplitShape;
            if (signalIsReal)
                signal.shape = signalShapeBack;

            if (!signalIsReal)
            {
                // We have, in matrix notation:
                // signal.re = (D.re S.re - D.im S.im )
                // signal.im = (D.re S.im + D.im S.re )
                // realPartResult = (rereResult - imimResult )
                // imaPartResult = (reimResult + imreResult )

                Sub(rereResult, imimResult, realPartResult);
                Add(reimResult, imreResult, imaPartResult);
            }

            var realPartResultInOrder = AllocTensorFloat(realPartResult.shape.Transpose(permutations)); // ie new TensorShape(signal.shape[0], numFrames, numDFTFreq)
            var imagPartResultInOrder = AllocTensorFloat(realPartResultInOrder.shape);

            Transpose(realPartResult, realPartResultInOrder, permutations);
            Transpose(imaPartResult, imagPartResultInOrder, permutations);

            // Stack on last axis to combine real/ima part in a new innermost dimension of size 2:
            // We will use our unsqueeze and concat:
            realPartResultInOrder.shape = realPartResultInOrder.shape.Unsqueeze(-1);
            imagPartResultInOrder.shape = imagPartResultInOrder.shape.Unsqueeze(-1);

            SliceSet(realPartResultInOrder, O, axis: -1, start: 0, 1);
            SliceSet(imagPartResultInOrder, O, axis: -1, start: 1, 1);

            ReleaseTensorFloat(realPartResult);
            ReleaseTensorFloat(imaPartResult);
            ReleaseTensorFloat(realPartResultInOrder);
            ReleaseTensorFloat(imagPartResultInOrder);
            if (!signalIsReal)
            {
                ReleaseTensorFloat(signalRealPart);
                ReleaseTensorFloat(signalImaPart);
                ReleaseTensorFloat(rereResult);
                ReleaseTensorFloat(imreResult);
                ReleaseTensorFloat(reimResult);
                ReleaseTensorFloat(imimResult);
            }
            if (freeWindowedCosSinTwiddle)
            {
                ReleaseTensor(windowedDFTMatrixCosPart);
                ReleaseTensor(windowedDFTMatrixSinPart);
            }
        }

        /// <inheritdoc/>
        public void STFT(Tensor<float> signal, Tensor<float> window, Tensor<float> O, int frameStep, int frameLength, Tensor<float> windowedDFTMatrix, bool onesided)
        {
            STFT(signal, window, O, frameStep, frameLength, windowedDFTMatrix, windowedDFTMatrixCosPart: null, windowedDFTMatrixSinPart: null, inverse: false, onesided, scale: 1.0f);
        }

        /// <inheritdoc/>
        public void DFT(Tensor<float> input, Tensor<float> O, int dftLength, int axis, Tensor<float> dftMatrix, bool inverse, bool onesided)
        {
            bool signalIsReal = input.shape[-1] == 1;
            int signalFrameLengthToUse = Math.Min(dftLength, input.shape[axis]);
            int outputXformSignalLength = O.shape[axis]; // onesided ? dftLength / 2 + 1 : dftLength;

            bool haveDFTMatrix = dftMatrix != null;
            var twiddleMatrixSplitShape = new TensorShape(outputXformSignalLength, signalFrameLengthToUse);

            Tensor<float> windowedDFTMatrixCosPart = AllocTensorFloat(twiddleMatrixSplitShape);
            Tensor<float> windowedDFTMatrixSinPart = AllocTensorFloat(twiddleMatrixSplitShape);
            // CPU prefers cos/sin split matrix but our compiler pass gives us the combined as unknown backend will be used:
            // TODO if we want to avoid this splitting job:
            // multi-format WindowedDFTMatrix operator that is only used by the optimizer pass + STFT multi inputs (to feed all possible formats, backend picks most appropriate)
            // We consider the DFT matrix won't have the normalizing scale for inverse DFT,
            // we will apply it here although would be better in the convolution later for precision.
            if (haveDFTMatrix)
                WindowedDFTMatrixRealImaSplitFromCombined(dftMatrix, windowedDFTMatrixCosPart, windowedDFTMatrixSinPart, dftLength, signalFrameLengthToUse, inverse, onesided, alternateRealImaOnRows: signalIsReal, scale: 1.0f / (float)(dftLength));
            else
                // generate it in split parts for STFT on CPU: also note that compared to GPU compute and pixel backends, here we bake the 1.0f/dftLength scale in it
                WindowedDFTMatrixRealIma(window: null, windowedDFTMatrixCosPart, windowedDFTMatrixSinPart, dftLength, signalFrameLengthToUse, inverse, onesided, alternateRealImaOnRows: signalIsReal, 1.0f / (float)(dftLength));

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

            bool needTranspose = input.shape.Axis(axis) != (input.shape.rank - 2);
            Tensor<float> preSTFTTransposeOut = sliceOut;
            Tensor<float> stftOut = O;
            Span<int> permutationsTmp = stackalloc int[TensorShape.maxRank];
            TensorShape stftOutShape = O.shape;
            if (needTranspose)
            {
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
                preSTFTTransposeOut = AllocTensorFloat(transposedShape);

                Transpose(sliceOut, preSTFTTransposeOut, permutations);

                stftOutShape = O.shape.Transpose(permutations);
                stftOut = AllocTensorFloat(stftOutShape);
            }

            var preSTFTTransposeOutShapeBack = preSTFTTransposeOut.shape;
            var stftOutShapeBack = stftOut.shape;

            // flatten everything into one big signal of numFrames * frameLength
            // numFrames = stftOut.shape.Length(0) / stftOut.shape.Length(-2))
            // frameLength = stftOut.shape[-2]
            preSTFTTransposeOut.shape = new TensorShape(1, preSTFTTransposeOut.shape.Length(0) / preSTFTTransposeOut.shape[-1], preSTFTTransposeOut.shape[-1]);
            stftOut.shape = new TensorShape(1, stftOut.shape.Length(0) / stftOut.shape.Length(-2), stftOut.shape[-2], stftOut.shape[-1]);

            STFT(preSTFTTransposeOut, window: null, stftOut, frameStep: signalFrameLengthToUse, frameLength: signalFrameLengthToUse,
                windowedDFTMatrix: null, windowedDFTMatrixCosPart: windowedDFTMatrixCosPart, windowedDFTMatrixSinPart: windowedDFTMatrixSinPart,
                inverse: inverse, onesided, scale: !inverse ? 1.0f : 1.0f / (float)(dftLength));
            // ...note scale could be outputXformSignalLength, since onesided only valid for DFT not IDFT anyway.

            stftOut.shape = stftOutShapeBack;
            preSTFTTransposeOut.shape = preSTFTTransposeOutShapeBack;

            // Reverse the transposition if needed:
            if (needTranspose)
            {
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

                Transpose(stftOut, O, permutations);
            }

            // possible aliases:
            //      preSTFTTransposeOut =(if !transpose)= sliceOut =(if !slice)= input
            //      stftOut =(if !transpose)= O
            if (needSlicing)
                ReleaseTensorFloat(sliceOut);
            if (needTranspose)
            {
                ReleaseTensorFloat(preSTFTTransposeOut);
                ReleaseTensorFloat(stftOut);
            }

            // These are always allocated, we split the precomputed dftMatrix
            // or generate directly, either way we have allocated those:
            ReleaseTensor(windowedDFTMatrixCosPart);
            ReleaseTensor(windowedDFTMatrixSinPart);
        }

        /// <summary>
        /// Computes a DFT matrix with an optional window baked-in: used automatically internally by the graph optimizer to precompute it for the STFT when possible
        /// (framelength known, signal shape partially known to be real/complex)
        /// </summary>
        /// <param name="window">The optional window tensor that modulates a signal frame when using the output matrix to calculate a DFT.</param>
        /// <param name="dftLength">The size of a DFT. Can be larger than 'inputFrameLength' - in that case the DFT matrix is built as if the input signal would be zero padded.</param>
        /// <param name="inputFrameLength">The size of the input signal that this DFT will apply to. If window is specified, has to be the same as the window length.</param>
        /// <param name="onesided">Returns only half of the DFT frequency results (real signals have a symmetric DFT spectrum).</param>
        /// <param name="alternateRealImaOnRows">Specify if alternate real/ima parts should be on rows, else on columns.</param>
        /// <param name="scale">Optional scalar scale applied to all matrix elements.</param>
        /// <returns>The output tensor.</returns>
        public void WindowedDFTMatrix(Tensor<float> window, Tensor<float> O, int dftLength, int inputFrameLength, bool inverse, bool onesided, bool alternateRealImaOnRows, float scale = 1.0f)
        {
            Logger.AssertIsTrue(dftLength >= inputFrameLength, "WindowedDFTMatrix: dftLength should be >= inputFrameLength.");

            var dftMatrixJob = new WindowedDFTMatrixJob();
            dftMatrixJob.frameLength = inputFrameLength;
            dftMatrixJob.fundamentalFreq = 1.0f / ((float)dftLength);
            dftMatrixJob.inverse = inverse;
            dftMatrixJob.scale = inverse ? scale : 1.0f;
            dftMatrixJob.alternateRealImaOnRows = alternateRealImaOnRows;

            CPUTensorData pinWindow = (window != null) ? Pin(window) : null;
            var pinOutput = Pin(O);

            unsafe
            {
                dftMatrixJob.Windowptr = (float*)((window != null) ? pinWindow.rawPtr : null);
                dftMatrixJob.WindowedDFTMatrixptr = (float*)pinOutput.rawPtr;
            }

            JobHandle indep = (window != null) ? JobHandle.CombineDependencies(pinWindow.fence, pinOutput.reuse) : pinOutput.reuse;

            int outputXformSignalLength = onesided ? dftLength / 2 + 1 : dftLength;
            int dispatchSize = inputFrameLength * outputXformSignalLength;
            Logger.AssertIsTrue(O.shape.length == dispatchSize * 2, "WindowedDFTMatrix: shape error");

            var jobDependencies = dftMatrixJob.ScheduleBatch(dispatchSize, 32, indep);
            pinOutput.fence = jobDependencies;

            if (window != null)
                pinWindow.reuse = jobDependencies;
        }

        // Generates DFT matrix as 2 separate real/ima (cos/sin) matrices:
        // If inverse == true, applies an optional scale
        public void WindowedDFTMatrixRealIma(Tensor<float> window, Tensor<float> Oreal, Tensor<float> Oima, int dftLength, int inputFrameLength, bool inverse, bool onesided, bool alternateRealImaOnRows, float scale = 1.0f)
        {
            var dftMatrixJob = new WindowedDFTMatrixOutRealImaJob();
            dftMatrixJob.inverse = inverse;
            dftMatrixJob.scale = inverse ? scale : 1.0f;
            dftMatrixJob.frameLength = inputFrameLength;
            dftMatrixJob.fundamentalFreq = 1.0f / ((float)dftLength);

            CPUTensorData pinWindow = null;
            if (window != null)
                pinWindow = Pin(window);

            var pinRealPart = Pin(Oreal);
            var pinImaPart = Pin(Oima);

            unsafe
            {
                dftMatrixJob.Windowptr = (float*)((window != null) ? pinWindow.rawPtr : null);
                dftMatrixJob.RealPartptr = (float*)pinRealPart.rawPtr;
                dftMatrixJob.ImaPartptr = (float*)pinImaPart.rawPtr;
            }

            JobHandle indep;
            if (window != null)
                indep = JobHandle.CombineDependencies(pinWindow.fence, pinRealPart.reuse, pinImaPart.reuse);
            else
                indep = JobHandle.CombineDependencies(pinRealPart.reuse, pinImaPart.reuse);

            int outputXformSignalLength = onesided ? dftLength / 2 + 1 : dftLength;
            int dispatchSize = inputFrameLength * outputXformSignalLength;

            JobHandle jobDependencies = dftMatrixJob.ScheduleBatch(dispatchSize, 32, indep);
            pinRealPart.fence = pinImaPart.fence = jobDependencies;

            if (window != null)
                pinWindow.reuse = jobDependencies;
        }

        // Split a single DFT matrix as 2 separate real/ima (cos/sin) DFT matrices.
        // If inverse == true, applies an optional scale
        public void WindowedDFTMatrixRealImaSplitFromCombined(Tensor<float> windowedDFTMatrix, Tensor<float> Oreal, Tensor<float> Oima, int dftLength, int inputFrameLength, bool inverse, bool onesided, bool alternateRealImaOnRows, float scale = 1.0f)
        {
            var dftMatrixJob = new WindowedDFTMatrixSplitMatrixToRealImaJob();
            dftMatrixJob.frameLength = inputFrameLength;
            dftMatrixJob.scale = inverse ? scale : 1.0f;
            dftMatrixJob.hasAlternateRealImaOnRows = alternateRealImaOnRows;

            var pinWindowedDFTMatrix = Pin(windowedDFTMatrix);
            var pinRealPart = Pin(Oreal);
            var pinImaPart = Pin(Oima);

            unsafe
            {
                dftMatrixJob.WindowedDFTMatrixptr = (float*)pinWindowedDFTMatrix.rawPtr;
                dftMatrixJob.RealPartptr = (float*)pinRealPart.rawPtr;
                dftMatrixJob.ImaPartptr = (float*)pinImaPart.rawPtr;
            }

            JobHandle indep;
            indep = JobHandle.CombineDependencies(pinWindowedDFTMatrix.fence, pinRealPart.reuse, pinImaPart.reuse);

            int outputXformSignalLength = onesided ? dftLength / 2 + 1 : dftLength;
            int dispatchSize = inputFrameLength * outputXformSignalLength;

            JobHandle jobDependencies = dftMatrixJob.ScheduleBatch(dispatchSize, 32, indep);
            pinRealPart.fence = pinImaPart.fence = jobDependencies;
        }

        /// <inheritdoc/>
        public void BlackmanWindow(Tensor<float> O, bool periodic)
        {
            var job = new BlackmanWindowJob();
            job.N = periodic ? O.count : O.count - 1;
            job.ScheduleBatchO(Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void HammingWindow(Tensor<float> O, bool periodic)
        {
            var job = new HammingWindowJob();
            job.N = periodic ? O.count : O.count - 1;
            job.ScheduleBatchO(Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void HannWindow(Tensor<float> O, bool periodic)
        {
            var job = new HannWindowJob();
            job.N = periodic ? O.count : O.count - 1;
            job.ScheduleBatchO(Pin(O), O.shape.length, 32);
        }

        /// <inheritdoc/>
        public void MelWeightMatrix(Tensor<float> O, int dftLength, int sampleRate, float lowerEdgeHertz, float upperEdgeHertz)
        {
            MemClear(O);
            var numMelBins = O.shape[1];
            var lowerEdgeMel = 2595 * math.log10(1 + lowerEdgeHertz / 700);
            var upperEdgeMel = 2595 * math.log10(1 + upperEdgeHertz / 700);
            var melStep = (upperEdgeMel - lowerEdgeMel) / (numMelBins + 2);
            var job = new MelWeightMatrixJob();
            job.dftLength = dftLength;
            job.sampleRate = sampleRate;
            job.lowerEdgeMel = lowerEdgeMel;
            job.melStep = melStep;
            job.numMelBins = numMelBins;
            job.ScheduleBatchO(Pin(O), job.numMelBins, 32);
        }

    }
}
