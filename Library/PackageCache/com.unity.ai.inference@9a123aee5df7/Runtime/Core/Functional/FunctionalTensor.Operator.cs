using UnityEngine;

namespace Unity.InferenceEngine
{
    public partial class FunctionalTensor
    {
        /// <summary>
        /// Unary plus operator.
        /// </summary>
        /// <remarks>
        /// This operator returns the input tensor unchanged.
        /// It exists for symmetry with the unary negation operator and to support explicit positive notation in expressions.
        /// </remarks>
        /// <param name="a">The functional tensor operand.</param>
        /// <returns>The input tensor unchanged.</returns>
        public static FunctionalTensor operator +(FunctionalTensor a) => a;

        /// <summary>
        /// Unary negation operator.
        /// </summary>
        /// <remarks>
        /// This operator computes the element-wise negation of the input tensor.
        /// This is equivalent to calling <see cref="Functional.Neg"/>.
        /// </remarks>
        /// <param name="a">The functional tensor to negate.</param>
        /// <returns>A functional tensor containing the element-wise negation of the input.</returns>
        public static FunctionalTensor operator -(FunctionalTensor a) => Functional.Neg(a);

        /// <summary>
        /// Addition operator.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise addition between two functional tensors.
        /// The tensors are broadcasted to a compatible shape if necessary.
        /// This is equivalent to calling <see cref="Functional.Add"/>.
        /// </remarks>
        /// <param name="a">The first functional tensor.</param>
        /// <param name="b">The second functional tensor.</param>
        /// <returns>A functional tensor containing the element-wise sum of the two inputs.</returns>
        public static FunctionalTensor operator +(FunctionalTensor a, FunctionalTensor b) => Functional.Add(a, b);

        /// <summary>
        /// Addition operator.
        /// </summary>
        /// <remarks>
        /// This operator adds a constant integer value to every element in the tensor.
        /// </remarks>
        /// <param name="a">The functional tensor.</param>
        /// <param name="b">The scalar integer value to add.</param>
        /// <returns>A functional tensor with the scalar value added to each element.</returns>
        public static FunctionalTensor operator +(FunctionalTensor a, int b) => ScalarMad(a, 1, b);

        /// <summary>
        /// Addition operator.
        /// </summary>
        /// <remarks>
        /// This operator adds a constant integer value to every element in the tensor.
        /// This overload supports commutative addition (scalar + tensor = tensor + scalar).
        /// </remarks>
        /// <param name="a">The scalar integer value to add.</param>
        /// <param name="b">The functional tensor.</param>
        /// <returns>A functional tensor with the scalar value added to each element.</returns>
        public static FunctionalTensor operator +(int a, FunctionalTensor b) => b + a;

        /// <summary>
        /// Addition operator.
        /// </summary>
        /// <remarks>
        /// This operator adds a constant float value to every element in the tensor.
        /// The tensor is promoted to float type if necessary.
        /// </remarks>
        /// <param name="a">The functional tensor.</param>
        /// <param name="b">The scalar float value to add.</param>
        /// <returns>A functional tensor with the scalar value added to each element.</returns>
        public static FunctionalTensor operator +(FunctionalTensor a, float b) => ScalarMad(a, 1, b);

        /// <summary>
        /// Addition operator.
        /// </summary>
        /// <remarks>
        /// This operator adds a constant float value to every element in the tensor.
        /// The tensor is promoted to float type if necessary.
        /// </remarks>
        /// <param name="a">The scalar float value to add.</param>
        /// <param name="b">The functional tensor.</param>
        /// <returns>A functional tensor with the scalar value added to each element.</returns>
        public static FunctionalTensor operator +(float a, FunctionalTensor b) => b + a;

        /// <summary>
        /// Subtraction operator.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise subtraction between two functional tensors.
        /// The tensors are broadcasted to a compatible shape if necessary.
        /// This is equivalent to calling <see cref="Functional.Sub"/>.
        /// </remarks>
        /// <param name="a">The functional tensor to subtract from.</param>
        /// <param name="b">The functional tensor to subtract.</param>
        /// <returns>A functional tensor containing the element-wise difference.</returns>
        public static FunctionalTensor operator -(FunctionalTensor a, FunctionalTensor b) => Functional.Sub(a, b);

        /// <summary>
        /// Subtraction operator.
        /// </summary>
        /// <remarks>
        /// This operator subtracts a constant integer value from every element in the tensor.
        /// The tensor is promoted to float type if necessary.
        /// </remarks>
        /// <param name="a">The functional tensor to subtract from.</param>
        /// <param name="b">The scalar integer value to subtract.</param>
        /// <returns>A functional tensor with the scalar value subtracted from each element.</returns>
        public static FunctionalTensor operator -(FunctionalTensor a, int b) => ScalarMad(a, 1, -b);

