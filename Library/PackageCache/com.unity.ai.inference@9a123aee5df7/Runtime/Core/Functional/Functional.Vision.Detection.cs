using System;

namespace Unity.InferenceEngine
{
    public static partial class Functional
    {
        /// <summary>
        /// Returns the indexes of the boxes with the highest scores, which pass the intersect-over-union test to other output boxes.
        /// </summary>
        /// <remarks>
        /// This operation performs Non-Maximum Suppression (NMS) to filter overlapping bounding boxes in object detection.
        /// Boxes with IoU (Intersection over Union) greater than `iouThreshold` with higher-scoring boxes are suppressed.
        /// The `boxes` tensor must have rank `2` with shape `[N, 4]` in `(x1, y1, x2, y2)` corner format with normalized coordinates (`0 ≤ x1 &lt; x2 ≤ 1` and `0 ≤ y1 &lt; y2 ≤ 1`).
        /// The `scores` tensor must have rank `1` with shape `[N]`. Promotes `boxes` and `scores` to float type if necessary.
        /// Optionally, boxes with scores below `scoreThreshold` are filtered out before NMS.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var boxes = Functional.Constant(new TensorShape(3, 4), new[] { 0.1f, 0.1f, 0.3f, 0.3f, 0.15f, 0.15f, 0.35f, 0.35f, 0.5f, 0.5f, 0.7f, 0.7f });
        /// var scores = Functional.Constant(new[] { 0.9f, 0.8f, 0.95f });
        /// var result = Functional.NMS(boxes, scores, iouThreshold: 0.5f, scoreThreshold: 0.7f);
        /// // Result: Indices of boxes kept after NMS (e.g., [2, 0])
        /// ]]></code>
        /// </example>
        /// <param name="boxes">The boxes tensor `[N, 4]` with `(x1, y1, x2, y2)` corners format with `0 ≤ x1 &lt; x2 ≤ 1` and `0 ≤ y1 &lt; y2 ≤ 1`.</param>
        /// <param name="scores">The scores tensor `[N]`.</param>
        /// <param name="iouThreshold">The threshold above which to discard overlapping boxes.</param>
        /// <param name="scoreThreshold">(Optional) The threshold of score below which to discard boxes.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor NMS(FunctionalTensor boxes, FunctionalTensor scores, float iouThreshold, float? scoreThreshold = null)
        {
            DeclareRank(boxes, 2);
            DeclareRank(scores, 1);
            boxes = boxes.Float();
            scores = scores.Float();
            return FunctionalLayer.NonMaxSuppression(boxes.Unsqueeze(0), scores.Reshape(new[] { 1, 1, -1 }), Constant(-1), Constant(iouThreshold), scoreThreshold.HasValue ? Constant(scoreThreshold.Value) : null, Layers.CenterPointBox.Corners).Select(1, 2);
        }
    }
}
