using System;

namespace Unity.InferenceEngine.Layers
{
    /// <summary>
    /// Options for the direction of a recurrent layer.
    /// </summary>
    enum RnnDirection
    {
        /// <summary>
        /// Use only forward direction in the calculation.
        /// </summary>
        Forward = 0,
        /// <summary>
        /// Use only reverse direction in the calculation.
        /// </summary>
        Reverse = 1,
        /// <summary>
        /// Use both forward and reverse directions in the calculation.
        /// </summary>
        Bidirectional = 2,
    }

    /// <summary>
    /// Options for activation functions to apply in a recurrent layer.
    /// </summary>
    enum RnnActivation
    {
        /// <summary>
        /// Use `Relu` activation: f(x) = max(0, x).
        /// </summary>
        Relu = 0,
        /// <summary>
        /// Use `Tanh` activation: f(x) = (1 - e^{-2x}) / (1 + e^{-2x}).
        /// </summary>
        Tanh = 1,
        /// <summary>
        /// Use `Sigmoid` activation: f(x) = 1 / (1 + e^{-x}).
        /// </summary>
        Sigmoid = 2,
        /// <summary>
        /// Use `Affine` activation: f(x) = alpha * x + beta.
        /// </summary>
        Affine = 3,
        /// <summary>
        /// Use `LeakyRelu` activation: f(x) = x if x >= 0, otherwise f(x) = alpha * x.
        /// </summary>
        LeakyRelu = 4,
        /// <summary>
        /// Use `ThresholdedRelu` activation: f(x) = x if x >= alpha, otherwise f(x) = 0.
        /// </summary>
        ThresholdedRelu = 5,
        /// <summary>
        /// Use `ScaledTanh` activation: f(x) = alpha * tanh(beta * x).
        /// </summary>
        ScaledTanh = 6,
        /// <summary>
        /// Use `HardSigmoid` activation: f(x) = clamp(alpha * x + beta, 0, 1).
        /// </summary>
        HardSigmoid = 7,
        /// <summary>
        /// Use `Elu` activation: f(x) = x if x >= 0, otherwise f(x) = alpha * (e^x - 1).
        /// </summary>
        Elu = 8,
        /// <summary>
        /// Use `Softsign` activation: f(x) = x / (1 + |x|).
        /// </summary>
        Softsign = 9,
        /// <summary>
        /// Use `Softplus` activation: f(x) = log(1 + e^x).
        /// </summary>
        Softplus = 10,
    }

    /// <summary>
    /// Options for the layout of the tensor in a recurrent layer.
    /// </summary>
    enum RnnLayout
    {
        /// <summary>
        /// Use layout with sequence as the first dimension of the tensors.
        /// </summary>
        SequenceFirst = 0,
        /// <summary>
        /// Use layout with batch as the first dimension of the tensors.
        /// </summary>
        BatchFirst = 1,
    }

    /// <summary>
    /// Represents an `LSTM` recurrent layer. This generates an output tensor by computing a one-layer LSTM (long short-term memory) on an input tensor.
    /// </summary>
    [Operator(category = "Recurrent")]
    [Inputs(names = new[] { "X", "W", "R", "B", "sequenceLens", "initialH", "initialC", "P" })]
    [Outputs(names = new[] { "Y", "Y_h", "Y_c" })]
    partial class LSTM : Layer
    {
        public int hiddenSize;
        public RnnDirection direction;
        public RnnActivation[] activations;
        public float[] activationAlpha;
        public float[] activationBeta;
        public float clip;
        public bool inputForget;
        public RnnLayout layout;

        internal override int OutputCount => 3;