        /// <summary>
        /// Subtraction operator.
        /// </summary>
        /// <remarks>
        /// This operator subtracts every element in the tensor from a constant integer value.
        /// </remarks>
        /// <param name="a">The scalar integer value to subtract from.</param>
        /// <param name="b">The functional tensor to subtract.</param>
        /// <returns>A functional tensor with each element subtracted from the scalar value.</returns>
        public static FunctionalTensor operator -(int a, FunctionalTensor b) => ScalarMad(b, -1, a);

        /// <summary>
        /// Subtraction operator.
        /// </summary>
        /// <remarks>
        /// This operator subtracts a constant float value from every element in the tensor.
        /// The tensor is promoted to float type if necessary.
        /// </remarks>
        /// <param name="a">The functional tensor to subtract from.</param>
        /// <param name="b">The scalar float value to subtract.</param>
        /// <returns>A functional tensor with the scalar value subtracted from each element.</returns>
        public static FunctionalTensor operator -(FunctionalTensor a, float b) => ScalarMad(a, 1, -b);

        /// <summary>
        /// Subtraction operator.
        /// </summary>
        /// <remarks>
        /// This operator subtracts every element in the tensor from a constant float value.
        /// The tensor is promoted to float type if necessary.
        /// </remarks>
        /// <param name="a">The scalar float value to subtract from.</param>
        /// <param name="b">The functional tensor to subtract.</param>
        /// <returns>A functional tensor with each element subtracted from the scalar value.</returns>
        public static FunctionalTensor operator -(float a, FunctionalTensor b) => ScalarMad(b, -1, a);

        /// <summary>
        /// Multiply operator.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise multiplication between two functional tensors.
        /// The tensors are broadcasted to a compatible shape if necessary.
        /// This is equivalent to calling <see cref="Functional.Mul"/>.
        /// </remarks>
        /// <param name="a">The first functional tensor.</param>
        /// <param name="b">The second functional tensor.</param>
        /// <returns>A functional tensor containing the element-wise product.</returns>
        public static FunctionalTensor operator *(FunctionalTensor a, FunctionalTensor b) => Functional.Mul(a, b);

        /// <summary>
        /// Multiplication operator.
        /// </summary>
        /// <remarks>
        /// This operator multiplies every element in the tensor by a constant integer value.
        /// This overload supports commutative multiplication (scalar * tensor = tensor * scalar).
        /// </remarks>
        /// <param name="a">The scalar integer value.</param>
        /// <param name="b">The functional tensor.</param>
        /// <returns>A functional tensor with each element multiplied by the scalar value.</returns>
        public static FunctionalTensor operator *(int a, FunctionalTensor b) => ScalarMad(b, a, 0);

        /// <summary>
        /// Multiplication operator.
        /// </summary>
        /// <remarks>
        /// This operator multiplies every element in the tensor by a constant integer value.
        /// </remarks>
        /// <param name="a">The functional tensor.</param>
        /// <param name="b">The scalar integer value.</param>
        /// <returns>A functional tensor with each element multiplied by the scalar value.</returns>
        public static FunctionalTensor operator *(FunctionalTensor a, int b) => ScalarMad(a, b, 0);

        /// <summary>
        /// Multiplication operator.
        /// </summary>
        /// <remarks>
        /// This operator multiplies every element in the tensor by a constant float value.
        /// The tensor is promoted to float type if necessary.
        /// </remarks>
        /// <param name="a">The scalar float value.</param>
        /// <param name="b">The functional tensor.</param>
        /// <returns>A functional tensor with each element multiplied by the scalar value.</returns>
        public static FunctionalTensor operator *(float a, FunctionalTensor b) => ScalarMad(b, a, 0);

        /// <summary>
        /// Multiplication operator.
        /// </summary>
        /// <remarks>
        /// This operator multiplies every element in the tensor by a constant float value.
        /// The tensor is promoted to float type if necessary.
        /// </remarks>
        /// <param name="a">The functional tensor.</param>
        /// <param name="b">The scalar float value.</param>
        /// <returns>A functional tensor with each element multiplied by the scalar value.</returns>
        public static FunctionalTensor operator *(FunctionalTensor a, float b) => ScalarMad(a, b, 0);

