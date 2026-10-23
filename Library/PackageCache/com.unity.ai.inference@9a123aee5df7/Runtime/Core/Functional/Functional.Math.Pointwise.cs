using System;
using UnityEngine;

namespace Unity.InferenceEngine
{
    public static partial class Functional
    {
        /// <summary>
        /// Returns `|input|` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the absolute value of each element in the `input` tensor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -2.5f, -1.0f, 0.0f, 1.5f, 3.0f });
        /// var result = Functional.Abs(input);
        /// // Result: [2.5, 1.0, 0.0, 1.5, 3.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Abs(FunctionalTensor input)
        {
            return FunctionalLayer.Abs(input);
        }

        /// <summary>
        /// Returns `acos(input)` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the inverse cosine (arccosine) of each element in the `input` tensor.
        /// Mathematically defined for input values in `[-1, 1]`. Values outside this range will produce undefined results.
        /// The output is in radians in the range `[0, π]`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -1.0f, 0.0f, 1.0f });
        /// var result = Functional.Acos(input);
        /// // Result: [3.14159, 1.5708, 0.0] (approximately π, π/2, 0)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Acos(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Acos(input);
        }

        /// <summary>
        /// Returns `acosh(input)` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the inverse hyperbolic cosine of each element in the `input` tensor.
        /// Mathematically defined for input values in `[1, ∞)`. Values outside this range will produce undefined results.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1.0f, 2.0f, 3.0f });
        /// var result = Functional.Acosh(input);
        /// // Result: [0.0, 1.317, 1.763] (approximately)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Acosh(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Acosh(input);
        }

        /// <summary>
        /// Returns `input + other` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation performs element-wise addition of two tensors.
        /// Promotes `input` and `other` to a compatible data type if necessary.
        /// The tensors are broadcast to a common shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new[] { 1.0f, 2.0f, 3.0f });
        /// var b = Functional.Constant(new[] { 4.0f, 5.0f, 6.0f });
        /// var result = Functional.Add(a, b);
        /// // Result: [5.0, 7.0, 9.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input tensor.</param>
        /// <param name="other">The second input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Add(FunctionalTensor input, FunctionalTensor other)
        {
            (input, other) = PromoteTypes(input, other);
            return FunctionalLayer.Add(input, other);
        }

        /// <summary>
        /// Returns `Atan2(input, other)` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the arctangent of `input/other`, taking into account the signs of both arguments to determine the quadrant.
        /// The output is in radians in the range `[-π, π]`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var y = Functional.Constant(new[] { 1.0f, -1.0f, 0.0f });
        /// var x = Functional.Constant(new[] { 1.0f, 1.0f, -1.0f });
        /// var result = Functional.Atan2(y, x);
        /// // Result: [0.785, -0.785, 3.14159] (approximately π/4, -π/4, π)
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input tensor.</param>
        /// <param name="other">The second input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Atan2(FunctionalTensor input, FunctionalTensor other)
        {
            input = input.Float();
            other = other.Float();
            return FunctionalLayer.Atan2(input, other);
        }

        /// <summary>
        /// Returns `asin(input)` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the inverse sine (arcsine) of each element in the `input` tensor.
        /// Mathematically defined for input values in `[-1, 1]`. Values outside this range will produce undefined results.
        /// The output is in radians in the range `[-π/2, π/2]`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -1.0f, 0.0f, 1.0f });
        /// var result = Functional.Asin(input);
        /// // Result: [-1.5708, 0.0, 1.5708] (approximately -π/2, 0, π/2)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Asin(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Asin(input);
        }

        /// <summary>
        /// Returns `asinh(input)` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the inverse hyperbolic sine of each element in the `input` tensor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -1.0f, 0.0f, 1.0f });
        /// var result = Functional.Asinh(input);
        /// // Result: [-0.881, 0.0, 0.881] (approximately)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Asinh(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Asinh(input);
        }

        /// <summary>
        /// Returns `atan(input)` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the inverse tangent (arctangent) of each element in the `input` tensor.
        /// The output is in radians in the range `[-π/2, π/2]`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -1.0f, 0.0f, 1.0f });
        /// var result = Functional.Atan(input);
        /// // Result: [-0.785, 0.0, 0.785] (approximately -π/4, 0, π/4)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Atan(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Atan(input);
        }

        /// <summary>
        /// Returns `atanh(input)` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the inverse hyperbolic tangent of each element in the `input` tensor.
        /// Mathematically defined for input values in `(-1, 1)`. Values outside this range will produce undefined results.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -0.5f, 0.0f, 0.5f });
        /// var result = Functional.Atanh(input);
        /// // Result: [-0.549, 0.0, 0.549] (approximately)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Atanh(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Atanh(input);
        }

        /// <summary>
        /// Returns `⌈input⌉` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the ceiling (smallest integer greater than or equal to) of each element in the `input` tensor.
        /// If `input` is already an integer tensor, it is returned unchanged.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -1.7f, -0.3f, 0.5f, 1.2f });
        /// var result = Functional.Ceil(input);
        /// // Result: [-1.0, 0.0, 1.0, 2.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Ceil(FunctionalTensor input)
        {
            if (input.dataType == DataType.Int)
                return input;
            return FunctionalLayer.Ceil(input);
        }

        /// <summary>
        /// Returns `input` clamped to the range `[min, max]` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation clamps each element in the `input` tensor to the specified range.
        /// Values below `min` are set to `min`, and values above `max` are set to `max`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -5.0f, 0.0f, 5.0f, 10.0f });
        /// var result = Functional.Clamp(input, 0.0f, 8.0f);
        /// // Result: [0.0, 0.0, 5.0, 8.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="min">The min value.</param>
        /// <param name="max">The max value.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Clamp(FunctionalTensor input, float min, float max)
        {
            input = input.Float();
            return FunctionalLayer.Clip(input, Constant(min), Constant(max));
        }

        /// <summary>
        /// Returns `input` clamped to the range `[min, max]` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation clamps each element in the `input` tensor to the specified range.
        /// Values below `min` are set to `min`, and values above `max` are set to `max`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -5, 0, 5, 10 });
        /// var result = Functional.Clamp(input, 0, 8);
        /// // Result: [0, 0, 5, 8]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <param name="min">The min value.</param>
        /// <param name="max">The max value.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Clamp(FunctionalTensor input, int min, int max)
        {
            if (input.dataType == DataType.Float)
                return Clamp(input, (float)min, max);
            return FunctionalLayer.Clip(input, Constant(min), Constant(max));
        }

        /// <summary>
        /// Returns `cos(input)` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the cosine of each element in the `input` tensor.
        /// Input is expected in radians.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 0.0f, 1.5708f, 3.14159f });
        /// var result = Functional.Cos(input);
        /// // Result: [1.0, 0.0, -1.0] (approximately cos(0), cos(π/2), cos(π))
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Cos(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Cos(input);
        }

        /// <summary>
        /// Returns `cosh(input)` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the hyperbolic cosine of each element in the `input` tensor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -1.0f, 0.0f, 1.0f });
        /// var result = Functional.Cosh(input);
        /// // Result: [1.543, 1.0, 1.543] (approximately)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Cosh(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Cosh(input);
        }

        /// <summary>
        /// Returns the `input` values converted from angles in degrees to radians element-wise.
        /// </summary>
        /// <remarks>
        /// This operation converts angles from degrees to radians by multiplying each element by `π/180`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var degrees = Functional.Constant(new[] { 0.0f, 90.0f, 180.0f, 360.0f });
        /// var radians = Functional.Deg2Rad(degrees);
        /// // Result: [0.0, 1.571, 3.142, 6.283] (approximately 0, π/2, π, 2π)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Deg2Rad(FunctionalTensor input)
        {
            return Mathf.Deg2Rad * input;
        }

        /// <summary>
        /// Returns `input / other` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation performs element-wise division of two tensors.
        /// Promotes `input` and `other` to float type if necessary.
        /// The tensors are broadcast to a common shape.
        /// The result isn't rounded.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new[] { 10.0f, 15.0f, 20.0f });
        /// var b = Functional.Constant(new[] { 2.0f, 3.0f, 4.0f });
        /// var result = Functional.Div(a, b);
        /// // Result: [5.0, 5.0, 5.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input tensor.</param>
        /// <param name="other">The second input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Div(FunctionalTensor input, FunctionalTensor other)
        {
            return Div(input, other, roundingMode: null);
        }

        /// <summary>
        /// Returns `input / other` element-wise with rounding mode.
        /// </summary>
        /// <remarks>
        /// This operation performs element-wise division with optional rounding.
        /// Promotes `input` and `other` to float type if necessary.
        /// The tensors are broadcast to a common shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new[] { 7, 8, 9 });
        /// var b = Functional.Constant(new[] { 2, 2, 2 });
        /// var resultDefault = Functional.Div(a, b, null);
        /// // Result: [3.5, 4.0, 4.5]
        /// var resultTrunc = Functional.Div(a, b, "trunc");
        /// // Result: [3, 4, 4]
        /// var resultFloor = Functional.Div(a, b, "floor");
        /// // Result: [3, 4, 4]
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input tensor.</param>
        /// <param name="other">The second input tensor.</param>
        /// <param name="roundingMode">The type of rounding applied to the result:
        /// <list type="bullet"><item><description><c>null</c>: Default behavior. Performs no rounding.</description></item>
        /// <item><description><c>trunc</c>: Rounds the results of the division towards zero.</description></item>
        /// <item><description><c>floor</c>: Rounds the results of the division down.</description></item></list></param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Div(FunctionalTensor input, FunctionalTensor other, string roundingMode)
        {
            switch (roundingMode)
            {
                case null:
                {
                    input = input.Float();
                    other = other.Float();
                    return FunctionalLayer.Div(input, other);
                }
                case "trunc":
                {
                    (input, other) = PromoteTypes(input, other);
                    return FunctionalLayer.TruncDiv(input, other);
                }
                case "floor":
                {
                    (input, other) = PromoteTypes(input, other);
                    return FunctionalLayer.FloorDiv(input, other);
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Returns the error function of `input` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the Gauss error function.
        /// The output is in the range `(-1, 1)`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -2.0f, -1.0f, 0.0f, 1.0f, 2.0f });
        /// var result = Functional.Erf(input);
        /// // Result: [-0.995, -0.843, 0.0, 0.843, 0.995] (approximately)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Erf(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Erf(input);
        }

        /// <summary>
        /// Returns `e^input` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the exponential (e raised to the power of each element) of the `input` tensor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 0.0f, 1.0f, 2.0f });
        /// var result = Functional.Exp(input);
        /// // Result: [1.0, 2.718, 7.389] (approximately e^0, e^1, e^2)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Exp(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Exp(input);
        }

        /// <summary>
        /// Returns `e^input - 1` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes `e^x - 1` for each element. This function provides better numerical precision than computing `exp(x) - 1` for small values of `x`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 0.0f, 0.5f, 1.0f });
        /// var result = Functional.Expm1(input);
        /// // Result: [0.0, 0.649, 1.718] (approximately e^0-1, e^0.5-1, e^1-1)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Expm1(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Expm1(input);
        }

        /// <summary>
        /// Returns `input^exponent` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation raises the first input to the power of the second input.
        /// Promotes `input` and `exponent` to float type if necessary.
        /// The tensors are broadcast to a common shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var base = Functional.Constant(new[] { 2, 3, 4 });
        /// var exponent = Functional.Constant(new[] { 3, 2, 1 });
        /// var result = Functional.FloatPower(base, exponent);
        /// // Result: [8.0, 9.0, 4.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input tensor.</param>
        /// <param name="exponent">The second input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor FloatPower(FunctionalTensor input, FunctionalTensor exponent)
        {
            input = input.Float();
            exponent = exponent.Float();
            return FunctionalLayer.Pow(input, exponent);
        }

        /// <summary>
        /// Returns `⌊input⌋` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the floor (largest integer less than or equal to) of each element in the `input` tensor.
        /// If `input` is already an integer tensor, it is returned unchanged.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -1.7f, -0.3f, 0.5f, 1.2f });
        /// var result = Functional.Floor(input);
        /// // Result: [-2.0, -1.0, 0.0, 1.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Floor(FunctionalTensor input)
        {
            if (input.dataType == DataType.Int)
                return input;
            return FunctionalLayer.Floor(input);
        }

        /// <summary>
        /// Returns `⌊input/other⌋` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation performs element-wise division and floors the result.
        /// Promotes `input` and `other` to a compatible data type if necessary.
        /// The tensors are broadcast to a common shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new[] { 7.0f, 8.0f, 9.0f });
        /// var b = Functional.Constant(new[] { 2.0f, 2.0f, 2.0f });
        /// var result = Functional.FloorDivide(a, b);
        /// // Result: [3.0, 4.0, 4.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input tensor.</param>
        /// <param name="other">The second input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor FloorDivide(FunctionalTensor input, FunctionalTensor other)
        {
            return Div(input, other, "floor");
        }

        /// <summary>
        /// Returns `input % other` element-wise. The sign of the output is the same as that of the dividend.
        /// </summary>
        /// <remarks>
        /// This operation computes the modulo where the sign of the result matches the sign of the dividend (`input`).
        /// Promotes `input` and `other` to a compatible data type if necessary.
        /// The tensors are broadcast to a common shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new[] { 7.0f, -7.0f, 7.0f, -7.0f });
        /// var b = Functional.Constant(new[] { 3.0f, 3.0f, -3.0f, -3.0f });
        /// var result = Functional.FMod(a, b);
        /// // Result: [1.0, -1.0, 1.0, -1.0] (sign follows dividend)
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input tensor.</param>
        /// <param name="other">The second input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor FMod(FunctionalTensor input, FunctionalTensor other)
        {
            (input, other) = PromoteTypes(input, other);
            return FunctionalLayer.Mod(input, other, true);
        }

        /// <summary>
        /// Returns the fractional part of the `input` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the fractional part by subtracting the truncated integer part from `input`.
        /// For integer tensors, returns a tensor of zeros.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -1.7f, -0.3f, 0.5f, 1.2f, 2.9f });
        /// var result = Functional.Frac(input);
        /// // Result: [-0.7, -0.3, 0.5, 0.2, 0.9]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Frac(FunctionalTensor input)
        {
            if (input.dataType == DataType.Int)
                return ZerosLike(input);
            // TODO add frac to backend and layers
            return input - Trunc(input);
        }

        /// <summary>
        /// Returns the linear interpolation `input + weight * (end - input)` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation performs linear interpolation between two tensors based on a scalar `weight`.
        /// When `weight` is `0`, returns `input`; when `weight` is `1`, returns `end`. Values outside `[0, 1]` extrapolate.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var start = Functional.Constant(new[] { 0.0f, 10.0f, 20.0f });
        /// var end = Functional.Constant(new[] { 10.0f, 20.0f, 30.0f });
        /// var result = Functional.Lerp(start, end, 0.5f);
        /// // Result: [5.0, 15.0, 25.0] (midpoint between start and end)
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input tensor.</param>
        /// <param name="end">The second input tensor.</param>
        /// <param name="weight">The interpolation weight.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Lerp(FunctionalTensor input, FunctionalTensor end, float weight)
        {
            // TODO weight tensor
            // TODO add to layers and backend
            return input + weight * (end - input);
        }

        /// <summary>
        /// Returns `log(input)` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the natural logarithm (base e) of each element in the `input` tensor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1.0f, 2.718f, 7.389f });
        /// var result = Functional.Log(input);
        /// // Result: [0.0, 1.0, 2.0] (approximately ln(1), ln(e), ln(e^2))
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Log(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Log(input);
        }

        /// <summary>
        /// Returns `log10(input)` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the base-10 logarithm of each element in the `input` tensor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1.0f, 10.0f, 100.0f });
        /// var result = Functional.Log10(input);
        /// // Result: [0.0, 1.0, 2.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Log10(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Log10(input);
        }

        /// <summary>
        /// Returns `log(input + 1)` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the natural logarithm of `(1 + x)` for each element. This function provides better numerical precision than computing `log(1 + x)` for small values of `x`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 0.0f, 1.0f, 6.389f });
        /// var result = Functional.Log1P(input);
        /// // Result: [0.0, 0.693, 2.0] (approximately ln(1), ln(2), ln(7.389))
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Log1P(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Log1p(input);
        }

        /// <summary>
        /// Returns `log2(input)` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the base-2 logarithm of each element in the `input` tensor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1.0f, 2.0f, 4.0f, 8.0f });
        /// var result = Functional.Log2(input);
        /// // Result: [0.0, 1.0, 2.0, 3.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Log2(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Log2(input);
        }

        /// <summary>
        /// Returns `log(e^input + e^other)` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the logarithm of the sum of exponentials. This function provides better numerical stability than computing `log(exp(x) + exp(y))` directly.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new[] { 1.0f, 2.0f, 3.0f });
        /// var b = Functional.Constant(new[] { 1.0f, 2.0f, 3.0f });
        /// var result = Functional.LogAddExp(a, b);
        /// // Result: [1.693, 2.693, 3.693] (approximately ln(e^1+e^1), ln(e^2+e^2), ln(e^3+e^3))
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input tensor.</param>
        /// <param name="other">The second input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor LogAddExp(FunctionalTensor input, FunctionalTensor other)
        {
            return Log(Exp(input) + Exp(other));
        }

        /// <summary>
        /// Returns the logical AND `input &#38; other` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation performs element-wise logical AND, treating non-zero values as true and zero as false.
        /// The tensors are broadcast to a common shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new[] { 0, 1, 1, 0 });
        /// var b = Functional.Constant(new[] { 0, 0, 1, 1 });
        /// var result = Functional.LogicalAnd(a, b);
        /// // Result: [0, 0, 1, 0] (only both true gives true)
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input tensor.</param>
        /// <param name="other">The second input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor LogicalAnd(FunctionalTensor input, FunctionalTensor other)
        {
            return FunctionalLayer.And(NotEqual(input, Constant(0)), NotEqual(other, Constant(0)));
        }

        /// <summary>
        /// Returns the logical NOT `~input` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation performs element-wise logical NOT, treating non-zero values as true and zero as false.
        /// Returns `1` (true) for zero values and `0` (false) for non-zero values.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 0, 1, -5, 0, 10 });
        /// var result = Functional.LogicalNot(input);
        /// // Result: [1, 0, 0, 1, 0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor LogicalNot(FunctionalTensor input)
        {
            var zero = Constant(0);
            (input, zero) = PromoteTypes(input, zero);
            return FunctionalLayer.Equal(input, zero);
        }

        /// <summary>
        /// Returns the logical OR `input | other` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation performs element-wise logical OR, treating non-zero values as true and zero as false.
        /// The tensors are broadcast to a common shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new[] { 0, 1, 1, 0 });
        /// var b = Functional.Constant(new[] { 0, 0, 1, 1 });
        /// var result = Functional.LogicalOr(a, b);
        /// // Result: [0, 1, 1, 1] (at least one true gives true)
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input tensor.</param>
        /// <param name="other">The second input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor LogicalOr(FunctionalTensor input, FunctionalTensor other)
        {
            return FunctionalLayer.Or(NotEqual(input, Constant(0)), NotEqual(other, Constant(0)));
        }

        /// <summary>
        /// Returns the logical XOR `input ^ other` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation performs element-wise logical XOR (exclusive OR), treating non-zero values as true and zero as false.
        /// Returns `true` when exactly one input is true.
        /// The tensors are broadcast to a common shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new[] { 0, 1, 1, 0 });
        /// var b = Functional.Constant(new[] { 0, 0, 1, 1 });
        /// var result = Functional.LogicalXor(a, b);
        /// // Result: [0, 1, 0, 1] (exactly one true gives true)
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input tensor.</param>
        /// <param name="other">The second input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor LogicalXor(FunctionalTensor input, FunctionalTensor other)
        {
            return FunctionalLayer.Xor(NotEqual(input, Constant(0)), NotEqual(other, Constant(0)));
        }

        /// <summary>
        /// Returns the bitwise AND `input &#38; other` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation performs element-wise bitwise AND on integer tensors.
        /// The tensors must be of integer type and broadcastable to a common shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new[] { 12, 15, 7 }); // Binary: 1100, 1111, 0111
        /// var b = Functional.Constant(new[] { 10, 9, 5 });  // Binary: 1010, 1001, 0101
        /// var result = Functional.BitwiseAnd(a, b);
        /// // Result: [8, 9, 5] (Binary: 1000, 1001, 0101)
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input tensor.</param>
        /// <param name="other">The second input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor BitwiseAnd(FunctionalTensor input, FunctionalTensor other)
        {
            DeclareType(DataType.Int, input, other);
            return FunctionalLayer.BitwiseAnd(input, other);
        }

        /// <summary>
        /// Returns the bitwise NOT `~input` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation performs element-wise bitwise NOT (complement) on integer tensors.
        /// The tensor must be of integer type.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 0, 1, 5, -1 });
        /// var result = Functional.BitwiseNot(input);
        /// // Result: [-1, -2, -6, 0] (bitwise complement)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor BitwiseNot(FunctionalTensor input)
        {
            DeclareType(DataType.Int, input);
            return FunctionalLayer.BitwiseNot(input);
        }

        /// <summary>
        /// Returns the bitwise OR `input &#124; other` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation performs element-wise bitwise OR on integer tensors.
        /// The tensors must be of integer type and broadcastable to a common shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new[] { 12, 15, 7 }); // Binary: 1100, 1111, 0111
        /// var b = Functional.Constant(new[] { 10, 9, 5 });  // Binary: 1010, 1001, 0101
        /// var result = Functional.BitwiseOr(a, b);
        /// // Result: [14, 15, 7] (Binary: 1110, 1111, 0111)
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input tensor.</param>
        /// <param name="other">The second input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor BitwiseOr(FunctionalTensor input, FunctionalTensor other)
        {
            DeclareType(DataType.Int, input, other);
            return FunctionalLayer.BitwiseOr(input, other);
        }

        /// <summary>
        /// Returns the bitwise XOR `input ^ other` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation performs element-wise bitwise XOR (exclusive OR) on integer tensors.
        /// The tensors must be of integer type and broadcastable to a common shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new[] { 12, 15, 7 }); // Binary: 1100, 1111, 0111
        /// var b = Functional.Constant(new[] { 10, 9, 5 });  // Binary: 1010, 1001, 0101
        /// var result = Functional.BitwiseXor(a, b);
        /// // Result: [6, 6, 2] (Binary: 0110, 0110, 0010)
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input tensor.</param>
        /// <param name="other">The second input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor BitwiseXor(FunctionalTensor input, FunctionalTensor other)
        {
            DeclareType(DataType.Int, input, other);
            return FunctionalLayer.BitwiseXor(input, other);
        }

        /// <summary>
        /// Returns `input * other` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation performs element-wise multiplication of two tensors.
        /// Promotes `input` and `other` to a compatible data type if necessary.
        /// The tensors are broadcast to a common shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new[] { 2.0f, 3.0f, 4.0f });
        /// var b = Functional.Constant(new[] { 5.0f, 6.0f, 7.0f });
        /// var result = Functional.Mul(a, b);
        /// // Result: [10.0, 18.0, 28.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input tensor.</param>
        /// <param name="other">The second input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Mul(FunctionalTensor input, FunctionalTensor other)
        {
            (input, other) = PromoteTypes(input, other);
            return FunctionalLayer.Mul(input, other);
        }

        /// <summary>
        /// Returns `-input` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation negates each element in the `input` tensor, changing the sign.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -2.0f, -1.0f, 0.0f, 1.0f, 2.0f });
        /// var result = Functional.Neg(input);
        /// // Result: [2.0, 1.0, 0.0, -1.0, -2.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Neg(FunctionalTensor input)
        {
            return FunctionalLayer.Neg(input);
        }

        /// <summary>
        /// Returns the `input`.
        /// </summary>
        /// <remarks>
        /// This operation returns the `input` tensor unchanged. It is the identity operation.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1.0f, 2.0f, 3.0f });
        /// var result = Functional.Positive(input);
        /// // Result: [1.0, 2.0, 3.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Positive(FunctionalTensor input)
        {
            return input;
        }

        /// <summary>
        /// Returns `input^exponent` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation raises each element in the `input` tensor to the power of the corresponding element in the exponent tensor.
        /// Promotes `input` to float type if necessary.
        /// The tensors are broadcast to a common shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var base = Functional.Constant(new[] { 2.0f, 3.0f, 4.0f });
        /// var exponent = Functional.Constant(new[] { 3.0f, 2.0f, 1.0f });
        /// var result = Functional.Pow(base, exponent);
        /// // Result: [8.0, 9.0, 4.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input tensor.</param>
        /// <param name="exponent">The second input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Pow(FunctionalTensor input, FunctionalTensor exponent)
        {
            input = input.Float();
            return FunctionalLayer.Pow(input, exponent);
        }

        /// <summary>
        /// Returns `input^exponent` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation raises each element in the `input` tensor to a scalar exponent value.
        /// Promotes `input` to float type if necessary.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var base = Functional.Constant(new[] { 2.0f, 3.0f, 4.0f });
        /// var result = Functional.Pow(base, 2.0f);
        /// // Result: [4.0, 9.0, 16.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input tensor.</param>
        /// <param name="exponent">The scalar exponent value.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Pow(FunctionalTensor input, float exponent)
        {
            input = input.Float();
            return FunctionalLayer.Pow(input, Constant(exponent));
        }

        /// <summary>
        /// Returns the `input` values converted from angles in radians to degrees element-wise.
        /// </summary>
        /// <remarks>
        /// This operation converts angles from radians to degrees by multiplying each element by `180/π`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var radians = Functional.Constant(new[] { 0.0f, 1.571f, 3.142f, 6.283f });
        /// var degrees = Functional.Rad2Deg(radians);
        /// // Result: [0.0, 90.0, 180.0, 360.0] (approximately)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Rad2Deg(FunctionalTensor input)
        {
            return Mathf.Rad2Deg * input;
        }

        /// <summary>
        /// Returns `1/input` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the reciprocal (multiplicative inverse) of each element in the `input` tensor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1.0f, 2.0f, 4.0f, 10.0f });
        /// var result = Functional.Reciprocal(input);
        /// // Result: [1.0, 0.5, 0.25, 0.1]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Reciprocal(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Reciprocal(input);
        }

        /// <summary>
        /// Returns `input % other` element-wise. The sign of the output is the same as that of the divider.
        /// </summary>
        /// <remarks>
        /// This operation computes the modulo where the sign of the result matches the sign of the divisor (other).
        /// Promotes `input` and `other` to a compatible data type if necessary.
        /// The tensors are broadcast to a common shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new[] { 7.0f, -7.0f, 7.0f, -7.0f });
        /// var b = Functional.Constant(new[] { 3.0f, 3.0f, -3.0f, -3.0f });
        /// var result = Functional.Remainder(a, b);
        /// // Result: [1.0, 2.0, -2.0, -1.0] (sign follows divisor)
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input tensor.</param>
        /// <param name="other">The second input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Remainder(FunctionalTensor input, FunctionalTensor other)
        {
            (input, other) = PromoteTypes(input, other);
            return FunctionalLayer.Mod(input, other, false);
        }

        /// <summary>
        /// Returns `[input]` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation rounds each element in the `input` tensor to the nearest integer.
        /// If `input` is already an integer tensor, it is returned unchanged.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -1.7f, -0.5f, 0.5f, 1.2f });
        /// var result = Functional.Round(input);
        /// // Result: [-2.0, 0.0, 0.0, 1.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Round(FunctionalTensor input)
        {
            // TODO implement 'decimals' arg
            if (input.dataType == DataType.Int)
                return input;
            return FunctionalLayer.Round(input);
        }

        /// <summary>
        /// Returns `1/√input` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the reciprocal of the square root of each element in the `input` tensor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 1.0f, 4.0f, 9.0f, 16.0f });
        /// var result = Functional.RSqrt(input);
        /// // Result: [1.0, 0.5, 0.333, 0.25] (approximately)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor RSqrt(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Rsqrt(input);
        }

        /// <summary>
        /// Returns the sign of the `input` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation returns `-1` for negative values, `0` for zero, and `1` for positive values.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -5.0f, -1.0f, 0.0f, 1.0f, 5.0f });
        /// var result = Functional.Sign(input);
        /// // Result: [-1.0, -1.0, 0.0, 1.0, 1.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Sign(FunctionalTensor input)
        {
            return FunctionalLayer.Sign(input);
        }

        /// <summary>
        /// Returns `sin(input)` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the sine of each element in the `input` tensor.
        /// Input is expected in radians.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 0.0f, 1.5708f, 3.14159f });
        /// var result = Functional.Sin(input);
        /// // Result: [0.0, 1.0, 0.0] (approximately sin(0), sin(π/2), sin(π))
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Sin(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Sin(input);
        }

        /// <summary>
        /// Returns `sinh(input)` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the hyperbolic sine of each element in the `input` tensor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -1.0f, 0.0f, 1.0f });
        /// var result = Functional.Sinh(input);
        /// // Result: [-1.175, 0.0, 1.175] (approximately)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Sinh(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Sinh(input);
        }

        /// <summary>
        /// Returns `√(input)` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the square root of each element in the `input` tensor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 0.0f, 1.0f, 4.0f, 9.0f, 16.0f });
        /// var result = Functional.Sqrt(input);
        /// // Result: [0.0, 1.0, 2.0, 3.0, 4.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Sqrt(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Sqrt(input);
        }

        /// <summary>
        /// Returns `input*input` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the square of each element in the `input` tensor.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -2.0f, -1.0f, 0.0f, 1.0f, 2.0f });
        /// var result = Functional.Square(input);
        /// // Result: [4.0, 1.0, 0.0, 1.0, 4.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Square(FunctionalTensor input)
        {
            return FunctionalLayer.Square(input);
        }

        /// <summary>
        /// Returns `input - other` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation performs element-wise subtraction of two tensors.
        /// Promotes `input` and `other` to a compatible data type if necessary.
        /// The tensors are broadcast to a common shape.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var a = Functional.Constant(new[] { 5.0f, 7.0f, 9.0f });
        /// var b = Functional.Constant(new[] { 1.0f, 2.0f, 3.0f });
        /// var result = Functional.Sub(a, b);
        /// // Result: [4.0, 5.0, 6.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The first input tensor.</param>
        /// <param name="other">The second input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Sub(FunctionalTensor input, FunctionalTensor other)
        {
            (input, other) = PromoteTypes(input, other);
            return FunctionalLayer.Sub(input, other);
        }

        /// <summary>
        /// Returns `tan(input)` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the tangent of each element in the `input` tensor.
        /// Input is expected in radians.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { 0.0f, 0.785f, 1.047f });
        /// var result = Functional.Tan(input);
        /// // Result: [0.0, 1.0, 1.732] (approximately tan(0), tan(π/4), tan(π/3))
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Tan(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Tan(input);
        }

        /// <summary>
        /// Returns `tanh(input)` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation computes the hyperbolic tangent of each element in the `input` tensor.
        /// The output is in the range `(-1, 1)`.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -1.0f, 0.0f, 1.0f });
        /// var result = Functional.Tanh(input);
        /// // Result: [-0.762, 0.0, 0.762] (approximately)
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Tanh(FunctionalTensor input)
        {
            input = input.Float();
            return FunctionalLayer.Tanh(input);
        }

        /// <summary>
        /// Returns the truncated integer values of the elements of `input` element-wise.
        /// </summary>
        /// <remarks>
        /// This operation truncates each element in the `input` tensor towards zero, removing the fractional part.
        /// If `input` is already an integer tensor, it is returned unchanged.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var input = Functional.Constant(new[] { -1.7f, -0.3f, 0.5f, 1.2f });
        /// var result = Functional.Trunc(input);
        /// // Result: [-1.0, 0.0, 0.0, 1.0]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input tensor.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor Trunc(FunctionalTensor input)
        {
            if (input.dataType == DataType.Int)
                return input;
            return FunctionalLayer.Trunc(input);
        }
    }
}
