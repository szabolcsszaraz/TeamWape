using System;
using UnityEngine;

namespace Unity.InferenceEngine.Layers
{
    /// <summary>
    /// Options for the formatting of the box data for `NonMaxSuppression`.
    /// </summary>
    enum CenterPointBox
    {
        /// <summary>
        /// Use TensorFlow box formatting. Box data is [y1, x1, y2, x2] where (y1, x1) and (y2, x2) are the normalized coordinates of any diagonal pair of box corners.
        /// </summary>
        Corners,
        /// <summary>
        /// Use PyTorch box formatting. Box data is [x_center, y_center, width, height].
        /// </summary>
        Center
    }

    /// <summary>
    /// Represents a `NonMaxSuppression` object detection layer. This calculates an output tensor of selected indices of boxes from input `boxes` and `scores` tensors, and bases the indices on the scores and amount of intersection with previously selected boxes.
    /// </summary>
    [Operator(category = "ObjectDetection")]
    [Inputs(names = new[] { "boxes", "scores", "maxOutputBoxesPerClass", "iouThreshold", "scoreThreshold" }, inputCPURead = new[] { 2, 3, 4 })]
    partial class NonMaxSuppression : Layer
    {
        public CenterPointBox centerPointBox;

        internal static PartialTensor InferPartial(PartialTensor boxes, PartialTensor scores, PartialTensor maxOutputBoxesPerClass, PartialTensor iouThreshold, PartialTensor scoreThreshold, CenterPointBox centerPointBox)
        {
            var shape = new DynamicTensorShape(DynamicTensorDim.Unknown, DynamicTensorDim.Int(3));
            return new PartialTensor<int>(shape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var maxOutputBoxesPerClass = ctx.storage.GetInt(inputs[2], defaultValue: 0);
            var iouThreshold = ctx.storage.GetFloat(inputs[3], defaultValue: 0);
            var scoreThreshold = ctx.storage.GetFloat(inputs[4], defaultValue: float.MinValue);
            var boxes = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var scores = ctx.storage.GetTensor(inputs[1]) as Tensor<float>;

            Logger.AssertIsTrue(boxes.shape.rank == 3, "NonMaxSuppression.InputError: box data needs to be rank 3, got {0}", boxes.shape.rank);
            Logger.AssertIsTrue(scores.shape.rank == 3, "NonMaxSuppression.InputError: score data needs to be rank 3, got {0}", scores.shape.rank);
            Logger.AssertIsTrue(boxes.shape[2] == 4, "NonMaxSuppression.InputError: box data needs to have 4 values per box, got {0}", boxes.shape[2]);
            Logger.AssertIsTrue(iouThreshold <= 1f, "NonMaxSuppression.InputError: iou threshold must be lower that 1, got {0}", iouThreshold);
            Logger.AssertIsTrue(iouThreshold >= 0f, "NonMaxSuppression.InputError: iou threshold must be higher that 0, got {0}", iouThreshold);

            var numBatches = scores.shape[0];
            var numClasses = scores.shape[1];
            var numBoxes = scores.shape[2];

            if (maxOutputBoxesPerClass == -1)
                maxOutputBoxesPerClass = numBoxes;
            maxOutputBoxesPerClass = Mathf.Min(numBoxes, maxOutputBoxesPerClass);

            var shapeO = new TensorShape(numBatches * numClasses * maxOutputBoxesPerClass, 3);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], shapeO, DataType.Int, ctx.backend.backendType) as Tensor<int>;
            if (shapeO.HasZeroDims())
                return;

            if (ctx.backend is GPUComputeBackend gpubackend)
                gpubackend.NonMaxSuppression(boxes, scores, O, maxOutputBoxesPerClass, iouThreshold, scoreThreshold, centerPointBox);
            else
                ctx.cpuBackend.NonMaxSuppression(boxes, scores, O, maxOutputBoxesPerClass, iouThreshold, scoreThreshold, centerPointBox);
        }
    }

    /// <summary>
    /// Options for the pooling mode for `RoiAlign`.
    /// </summary>
    enum RoiPoolingMode
    {
        /// <summary>
        /// Use average pooling.
        /// </summary>
        Avg = 0,
        /// <summary>
        /// Use maximum pooling.
        /// </summary>
        Max = 1
    }

    enum RoiCoordinateTransformationMode
    {
        /// <summary>
        /// Omit the pixel shift for the input (use this for a backward-compatible behavior).
        /// </summary>
        OutputHalfPixel = 0,
        /// <summary>
        /// Shift the input coordinates by -0.5 (the recommended behavior).
        /// </summary>
        HalfPixel = 1
    }

    /// <summary>
    /// Represents an `RoiAlign` region of interest alignment layer. This calculates an output tensor by pooling the input tensor across each region of interest given by the `rois` tensor.
    /// </summary>
    [Operator(category = "ObjectDetection")]
    [Inputs(names = new[] { "X", "rois", "batchIndices" })]
    partial class RoiAlign : Layer
    {
        public RoiPoolingMode mode;
        public int outputHeight;
        public int outputWidth;
        public int samplingRatio;
        public float spatialScale;
        public RoiCoordinateTransformationMode coordinateTransformationMode;

        internal static PartialTensor InferPartial(PartialTensor X, PartialTensor rois, PartialTensor batchIndices, RoiPoolingMode mode, int outputHeight, int outputWidth, int samplingRatio, float samplingScale, RoiCoordinateTransformationMode coordinateTransformationMode)
        {
            var shapeX = X.shape;
            var shapeRois = rois.shape;
            var shapeIndices = batchIndices.shape;
            var shapeOut = DynamicTensorShape.DynamicOfRank(4);

            shapeRois.DeclareRank(2);
            Logger.AssertIsFalse(shapeRois[1] != 4, "RoiAlign.ValueError: incorrect number of num_rois, expecting 4");
            shapeOut[0] = shapeRois[0];

            shapeX.DeclareRank(4);
            shapeOut[1] = shapeX[1];

            shapeIndices.DeclareRank(1);
            shapeOut[0] = DynamicTensorDim.MaxDefinedDim(shapeOut[0], shapeIndices[0]);

            shapeOut[2] = DynamicTensorDim.Int(outputHeight);
            shapeOut[3] = DynamicTensorDim.Int(outputWidth);

            return new PartialTensor<float>(shapeOut);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var rois = ctx.storage.GetTensor(inputs[1]) as Tensor<float>;
            var indices = ctx.storage.GetTensor(inputs[2]) as Tensor<int>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], ShapeInference.RoiAlign(X.shape, rois.shape, indices.shape, outputHeight, outputWidth), DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.RoiAlign(X, rois, indices, O, mode, outputHeight, outputWidth, samplingRatio, spatialScale, coordinateTransformationMode);
        }
    }
}