        /// <summary>
        /// Division operator.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise division between two functional tensors.
        /// The tensors are broadcasted to a compatible shape if necessary.
        /// This is equivalent to calling <see cref="Functional.Div(FunctionalTensor, FunctionalTensor)"/>.
        /// </remarks>
        /// <param name="a">The functional tensor to divide.</param>
        /// <param name="b">The functional tensor to divide by.</param>
        /// <returns>A functional tensor containing the element-wise quotient.</returns>
        public static FunctionalTensor operator /(FunctionalTensor a, FunctionalTensor b) => Functional.Div(a, b);

        /// <summary>
        /// Division operator.
        /// </summary>
        /// <remarks>
        /// This operator divides every element in the tensor by a constant integer value.
        /// For integer tensors, this performs integer division.
        /// </remarks>
        /// <param name="a">The functional tensor to divide.</param>
        /// <param name="b">The scalar integer value to divide by.</param>
        /// <returns>A functional tensor with each element divided by the scalar value.</returns>
        public static FunctionalTensor operator /(FunctionalTensor a, int b) => a.dataType == DataType.Float ? a / (float)b : Functional.Div(a, Functional.Constant(b));

        /// <summary>
        /// Division operator.
        /// </summary>
        /// <remarks>
        /// This operator divides a constant integer value by every element in the tensor.
        /// For integer tensors, this performs integer division.
        /// </remarks>
        /// <param name="a">The scalar integer value to divide.</param>
        /// <param name="b">The functional tensor to divide by.</param>
        /// <returns>A functional tensor with the scalar divided by each element.</returns>
        public static FunctionalTensor operator /(int a, FunctionalTensor b) => b.dataType == DataType.Float ? (float)a / b : Functional.Div(Functional.Constant(a), b);

        /// <summary>
        /// Division operator.
        /// </summary>
        /// <remarks>
        /// This operator divides every element in the tensor by a constant float value.
        /// The tensor is promoted to float type if necessary.
        /// </remarks>
        /// <param name="a">The functional tensor to divide.</param>
        /// <param name="b">The scalar float value to divide by.</param>
        /// <returns>A functional tensor with each element divided by the scalar value.</returns>
        public static FunctionalTensor operator /(FunctionalTensor a, float b) => ScalarMad(a, 1 / b, 0);

        /// <summary>
        /// Division operator.
        /// </summary>
        /// <remarks>
        /// This operator divides a constant float value by every element in the tensor.
        /// The tensor is promoted to float type if necessary.
        /// </remarks>
        /// <param name="a">The scalar float value to divide.</param>
        /// <param name="b">The functional tensor to divide by.</param>
        /// <returns>A functional tensor with the scalar divided by each element.</returns>
        public static FunctionalTensor operator /(float a, FunctionalTensor b) => a * Functional.Reciprocal(b);

        /// <summary>
        /// Remainder operator.
        /// </summary>
        /// <remarks>
        /// This operator computes the element-wise remainder (modulo operation) between two functional tensors.
        /// The tensors are broadcasted to a compatible shape if necessary.
        /// This is equivalent to calling <see cref="Functional.Remainder"/>.
        /// For element `a` and `b`, the result is `a - floor(a/b) * b`.
        /// </remarks>
        /// <param name="a">The functional tensor to divide (dividend).</param>
        /// <param name="b">The functional tensor to divide by (divisor).</param>
        /// <returns>A functional tensor containing the element-wise remainder.</returns>
        public static FunctionalTensor operator %(FunctionalTensor a, FunctionalTensor b) => Functional.Remainder(a, b);

        /// <summary>
        /// Remainder operator.
        /// </summary>
        /// <remarks>
        /// This operator computes the remainder when dividing every element in the tensor by a constant integer value.
        /// </remarks>
        /// <param name="a">The functional tensor to divide.</param>
        /// <param name="b">The scalar integer value to divide by.</param>
        /// <returns>A functional tensor with the remainder of each element divided by the scalar.</returns>
        public static FunctionalTensor operator %(FunctionalTensor a, int b) => Functional.Remainder(a, Functional.Constant(b));

        /// <summary>
        /// Remainder operator.
        /// </summary>
        /// <remarks>
        /// This operator computes the remainder when dividing a constant integer value by every element in the tensor.
        /// </remarks>
        /// <param name="a">The scalar integer value to divide.</param>
        /// <param name="b">The functional tensor to divide by.</param>
        /// <returns>A functional tensor with the scalar modulo each element.</returns>
        public static FunctionalTensor operator %(int a, FunctionalTensor b) => Functional.Remainder(Functional.Constant(a), b);

