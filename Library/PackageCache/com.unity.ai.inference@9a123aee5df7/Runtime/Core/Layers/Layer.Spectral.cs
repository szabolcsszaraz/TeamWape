using System;

namespace Unity.InferenceEngine.Layers
{
    [Operator(category = "Spectral")]
    [Inputs(names = new[] { "size" }, inputCPURead = new[] { 0 })]
    partial class BlackmanWindow : Layer
    {
        public bool periodic;

        internal static PartialTensor InferPartial(PartialTensor size, bool periodic)
        {
            var outShape = DynamicTensorShape.DynamicOfRank(1);
            if (size.isPartiallyKnown)
                outShape[0] = (DynamicTensorDim)size.Get<int>();
            return PartialTensor.Create(DataType.Float, outShape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var size = ctx.storage.GetInt(0);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], new TensorShape(size), DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.BlackmanWindow(O, periodic);
        }
    }

    [Operator(category = "Spectral")]
    [Inputs(names = new[] { "input", "dftLength", "axis", "dftMatrix" }, inputCPURead = new[] { 1, 2 })]
    partial class DFT : Layer
    {
        public bool inverse;
        public bool onesided;

        internal static PartialTensor InferPartial(PartialTensor input, PartialTensor dftLength, PartialTensor axis, PartialTensor dftMatrix, bool inverse, bool onesided)
        {
            var axisTmp = axis as PartialTensor<int>;
            var axisValue = PartialTensorElement<int>.Unknown;
            if (axisTmp != null)
                axisValue = PartialTensorElement<int>.MaxDefinedElement(axisValue, axisTmp.Get<int>());
            else
                axisValue = PartialTensorElement<int>.Value(-2);

            var inputSignalLength = axisValue.isValue && input.shape.hasRank && input.shape.rank > 0 ? (PartialTensorElement<int>)input.shape[axisValue.value] : PartialTensorElement<int>.Unknown;

            var dftMatrixTmp = dftMatrix as PartialTensor<float>;
            var dftMatrixNumTimeColValue = PartialTensorElement<int>.Unknown;
            var dftMatrixNumFreqRowsValue = PartialTensorElement<int>.Unknown;
            if (dftMatrixTmp != null)
            {
                dftMatrixNumTimeColValue = PartialTensorElement<int>.MaxDefinedElement(dftMatrixNumTimeColValue, (PartialTensorElement<int>)dftMatrixTmp.shape[1]);
                dftMatrixNumFreqRowsValue = PartialTensorElement<int>.MaxDefinedElement(dftMatrixNumFreqRowsValue, (PartialTensorElement<int>)dftMatrixTmp.shape[0]);
            }

            var dftLengthTmp = dftLength as PartialTensor<int>;
            var dftLengthValue = PartialTensorElement<int>.Unknown;
            if (dftLengthTmp != null)
                dftLengthValue = PartialTensorElement<int>.MaxDefinedElement(dftLengthValue, dftLengthTmp.Get<int>());
            else
                dftLengthValue = inputSignalLength;

            var shapeO = DynamicTensorShape.DynamicOfRankLike(input.shape);
            // Note here: the DFT matrix needs only min(inputsignal.shape[axis], dftLength) temporal points here
            // (ie frameLength = min(inputsignal.shape[axis], dftLength), cf with STFT)
            var signalFrameLengthToUse = PartialTensorElement<int>.Unknown;
            PartialTensorElement<int> outputXformSignalLength = onesided ? (PartialTensorElement<int>)(((DynamicTensorDim)dftLengthValue).DivideWithRounding(2, roundingDirection: -1) + 1) : dftLengthValue;
            if (axisValue.isValue)
            {
                shapeO = input.shape;
                shapeO[axisValue.value] = (DynamicTensorDim)outputXformSignalLength;
                if (inputSignalLength.isValue && dftLengthValue.isValue) // otherwise signalFrameLengthToUse stays unknown
                    signalFrameLengthToUse = (inputSignalLength > dftLengthValue) ? dftLengthValue : inputSignalLength;
            }
            shapeO[-1] = DynamicTensorDim.Int(2);

            var signalElementSize = input.shape.hasRank && input.shape.rank > 0 ? input.shape[-1] : DynamicTensorDim.Unknown;
            if (signalElementSize.isValue)
            {
                if (signalElementSize == 1)
                {
                    // real signal
                    if (dftMatrixNumTimeColValue.isValue && signalFrameLengthToUse.isValue)
                        Logger.AssertIsTrue(dftMatrixNumTimeColValue == signalFrameLengthToUse, "DFT.InputError: dftMatrix num of columns should match input framelength to use ({0}) for real signals", signalFrameLengthToUse);

                    if (dftMatrixNumFreqRowsValue.isValue && outputXformSignalLength.isValue)
                        Logger.AssertIsTrue(dftMatrixNumFreqRowsValue.value == outputXformSignalLength.value * 2, "DFT.InputError: dftMatrix num of rows should be twice the expected number of output points of the transform (num of output pts = {0}) for real signals", outputXformSignalLength.value);
                }
                else if (signalElementSize == 2)
                {
                    // complex signal
                    if (dftMatrixNumTimeColValue.isValue && signalFrameLengthToUse.isValue)
                        Logger.AssertIsTrue(dftMatrixNumTimeColValue.value == (signalFrameLengthToUse.value * 2), "DFT.InputError: dftMatrixnum of columns should be twice input framelength to use ({0}) for complex signals", signalFrameLengthToUse);

                    if (dftMatrixNumFreqRowsValue.isValue && outputXformSignalLength.isValue)
                        Logger.AssertIsTrue(dftMatrixNumFreqRowsValue == outputXformSignalLength, "DFT.InputError: dftMatrixnum of rows should match the expected number of output points of the transform ({0}) for complex signals", outputXformSignalLength.value);
                }
                else
                    Logger.AssertIsTrue(false, "DFT.InputError: input tensor innermost axis should have size 1 (for real signal) or 2 (for complex signal)");
            }

            return new PartialTensor<float>(shapeO);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var signalTensor = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            int axis = ctx.storage.GetInts(inputs[2])[0];
            int dftLength = inputs[1] >= 0 ? ctx.storage.GetInts(inputs[1])[0] : signalTensor.shape[axis];
            var dftMatrix = ctx.storage.GetTensor(inputs[3]) as Tensor<float>;
            bool signalIsReal = signalTensor.shape[-1] == 1;

            int signalFrameLengthToUse = Math.Min(dftLength, signalTensor.shape[axis]);
            int outputXformSignalLength = onesided ? dftLength / 2 + 1 : dftLength;

            Logger.AssertIsTrue(signalTensor.shape[-1] <= 2, "Layer.Signal.DFT: incorrect input.shape[-1] size ({0}): should be 1 or 2 (for complex signal)", signalTensor.shape[-1]);

            if (dftMatrix != null)
            {
                bool ret;
                if (dftMatrix.shape.rank > 1)
                {
                    bool testNumRows = signalIsReal ? outputXformSignalLength * 2 == dftMatrix.shape[0] : outputXformSignalLength == dftMatrix.shape[0];
                    bool testNumCols = signalIsReal ? signalFrameLengthToUse == dftMatrix.shape[1] : signalFrameLengthToUse * 2 == dftMatrix.shape[1];
                    ret = testNumRows && testNumCols;
                }
                else
                {
                    ret = dftMatrix.shape.rank == 1 && dftMatrix.shape.length == outputXformSignalLength * signalFrameLengthToUse * 2;
                }
                Logger.AssertIsTrue(ret, "Layer.Signal.DFT: incorrect dftMatrix shape {0} vs output transform signal length {1}, input framelength to use {2} and given signal point size",
                    dftMatrix.shape, outputXformSignalLength, signalFrameLengthToUse);
            }

            var shapeO = new TensorShape(signalTensor.shape);
            shapeO[axis] = outputXformSignalLength;
            shapeO[-1] = 2;

            var O = ctx.storage.AllocateTensorAndStore(outputs[0], shapeO, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;

            ctx.backend.DFT(signalTensor, O, dftLength, axis, dftMatrix: dftMatrix, inverse, onesided);
        }
    }

