using System;
using UnityEngine;

namespace Unity.InferenceEngine
{
    public static partial class Functional
    {
        /// <summary>
        /// Returns the elements of the `input` tensor rearranged from a `(∗,C×r^2,H,W)` tensor to a `(∗,C,H×r,W×r)` tensor where `r` is the upscale factor.
        /// </summary>
        /// <remarks>
        /// This operation rearranges elements in a tensor for upsampling.
        /// Pixel shuffle converts depth (channels) into spatial dimensions by rearranging blocks of pixels from the channel dimension to spatial dimensions.
        /// The operation transforms shape `[batch, C×r², H, W]` to `[batch, C, H×r, W×r]` where `r` is the upscale factor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 4, 2, 2), new float[16]); // Shape: [1, 4, 2, 2]
        /// var result = Functional.PixelShuffle(input, upscaleFactor: 2);
        /// // Result shape: [1, 1, 4, 4] (upscaled by factor of 2)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="upscaleFactor">The upscale factor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor PixelShuffle(FunctionalTensor input, int upscaleFactor)
        {
            return FunctionalLayer.DepthToSpace(input, upscaleFactor, Layers.DepthToSpaceMode.DepthColumnRow);
        }

        /// <summary>
        /// Returns the elements of the `input` tensor rearranged from a `(∗,C,H×r,W×r)` tensor to a `(∗,C×r^2,H,W)` tensor where `r` is the downscale factor.
        /// </summary>
        /// <remarks>
        /// This operation is the inverse of <see cref="PixelShuffle"/>, rearranging spatial dimensions into the channel dimension.
        /// Pixel unshuffle converts spatial information into depth by moving blocks of pixels from spatial dimensions to the channel dimension.
        /// The operation transforms shape `[batch, C, H×r, W×r]` to `[batch, C×r², H, W]` where `r` is the downscale factor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 1, 4, 4), new float[16]); // Shape: [1, 1, 4, 4]
        /// var result = Functional.PixelUnshuffle(input, downscaleFactor: 2);
        /// // Result shape: [1, 4, 2, 2] (downscaled by factor of 2)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="downscaleFactor">The downscale factor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor PixelUnshuffle(FunctionalTensor input, int downscaleFactor)
        {
            return FunctionalLayer.SpaceToDepth(input, downscaleFactor);
        }