        /// <summary>
        /// Remainder operator.
        /// </summary>
        /// <remarks>
        /// This operator computes the remainder when dividing every element in the tensor by a constant float value.
        /// </remarks>
        /// <param name="a">The functional tensor to divide.</param>
        /// <param name="b">The scalar float value to divide by.</param>
        /// <returns>A functional tensor with the remainder of each element divided by the scalar.</returns>
        public static FunctionalTensor operator %(FunctionalTensor a, float b) => Functional.Remainder(a, Functional.Constant(b));

        /// <summary>
        /// Remainder operator.
        /// </summary>
        /// <remarks>
        /// This operator computes the remainder when dividing a constant float value by every element in the tensor.
        /// </remarks>
        /// <param name="a">The scalar float value to divide.</param>
        /// <param name="b">The functional tensor to divide by.</param>
        /// <returns>A functional tensor with the scalar modulo each element.</returns>
        public static FunctionalTensor operator %(float a, FunctionalTensor b) => Functional.Remainder(Functional.Constant(a), b);

        /// <summary>
        /// Greater than operator.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise comparison between two functional tensors.
        /// Returns an integer tensor where each element is `1` if `a &gt; b`, and `0` otherwise.
        /// The tensors are broadcasted to a compatible shape if necessary.
        /// This is equivalent to calling <see cref="Functional.Greater"/>.
        /// </remarks>
        /// <param name="a">The first functional tensor to compare.</param>
        /// <param name="b">The second functional tensor to compare.</param>
        /// <returns>An integer functional tensor with `1` where `a &gt; b` and `0` elsewhere.</returns>
        public static FunctionalTensor operator >(FunctionalTensor a, FunctionalTensor b) => Functional.Greater(a, b);

        /// <summary>
        /// Greater than operator.
        /// </summary>
        /// <remarks>
        /// This operator compares every element in the tensor with a constant integer value.
        /// Returns an integer tensor where each element is `1` if `element &gt; scalar`, and `0` otherwise.
        /// </remarks>
        /// <param name="a">The functional tensor to compare.</param>
        /// <param name="b">The scalar integer value to compare with.</param>
        /// <returns>An integer functional tensor with `1` where elements are greater than the scalar.</returns>
        public static FunctionalTensor operator >(FunctionalTensor a, int b) => a.dataType == DataType.Float ? a > (float)b : a > Functional.Constant(b);

        /// <summary>
        /// Greater than operator.
        /// </summary>
        /// <remarks>
        /// This operator compares every element in the tensor with a constant float value.
        /// Returns an integer tensor where each element is `1` if `element &gt; scalar`, and `0` otherwise.
        /// For integer tensors, the float is floored before comparison.
        /// </remarks>
        /// <param name="a">The functional tensor to compare.</param>
        /// <param name="b">The scalar float value to compare with.</param>
        /// <returns>An integer functional tensor with `1` where elements are greater than the scalar.</returns>
        public static FunctionalTensor operator >(FunctionalTensor a, float b) => a.dataType == DataType.Int ? a > Mathf.FloorToInt(b) : a > Functional.Constant(b);

        /// <summary>
        /// Greater than operator.
        /// </summary>
        /// <remarks>
        /// This operator compares a constant integer value with every element in the tensor.
        /// Returns an integer tensor where each element is `1` if `scalar &gt; element`, and `0` otherwise.
        /// </remarks>
        /// <param name="a">The scalar integer value to compare.</param>
        /// <param name="b">The functional tensor to compare with.</param>
        /// <returns>An integer functional tensor with `1` where the scalar is greater than elements.</returns>
        public static FunctionalTensor operator >(int a, FunctionalTensor b) => b < a;

        /// <summary>
        /// Greater than operator.
        /// </summary>
        /// <remarks>
        /// This operator compares a constant float value with every element in the tensor.
        /// Returns an integer tensor where each element is `1` if `scalar &gt; element`, and `0` otherwise.
        /// </remarks>
        /// <param name="a">The scalar float value to compare.</param>
        /// <param name="b">The functional tensor to compare with.</param>
        /// <returns>An integer functional tensor with `1` where the scalar is greater than elements.</returns>
        public static FunctionalTensor operator >(float a, FunctionalTensor b) => b < a;

        /// <summary>
        /// Less than operator.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise comparison between two functional tensors.
        /// Returns an integer tensor where each element is `1` if `a[i] &lt; b[i]`, and `0` otherwise.
        /// The tensors are broadcasted to a compatible shape if necessary.
        /// This is equivalent to calling <see cref="Functional.Less"/>.
        /// </remarks>
        /// <param name="a">The first functional tensor to compare.</param>
        /// <param name="b">The second functional tensor to compare.</param>
        /// <returns>An integer functional tensor with `1` where `a &lt; b` and `0` elsewhere.</returns>
        public static FunctionalTensor operator <(FunctionalTensor a, FunctionalTensor b) => Functional.Less(a, b);