        internal static PartialTensor[] InferPartial(PartialTensor X, PartialTensor W, PartialTensor R, PartialTensor B, PartialTensor sequenceLens, PartialTensor initialH, PartialTensor initialC, PartialTensor P, int hiddenSize, RnnDirection direction, RnnActivation[] activations, float[] activationAlpha, float[] activationBeta, float clip, bool inputForget, RnnLayout layout)
        {
            var shapeX = X.shape;
            var shapeW = W.shape;
            var shapeR = R.shape;

            var seqLength = DynamicTensorDim.Unknown;
            var batchSize = DynamicTensorDim.Unknown;

            shapeX.DeclareRank(3);
            shapeW.DeclareRank(3);
            shapeR.DeclareRank(3);

            seqLength = DynamicTensorDim.MaxDefinedDim(seqLength, layout == RnnLayout.SequenceFirst ? shapeX[0] : shapeX[1]);
            batchSize = DynamicTensorDim.MaxDefinedDim(batchSize, layout == RnnLayout.SequenceFirst ? shapeX[1] : shapeX[0]);

            B?.shape.DeclareRank(2);

            if (sequenceLens != null && sequenceLens.shape is var shapeSequenceLens)
            {
                shapeSequenceLens.DeclareRank(1);
                batchSize = DynamicTensorDim.MaxDefinedDim(batchSize, shapeSequenceLens[0]);
            }

            if (initialH != null && initialH.shape is var shapeInitialH)
            {
                shapeInitialH.DeclareRank(3);
                batchSize = DynamicTensorDim.MaxDefinedDim(batchSize, layout == RnnLayout.SequenceFirst ? shapeInitialH[1] : shapeInitialH[0]);
            }

            if (initialC != null && initialC.shape is var shapeInitialC)
            {
                shapeInitialC.DeclareRank(3);
                batchSize = DynamicTensorDim.MaxDefinedDim(batchSize, layout == RnnLayout.SequenceFirst ? shapeInitialC[1] : shapeInitialC[0]);
            }

            P?.shape.DeclareRank(2);

            var numDirectionsDim = DynamicTensorDim.Int(direction == RnnDirection.Bidirectional ? 2 : 1);
            var hiddenSizeDim = DynamicTensorDim.Int(hiddenSize);

            if (layout == RnnLayout.SequenceFirst)
            {
                return new PartialTensor[]
                {
                    new PartialTensor<float>(new DynamicTensorShape(seqLength, numDirectionsDim, batchSize, hiddenSizeDim)),
                    new PartialTensor<float>(new DynamicTensorShape(numDirectionsDim, batchSize, hiddenSizeDim)),
                    new PartialTensor<float>(new DynamicTensorShape(numDirectionsDim, batchSize, hiddenSizeDim))
                };
            }
            else
            {
                return new PartialTensor[]
                {
                    new PartialTensor<float>(new DynamicTensorShape(batchSize, seqLength, numDirectionsDim, hiddenSizeDim)),
                    new PartialTensor<float>(new DynamicTensorShape(batchSize, numDirectionsDim, hiddenSizeDim)),
                    new PartialTensor<float>(new DynamicTensorShape(batchSize, numDirectionsDim, hiddenSizeDim))
                };
            }
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var W = ctx.storage.GetTensor(inputs[1]) as Tensor<float>;
            var R = ctx.storage.GetTensor(inputs[2]) as Tensor<float>;
            var B = ctx.storage.GetTensor(inputs[3]) as Tensor<float>;
            var sequenceLens = ctx.storage.GetTensor(inputs[4]) as Tensor<int>;
            var initialH = ctx.storage.GetTensor(inputs[5]) as Tensor<float>;
            var initialC = ctx.storage.GetTensor(inputs[6]) as Tensor<float>;
            var P = ctx.storage.GetTensor(inputs[7]) as Tensor<float>;

            ShapeInference.LSTM(X.shape, W.shape, R.shape, layout, out var shapeY, out var shapeY_h, out var shapeY_c);
            var Y = ctx.storage.AllocateTensorAndStore(outputs[0], shapeY, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            var Y_h = ctx.storage.AllocateTensorAndStore(outputs[1], shapeY_h, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            var Y_c = ctx.storage.AllocateTensorAndStore(outputs[2], shapeY_c, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (Y.shape.HasZeroDims())
                return;

            ctx.backend.LSTM(X, W, R, B, sequenceLens, initialH, initialC, P, Y, Y_h, Y_c, direction, activations, activationAlpha, activationBeta, inputForget, clip, layout);
        }
    }
}