    [Operator(category = "Spectral")]
    [Inputs(names = new[] { "size" }, inputCPURead = new[] { 0 })]
    partial class HammingWindow : Layer
    {
        public bool periodic;

        internal static PartialTensor InferPartial(PartialTensor size, bool periodic)
        {
            var outShape = DynamicTensorShape.DynamicOfRank(1);
            if (size.isPartiallyKnown)
                outShape[0] = (DynamicTensorDim)size.Get<int>();
            return PartialTensor.Create(DataType.Float, outShape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var size = ctx.storage.GetInt(0);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], new TensorShape(size), DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.HammingWindow(O, periodic);
        }
    }

    [Operator(category = "Spectral")]
    [Inputs(names = new[] { "size" }, inputCPURead = new[] { 0 })]
    partial class HannWindow : Layer
    {
        public bool periodic;

        internal static PartialTensor InferPartial(PartialTensor size, bool periodic)
        {
            var outShape = DynamicTensorShape.DynamicOfRank(1);
            if (size.isPartiallyKnown)
                outShape[0] = (DynamicTensorDim)size.Get<int>();
            return PartialTensor.Create(DataType.Float, outShape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var size = ctx.storage.GetInt(0);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], new TensorShape(size), DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.HannWindow(O, periodic);
        }
    }