        /// <summary>
        /// Less than operator.
        /// </summary>
        /// <remarks>
        /// This operator compares every element in the tensor with a constant integer value.
        /// Returns an integer tensor where each element is `1` if `element &lt; scalar`, and `0` otherwise.
        /// </remarks>
        /// <param name="a">The functional tensor to compare.</param>
        /// <param name="b">The scalar integer value to compare with.</param>
        /// <returns>An integer functional tensor with `1` where elements are less than the scalar.</returns>
        public static FunctionalTensor operator <(FunctionalTensor a, int b) => a.dataType == DataType.Float ? a < (float)b : a < Functional.Constant(b);

        /// <summary>
        /// Less than operator.
        /// </summary>
        /// <remarks>
        /// This operator compares every element in the tensor with a constant float value.
        /// Returns an integer tensor where each element is `1` if `element &lt; scalar`, and `0` otherwise.
        /// For integer tensors, the float is ceiled before comparison.
        /// </remarks>
        /// <param name="a">The functional tensor to compare.</param>
        /// <param name="b">The scalar float value to compare with.</param>
        /// <returns>An integer functional tensor with `1` where elements are less than the scalar.</returns>
        public static FunctionalTensor operator <(FunctionalTensor a, float b) => a.dataType == DataType.Int ? a < Mathf.CeilToInt(b) : a < Functional.Constant(b);

        /// <summary>
        /// Less than operator.
        /// </summary>
        /// <remarks>
        /// This operator compares a constant integer value with every element in the tensor.
        /// Returns an integer tensor where each element is `1` if `scalar &lt; element`, and `0` otherwise.
        /// </remarks>
        /// <param name="a">The scalar integer value to compare.</param>
        /// <param name="b">The functional tensor to compare with.</param>
        /// <returns>An integer functional tensor with `1` where the scalar is less than elements.</returns>
        public static FunctionalTensor operator <(int a, FunctionalTensor b) => b > a;

        /// <summary>
        /// Less than operator.
        /// </summary>
        /// <remarks>
        /// This operator compares a constant float value with every element in the tensor.
        /// Returns an integer tensor where each element is `1` if `scalar &lt; element`, and `0` otherwise.
        /// </remarks>
        /// <param name="a">The scalar float value to compare.</param>
        /// <param name="b">The functional tensor to compare with.</param>
        /// <returns>An integer functional tensor with `1` where the scalar is less than elements.</returns>
        public static FunctionalTensor operator <(float a, FunctionalTensor b) => b > a;

        /// <summary>
        /// Greater than or equal operator.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise comparison between two functional tensors.
        /// Returns an integer tensor where each element is `1` if `a[i] &gt;= b[i]`, and `0` otherwise.
        /// The tensors are broadcasted to a compatible shape if necessary.
        /// This is equivalent to calling <see cref="Functional.GreaterEqual"/>.
        /// </remarks>
        /// <param name="a">The first functional tensor to compare.</param>
        /// <param name="b">The second functional tensor to compare.</param>
        /// <returns>An integer functional tensor with `1` where `a &gt;= b` and `0` elsewhere.</returns>
        public static FunctionalTensor operator >=(FunctionalTensor a, FunctionalTensor b) => Functional.GreaterEqual(a, b);

        /// <summary>
        /// Greater than or equal operator.
        /// </summary>
        /// <remarks>
        /// This operator compares every element in the tensor with a constant integer value.
        /// Returns an integer tensor where each element is `1` if `element &gt;= scalar`, and `0` otherwise.
        /// </remarks>
        /// <param name="a">The functional tensor to compare.</param>
        /// <param name="b">The scalar integer value to compare with.</param>
        /// <returns>An integer functional tensor with `1` where elements are greater than or equal to the scalar.</returns>
        public static FunctionalTensor operator >=(FunctionalTensor a, int b) => a.dataType == DataType.Float ? a >= (float)b : a >= Functional.Constant(b);

        /// <summary>
        /// Greater than or equal operator.
        /// </summary>
        /// <remarks>
        /// This operator compares every element in the tensor with a constant float value.
        /// Returns an integer tensor where each element is `1` if `element &gt;= scalar`, and `0` otherwise.
        /// For integer tensors, the float is ceiled before comparison.
        /// </remarks>
        /// <param name="a">The functional tensor to compare.</param>
        /// <param name="b">The scalar float value to compare with.</param>
        /// <returns>An integer functional tensor with `1` where elements are greater than or equal to the scalar.</returns>
        public static FunctionalTensor operator >=(FunctionalTensor a, float b) => a.dataType == DataType.Int ? a >= Mathf.CeilToInt(b) : a >= Functional.Constant(b);