        /// <summary>
        /// Returns the `input` tensor with the spatial dimensions downsampled or upsampled to a size or by a scale factor.
        /// </summary>
        /// <remarks>
        /// This operation resizes the spatial dimensions of the `input` tensor using interpolation.
        /// You must specify either `size` or `scaleFactor` (but not both).
        /// Promotes `input` to float type if necessary.
        /// The available modes are `nearest` (nearest neighbor) and `linear` (bilinear for 2D, trilinear for 3D).
        /// Applies the interpolation starting from dimension 2 onward (preserving batch and channel dimensions).
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 1, 4, 4), new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f, 11.0f, 12.0f, 13.0f, 14.0f, 15.0f, 16.0f }); // Shape: [1, 1, 4, 4]
        /// var result = Functional.Interpolate(input, size: new[] { 8, 8 }, mode: "linear");
        /// // Result shape: [1, 1, 8, 8] (upsampled to 8x8) with values:
        /// [[[[1, 1.25, 1.75, 2.25, 2.75, 3.25, 3.75, 4], [2, 2.25, 2.75, 3.25, 3.75, 4.25, 4.75, 5], [4, 4.25, 4.75, 5.25, 5.75, 6.25, 6.75, 7], [6, 6.25, 6.75, 7.25, 7.75, 8.25, 8.75, 9], [8, 8.25, 8.75, 9.25, 9.75, 10.25, 10.75, 11], [10, 10.25, 10.75, 11.25, 11.75, 12.25, 12.75, 13], [12, 12.25, 12.75, 13.25, 13.75, 14.25, 14.75, 15], [13, 13.25, 13.75, 14.25, 14.75, 15.25, 15.75, 16]]]]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="size">The optional output size.</param>
        /// <param name="scaleFactor">The optional output scale factors.</param>
        /// <param name="mode">The mode used for interpolating. The options are: `nearest` or `linear` (bilinear for 2D, trilinear for 3D). `bicubic` maps to `linear`.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Interpolate(FunctionalTensor input, int[] size = null, float[] scaleFactor = null, string mode = "nearest")
        {
            // TODO add recompute_scale_factor, antialias, single value size
            input = input.Float();
            var interpolationMode = mode switch
            {
                "nearest" => Layers.InterpolationMode.Nearest,
                "linear" => Layers.InterpolationMode.Linear,
                "bicubic" => Layers.InterpolationMode.Linear,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };
            var numAxes = size?.Length ?? scaleFactor.Length;
            var axes = new int[numAxes];
            for (var i = 0; i < numAxes; i++)
                axes[i] = 2 + i;

            if (size != null)
                return FunctionalLayer.Resize(input, Constant(size), Layers.ScaleMode.Sizes, Layers.CoordTransformMode.PytorchHalfPixel, interpolationMode, Layers.NearestMode.RoundPreferFloor, axes);

            return FunctionalLayer.Resize(input, Constant(scaleFactor), Layers.ScaleMode.Scales, Layers.CoordTransformMode.PytorchHalfPixel, interpolationMode, Layers.NearestMode.RoundPreferFloor, axes);
        }

        /// <summary>
        /// Returns the `input` tensor sampled at coordinates given by the `grid` tensor.
        /// </summary>
        /// <remarks>
        /// This operation samples the `input` tensor at locations specified by a grid of coordinates.
        /// The `grid` tensor contains normalized coordinates in the range `[-1, 1]` where `(-1, -1)` is the top-left corner and `(1, 1)` is the bottom-right corner.
        /// Promotes `input` and `grid` to float type if necessary.
        /// Available modes are `nearest` or `bilinear`.
        /// Padding modes: `zeros` (out-of-bounds samples are `0`), `border` (clamp to edge), `reflection` (reflect at boundary).
        /// When `alignCorners` is `true`, extreme grid values `[-1, 1]` map to pixel centers. When `false`, they map to pixel edges.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new TensorShape(1, 1, 4, 4), new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f, 11.0f, 12.0f, 13.0f, 14.0f, 15.0f, 16.0f }); // Shape: [1, 1, 4, 4]
        /// var grid = Functional.Constant(new TensorShape(1, 2, 2, 2), new float[] { -0.8f, -0.2f, 0.2f, 0.8f, -1.0f, 1.0f, 0.0f, 0.0f }); // Sampling coordinates
        /// var result = Functional.GridSample(input, grid, mode: "bilinear", paddingMode: "zeros");
        /// // Result shape: [1, 1, 2, 2] (sampled at grid locations) with values [[[[4.86, 13.41], [3.25, 8.5]]]]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="grid">The grid tensor containing the spatial coordinates per output pixel.</param>
        /// <param name="mode">The mode used for interpolating. The options are: `nearest` or `bilinear`. `bicubic` maps to `bilinear`.</param>
        /// <param name="paddingMode">The mode to use for sampling out-of-bounds coordinates. The options are: `zeros`, `border`, or `reflection`.</param>
        /// <param name="alignCorners">Whether to map the extreme values in the coordinates `-1` and `1` to the center of the corner pixels rather than the outer edges.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor GridSample(FunctionalTensor input, FunctionalTensor grid, string mode = "bilinear", string paddingMode = "zeros", bool alignCorners = false)
        {
            input = input.Float();
            grid = grid.Float();
            var interpolationMode = mode switch
            {
                "nearest" => Layers.InterpolationMode.Nearest,
                "bilinear" => Layers.InterpolationMode.Linear,
                "bicubic" => Layers.InterpolationMode.Linear,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };
            var padMode = paddingMode switch
            {
                "zeros" => Layers.PaddingMode.Zeros,
                "border" => Layers.PaddingMode.Border,
                "reflection" => Layers.PaddingMode.Reflection,
                _ => throw new ArgumentOutOfRangeException(nameof(paddingMode), paddingMode, null)
            };
            return FunctionalLayer.GridSample(input, grid, interpolationMode, padMode, alignCorners);
        }
    }
}