    [Operator(category = "Spectral")]
    [Inputs(names = new[] { "numMelBins", "dftLength", "sampleRate", "lowerEdgeHertz", "upperEdgeHertz" }, inputCPURead = new[] { 0, 1, 2, 3, 4 })]
    partial class MelWeightMatrix : Layer
    {
        internal static PartialTensor InferPartial(PartialTensor numMelBins, PartialTensor dftLength, PartialTensor sampleRate, PartialTensor lowerEdgeHertz, PartialTensor upperEdgeHertz)
        {
            var outShape = DynamicTensorShape.DynamicOfRank(2);
            if (dftLength.isPartiallyKnown)
                outShape[0] = ((DynamicTensorDim)dftLength.Get<int>()).DivideWithRounding(2, -1) + 1;
            if (numMelBins.isPartiallyKnown)
                outShape[1] = (DynamicTensorDim)numMelBins.Get<int>();
            return PartialTensor.Create(DataType.Float, outShape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var numMelBins = ctx.storage.GetInt(0);
            var dftLength = ctx.storage.GetInt(1);
            var sampleRate = ctx.storage.GetInt(2);
            var lowerEdgeHertz = ctx.storage.GetFloat(3);
            var upperEdgeHertz = ctx.storage.GetFloat(4);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], new TensorShape(dftLength / 2 + 1, numMelBins), DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.MelWeightMatrix(O, dftLength, sampleRate, lowerEdgeHertz, upperEdgeHertz);
        }
    }

    [Operator(category = "Spectral")]
    [Inputs(names = new[] { "signal", "frameStep", "window", "frameLength", "windowedDFTMatrix" }, inputCPURead = new[] { 1, 3 })]
    partial class STFT : Layer
    {
        public bool onesided;