        /// <summary>
        /// Greater than or equal operator.
        /// </summary>
        /// <remarks>
        /// This operator compares a constant integer value with every element in the tensor.
        /// Returns an integer tensor where each element is `1` if `scalar &gt;= element`, and `0` otherwise.
        /// </remarks>
        /// <param name="a">The scalar integer value to compare.</param>
        /// <param name="b">The functional tensor to compare with.</param>
        /// <returns>An integer functional tensor with `1` where the scalar is greater than or equal to elements.</returns>
        public static FunctionalTensor operator >=(int a, FunctionalTensor b) => b <= a;

        /// <summary>
        /// Greater than or equal operator.
        /// </summary>
        /// <remarks>
        /// This operator compares a constant float value with every element in the tensor.
        /// Returns an integer tensor where each element is `1` if `scalar &gt;= element`, and `0` otherwise.
        /// </remarks>
        /// <param name="a">The scalar float value to compare.</param>
        /// <param name="b">The functional tensor to compare with.</param>
        /// <returns>An integer functional tensor with `1` where the scalar is greater than or equal to elements.</returns>
        public static FunctionalTensor operator >=(float a, FunctionalTensor b) => b <= a;

        /// <summary>
        /// Less than or equal operator.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise comparison between two functional tensors.
        /// Returns an integer tensor where each element is `1` if `a[i] &lt;= b[i]`, and `0` otherwise.
        /// The tensors are broadcasted to a compatible shape if necessary.
        /// This is equivalent to calling <see cref="Functional.LessEqual"/>.
        /// </remarks>
        /// <param name="a">The first functional tensor to compare.</param>
        /// <param name="b">The second functional tensor to compare.</param>
        /// <returns>An integer functional tensor with `1` where `a &lt;= b` and `0` elsewhere.</returns>
        public static FunctionalTensor operator <=(FunctionalTensor a, FunctionalTensor b) => Functional.LessEqual(a, b);

        /// <summary>
        /// Less than or equal operator.
        /// </summary>
        /// <remarks>
        /// This operator compares every element in the tensor with a constant integer value.
        /// Returns an integer tensor where each element is `1` if `element &lt;= scalar`, and `0` otherwise.
        /// </remarks>
        /// <param name="a">The functional tensor to compare.</param>
        /// <param name="b">The scalar integer value to compare with.</param>
        /// <returns>An integer functional tensor with `1` where elements are less than or equal to the scalar.</returns>
        public static FunctionalTensor operator <=(FunctionalTensor a, int b) => a.dataType == DataType.Float ? a <= (float)b : a <= Functional.Constant(b);

        /// <summary>
        /// Less than or equal operator.
        /// </summary>
        /// <remarks>
        /// This operator compares every element in the tensor with a constant float value.
        /// Returns an integer tensor where each element is `1` if `element &lt;= scalar`, and `0` otherwise.
        /// For integer tensors, the float is floored before comparison.
        /// </remarks>
        /// <param name="a">The functional tensor to compare.</param>
        /// <param name="b">The scalar float value to compare with.</param>
        /// <returns>An integer functional tensor with `1` where elements are less than or equal to the scalar.</returns>
        public static FunctionalTensor operator <=(FunctionalTensor a, float b) => a.dataType == DataType.Int ? a <= Mathf.FloorToInt(b) : a <= Functional.Constant(b);

        /// <summary>
        /// Less than or equal operator.
        /// </summary>
        /// <remarks>
        /// This operator compares a constant integer value with every element in the tensor.
        /// Returns an integer tensor where each element is `1` if `scalar &lt;= element`, and `0` otherwise.
        /// </remarks>
        /// <param name="a">The scalar integer value to compare.</param>
        /// <param name="b">The functional tensor to compare with.</param>
        /// <returns>An integer functional tensor with `1` where the scalar is less than or equal to elements.</returns>
        public static FunctionalTensor operator <=(int a, FunctionalTensor b) => b >= a;

        /// <summary>
        /// Less than or equal operator.
        /// </summary>
        /// <remarks>
        /// This operator compares a constant float value with every element in the tensor.
        /// Returns an integer tensor where each element is `1` if `scalar &lt;= element`, and `0` otherwise.
        /// </remarks>
        /// <param name="a">The scalar float value to compare.</param>
        /// <param name="b">The functional tensor to compare with.</param>
        /// <returns>An integer functional tensor with `1` where the scalar is less than or equal to elements.</returns>
        public static FunctionalTensor operator <=(float a, FunctionalTensor b) => b >= a;