        internal static PartialTensor InferPartial(PartialTensor signal, PartialTensor frameStep, PartialTensor window, PartialTensor frameLength, PartialTensor windowedDFTMatrix, bool onesided)
        {
            var frameStepTmp = frameStep as PartialTensor<int>;
            var frameStepVal = PartialTensorElement<int>.Unknown;
            if (frameStepTmp != null)
                frameStepVal = PartialTensorElement<int>.MaxDefinedElement(frameStepVal, frameStepTmp.Get<int>());

            var frameLengthValue = PartialTensorElement<int>.Unknown;
            var windowLengthValue = PartialTensorElement<int>.Unknown;
            var windowedDFTMatrixFrameLengthValue = PartialTensorElement<int>.Unknown;
            var windowedDFTMatrixNumFreqRowsValue = PartialTensorElement<int>.Unknown;

            var windowTmp = window as PartialTensor<float>;
            var windowedDFTMatrixTmp = windowedDFTMatrix as PartialTensor<float>;

            var frameLengthTmp = frameLength as PartialTensor<int>;
            if (windowTmp != null)
                windowLengthValue = PartialTensorElement<int>.MaxDefinedElement(windowLengthValue, (PartialTensorElement<int>)windowTmp.shape[0]);
            if (frameLengthTmp != null)
                frameLengthValue = PartialTensorElement<int>.MaxDefinedElement(frameLengthValue, frameLengthTmp.Get<int>());
            if (windowedDFTMatrixTmp != null)
            {
                windowedDFTMatrixFrameLengthValue = PartialTensorElement<int>.MaxDefinedElement(windowedDFTMatrixFrameLengthValue, (PartialTensorElement<int>)windowedDFTMatrixTmp.shape[1]);
                windowedDFTMatrixNumFreqRowsValue = PartialTensorElement<int>.MaxDefinedElement(windowedDFTMatrixNumFreqRowsValue, (PartialTensorElement<int>)windowedDFTMatrixTmp.shape[0]);
            }

            if (frameLengthValue.isValue && windowLengthValue.isValue)
                Logger.AssertIsTrue(frameLengthValue.value == windowLengthValue.value, "STFT.InputError: frameLength should equal length of window");

            frameLengthValue = PartialTensorElement<int>.MaxDefinedElement(frameLengthValue, windowLengthValue);

            var shapeO = DynamicTensorShape.DynamicOfRank(4);
            shapeO[0] = signal.shape.hasRank && signal.shape.rank > 0 ? signal.shape[0] : DynamicTensorDim.Unknown;
            shapeO[1] = signal.shape.hasRank && signal.shape.rank > 0 ? (DynamicTensorDim)(((PartialTensorElement<int>)signal.shape[1] - frameLengthValue + frameStepVal) / frameStepVal) : DynamicTensorDim.Unknown;
            shapeO[2] = onesided ? ((DynamicTensorDim)frameLengthValue).DivideWithRounding(2, roundingDirection: -1) + 1 : (DynamicTensorDim)frameLengthValue;
            shapeO[3] = DynamicTensorDim.Int(2);

            if (frameLengthValue.isValue && windowedDFTMatrixFrameLengthValue.isValue && signal.shape.hasRank)
            {
                Logger.AssertIsTrue(signal.shape.rank == 2 || signal.shape.rank == 3, "STFT.InputError: signal rank should be 2 or 3");
                if (signal.shape.rank == 2 || (signal.shape.rank == 3 && signal.shape[2] == DynamicTensorDim.One))
                {
                    // real signal
                    Logger.AssertIsTrue(windowedDFTMatrixFrameLengthValue.value == frameLengthValue.value, "STFT.InputError: windowedDFTMatrix num of columns should match frameLength for real signals");
                    if (windowedDFTMatrixNumFreqRowsValue.isValue)
                        Logger.AssertIsTrue(windowedDFTMatrixNumFreqRowsValue.value == shapeO[2] * 2, "STFT.InputError: windowedDFTMatrix num of rows should be twice the expected number of ouput freq ({0}) for real signals", shapeO[2]);
                }
                else if (signal.shape.rank == 3 && signal.shape[2] == DynamicTensorDim.Int(2))
                {
                    // complex signal
                    Logger.AssertIsTrue(windowedDFTMatrixFrameLengthValue.value == (frameLengthValue.value * 2), "STFT.InputError: windowedDFTMatrix num of columns should be twice the frameLength for complex signals");
                    if (windowedDFTMatrixNumFreqRowsValue.isValue)
                        Logger.AssertIsTrue(windowedDFTMatrixNumFreqRowsValue.value == shapeO[2], "STFT.InputError: windowedDFTMatrix num of rows should match the expected number of ouput freq ({0}) for complex signals", shapeO[2]);
                }
            }

            return new PartialTensor<float>(shapeO);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var signal = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var signalShape = signal.shape;
            if (signal.shape.rank == 2)
                signal.shape = new TensorShape(signalShape[0], signalShape[1], 1); // this is not in the onnx spec but is supported by ORT
            bool signalIsReal = signal.shape[2] == 1;
            var frameStep = ctx.storage.GetInts(inputs[1])[0];
            var window = ctx.storage.GetTensor(inputs[2]) as Tensor<float>;
            var frameLength = inputs[3] >= 0 ? ctx.storage.GetInts(inputs[3])[0] : window.shape[0];
            var windowedDFTMatrix = ctx.storage.GetTensor(inputs[4]) as Tensor<float>;

            Logger.AssertIsTrue(signal.shape[-1] <= 2, "Layer.Signal.STFT: incorrect signal.shape[-1] size ({0}): should be 1 or 2 (for complex signal)", signal.shape[-1]);

            if (window != null)
                Logger.AssertIsTrue(frameLength == window.shape[0], "Layer.Signal.STFT: incorrect window size ({0}) vs specified frameLength, {1}", window.shape[0], frameLength);

            if (windowedDFTMatrix != null)
            {
                bool ret;
                if (windowedDFTMatrix.shape.rank > 1)
                {
                    bool testNumRows = signalIsReal ? frameLength * 2 == windowedDFTMatrix.shape[0] : frameLength == windowedDFTMatrix.shape[0];
                    bool testNumCols = signalIsReal ? frameLength == windowedDFTMatrix.shape[1] : frameLength * 2 == windowedDFTMatrix.shape[1];
                    ret = testNumRows && testNumCols;
                }
                else
                {
                    ret = windowedDFTMatrix.shape.rank == 1 && windowedDFTMatrix.shape.length == frameLength * frameLength * 2;
                }
                Logger.AssertIsTrue(ret, "Layer.Signal.STFT: incorrect windowedDFTMatrix shape {0} vs frameLength {1} and signal point size {2}", windowedDFTMatrix.shape, frameLength, signal.shape[-1]);
            }

            var shape = new TensorShape(signal.shape[0], (signal.shape[1] - frameLength + frameStep) / frameStep, onesided ? frameLength / 2 + 1 : frameLength, 2);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (!O.shape.HasZeroDims())
                ctx.backend.STFT(signal, window, O, frameStep, frameLength, windowedDFTMatrix: windowedDFTMatrix, onesided: onesided);

            signal.shape = signalShape; // reset shape
        }
    }
}