        /// <summary>
        /// Unary not operator.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise bitwise negation (complement) of the input tensor.
        /// For each element, all bits are inverted.
        /// This is equivalent to calling <see cref="Functional.BitwiseNot"/>.
        /// This operator is typically used with integer tensors.
        /// </remarks>
        /// <param name="a">The functional tensor to negate bitwise.</param>
        /// <returns>A functional tensor containing the bitwise NOT of each element.</returns>
        public static FunctionalTensor operator ~(FunctionalTensor a) => Functional.BitwiseNot(a);

        /// <summary>
        /// Xor operator.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise bitwise exclusive-or between two functional tensors.
        /// For each corresponding pair of elements, the result has bits set where the inputs differ.
        /// The tensors are broadcasted to a compatible shape if necessary.
        /// This is equivalent to calling <see cref="Functional.BitwiseXor"/>.
        /// </remarks>
        /// <param name="a">The first functional tensor.</param>
        /// <param name="b">The second functional tensor.</param>
        /// <returns>A functional tensor containing the bitwise XOR of the two inputs.</returns>
        public static FunctionalTensor operator ^(FunctionalTensor a, FunctionalTensor b) => Functional.BitwiseXor(a, b);

        /// <summary>
        /// Xor operator.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise bitwise XOR between the tensor and a boolean value.
        /// </remarks>
        /// <param name="a">The functional tensor.</param>
        /// <param name="b">The boolean value.</param>
        /// <returns>A functional tensor containing the bitwise XOR with the boolean.</returns>
        public static FunctionalTensor operator ^(FunctionalTensor a, bool b) => a ^ (b ? 1 : 0);

        /// <summary>
        /// Xor operator.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise bitwise XOR between every element in the tensor and a constant integer value.
        /// </remarks>
        /// <param name="a">The functional tensor.</param>
        /// <param name="b">The scalar integer value.</param>
        /// <returns>A functional tensor with each element XORed with the scalar.</returns>
        public static FunctionalTensor operator ^(FunctionalTensor a, int b) => a ^ Functional.Constant(b);

        /// <summary>
        /// Xor operator.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise bitwise XOR between a boolean value and the tensor.
        /// The boolean is converted to `1` (true) or `0` (false) before the operation.
        /// This overload supports commutative XOR (bool ^ tensor = tensor ^ bool).
        /// </remarks>
        /// <param name="a">The boolean value.</param>
        /// <param name="b">The functional tensor.</param>
        /// <returns>A functional tensor containing the bitwise XOR with the boolean.</returns>
        public static FunctionalTensor operator ^(bool a, FunctionalTensor b) => b ^ a;

        /// <summary>
        /// Xor operator.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise bitwise XOR between a constant integer value and every element in the tensor.
        /// This overload supports commutative XOR (int ^ tensor = tensor ^ int).
        /// </remarks>
        /// <param name="a">The scalar integer value.</param>
        /// <param name="b">The functional tensor.</param>
        /// <returns>A functional tensor with each element XORed with the scalar.</returns>
        public static FunctionalTensor operator ^(int a, FunctionalTensor b) => b ^ a;

        /// <summary>
        /// And operator.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise bitwise AND between two functional tensors.
        /// For each corresponding pair of elements, the result has bits set only where both inputs have bits set.
        /// The tensors are broadcasted to a compatible shape if necessary.
        /// This is equivalent to calling <see cref="Functional.BitwiseAnd"/>.
        /// </remarks>
        /// <param name="a">The first functional tensor.</param>
        /// <param name="b">The second functional tensor.</param>
        /// <returns>A functional tensor containing the bitwise AND of the two inputs.</returns>
        public static FunctionalTensor operator &(FunctionalTensor a, FunctionalTensor b) => Functional.BitwiseAnd(a, b);

        /// <summary>
        /// And operator.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise bitwise AND between the tensor and a boolean value.
        /// The boolean is converted to `1` (true) or `0` (false) before the operation.
        /// </remarks>
        /// <param name="a">The functional tensor.</param>
        /// <param name="b">The boolean value.</param>
        /// <returns>A functional tensor containing the bitwise AND with the boolean.</returns>
        public static FunctionalTensor operator &(FunctionalTensor a, bool b) => a & (b ? 1 : 0);

        /// <summary>
        /// And operator.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise bitwise AND between every element in the tensor and a constant integer value.
        /// </remarks>
        /// <param name="a">The functional tensor.</param>
        /// <param name="b">The scalar integer value.</param>
        /// <returns>A functional tensor with each element ANDed with the scalar.</returns>
        public static FunctionalTensor operator &(FunctionalTensor a, int b) => a & Functional.Constant(b);

        /// <summary>
        /// And operator.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise bitwise AND between a boolean value and the tensor.
        /// The boolean is converted to `1` (true) or `0` (false) before the operation.
        /// This overload supports commutative AND (bool &amp; tensor = tensor &amp; bool).
        /// </remarks>
        /// <param name="a">The boolean value.</param>
        /// <param name="b">The functional tensor.</param>
        /// <returns>A functional tensor containing the bitwise AND with the boolean.</returns>
        public static FunctionalTensor operator &(bool a, FunctionalTensor b) => b & a;

        /// <summary>
        /// And operator.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise bitwise AND between a constant integer value and every element in the tensor.
        /// This overload supports commutative AND (int &amp; tensor = tensor &amp; int).
        /// </remarks>
        /// <param name="a">The scalar integer value.</param>
        /// <param name="b">The functional tensor.</param>
        /// <returns>A functional tensor with each element ANDed with the scalar.</returns>
        public static FunctionalTensor operator &(int a, FunctionalTensor b) => b & a;

        /// <summary>
        /// Or operator.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise bitwise OR between two functional tensors.
        /// For each corresponding pair of elements, the result has bits set where either input has bits set.
        /// The tensors are broadcasted to a compatible shape if necessary.
        /// This is equivalent to calling <see cref="Functional.BitwiseOr"/>.
        /// </remarks>
        /// <param name="a">The first functional tensor.</param>
        /// <param name="b">The second functional tensor.</param>
        /// <returns>A functional tensor containing the bitwise OR of the two inputs.</returns>
        public static FunctionalTensor operator |(FunctionalTensor a, FunctionalTensor b) => Functional.BitwiseOr(a, b);

        /// <summary>
        /// Or operator.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise bitwise OR between the tensor and a boolean value.
        /// The boolean is converted to `1` (true) or `0` (false) before the operation.
        /// </remarks>
        /// <param name="a">The functional tensor.</param>
        /// <param name="b">The boolean value.</param>
        /// <returns>A functional tensor containing the bitwise OR with the boolean.</returns>
        public static FunctionalTensor operator |(FunctionalTensor a, bool b) => a | (b ? 1 : 0);

        /// <summary>
        /// Or operator.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise bitwise OR between every element in the tensor and a constant integer value.
        /// </remarks>
        /// <param name="a">The functional tensor.</param>
        /// <param name="b">The scalar integer value.</param>
        /// <returns>A functional tensor with each element ORed with the scalar.</returns>
        public static FunctionalTensor operator |(FunctionalTensor a, int b) => a | Functional.Constant(b);

        /// <summary>
        /// Or operator.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise bitwise OR between a boolean value and the tensor.
        /// The boolean is converted to `1` (true) or `0` (false) before the operation.
        /// This overload supports commutative OR (bool | tensor = tensor | bool).
        /// </remarks>
        /// <param name="a">The boolean value.</param>
        /// <param name="b">The functional tensor.</param>
        /// <returns>A functional tensor containing the bitwise OR with the boolean.</returns>
        public static FunctionalTensor operator |(bool a, FunctionalTensor b) => b | a;

        /// <summary>
        /// Or operator.
        /// </summary>
        /// <remarks>
        /// This operator performs element-wise bitwise OR between a constant integer value and every element in the tensor.
        /// This overload supports commutative OR (int | tensor = tensor | int).
        /// </remarks>
        /// <param name="a">The scalar integer value.</param>
        /// <param name="b">The functional tensor.</param>
        /// <returns>A functional tensor with each element ORed with the scalar.</returns>
        public static FunctionalTensor operator |(int a, FunctionalTensor b) => b | a;

        // helper for operators with float values
        static FunctionalTensor ScalarMad(FunctionalTensor input, float s, float b)
        {
            input = input.Float();
            return FunctionalLayer.ScalarMad(input, DataType.Float, s, b, 0, 0);
        }

        // helper for operators with int values, type promotion to floats if needed
        static FunctionalTensor ScalarMad(FunctionalTensor input, int s, int b)
        {
            if (input.dataType == DataType.Float)
                return ScalarMad(input, (float)s, b);
            return FunctionalLayer.ScalarMad(input, DataType.Int, 0, 0, s, b);
        }
    }
}
