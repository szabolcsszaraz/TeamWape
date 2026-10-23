using System;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.InferenceEngine
{
    enum ElementType
    {
        Unknown = 0,
        Value,
        Param
    }

    /// <summary>
    /// Represents a single element of a Partial Tensor, can be an int value, float value, byte param or unknown
    /// </summary>
    struct PartialTensorElement<T> where T : unmanaged
    {
        const string k_UnknownName = "?";

        ElementType m_ElementType;
        byte m_Param;
        T m_Value;

        public static PartialTensorElement<T> Unknown => new PartialTensorElement<T>();

        public static PartialTensorElement<T> Value(T value)
        {
            return new PartialTensorElement<T>
            {
                m_ElementType = ElementType.Value,
                m_Param = default,
                m_Value = value,
            };
        }

        public static PartialTensorElement<T> Param(byte param)
        {
            return new PartialTensorElement<T>
            {
                m_ElementType = ElementType.Param,
                m_Param = param,
                m_Value = default
            };
        }

        public bool isUnknown => m_ElementType == ElementType.Unknown;
        public bool isValue => m_ElementType == ElementType.Value;
        public bool isParam => m_ElementType == ElementType.Param;

        public static PartialTensorElement<T> Zero
        {
            get
            {
                if (typeof(T) == typeof(int))
                    return Value((T)Convert.ChangeType(0, typeof(T)));
                if (typeof(T) == typeof(float))
                    return Value((T)Convert.ChangeType(0f, typeof(T)));
                throw new NotImplementedException();
            }
        }

        public static PartialTensorElement<T> One
        {
            get
            {
                if (typeof(T) == typeof(int))
                    return Value((T)Convert.ChangeType(1, typeof(T)));
                if (typeof(T) == typeof(float))
                    return Value((T)Convert.ChangeType(1f, typeof(T)));
                throw new NotImplementedException();
            }
        }

        public T value
        {
            get
            {
                Logger.AssertIsTrue(m_ElementType == ElementType.Value, "Cannot get value of element with type != ElementType.Value");
                return m_Value;
            }
        }

        public byte param
        {
            get
            {
                Logger.AssertIsTrue(m_ElementType == ElementType.Param, "Cannot get param of element with type != ElementType.Param");
                return m_Param;
            }
        }

        public PartialTensorElement<T1> Cast<T1>() where T1 : unmanaged
        {
            if (isUnknown)
                return PartialTensorElement<T1>.Unknown;
            if (isParam)
                return PartialTensorElement<T1>.Param(m_Param);
            return PartialTensorElement<T1>.Value((T1)Convert.ChangeType(m_Value, typeof(T1)));
        }

        /// <summary>
        /// Returns a string that represents the `PartialTensorElement`.
        /// </summary>
        public override string ToString()
        {
            return m_ElementType switch
            {
                ElementType.Unknown => "?",
                ElementType.Value => value.ToString(),
                ElementType.Param => param.ToString(),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        internal string ToString(Func<byte, string> SymbolicDimNaming)
        {
            return m_ElementType switch
            {
                ElementType.Unknown => k_UnknownName,
                ElementType.Value => value.ToString(),
                ElementType.Param => SymbolicDimNaming(param),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public bool Equals(T other)
        {
            return m_ElementType == ElementType.Value && Equals(m_Value, other);
        }

        public bool Equals(PartialTensorElement<T> other)
        {
            return m_ElementType == other.m_ElementType && Equals(m_Value, other.m_Value) && m_Param == other.m_Param;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current `PartialTensorElement`.
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is PartialTensorElement<T> other && Equals(other);
        }

        /// <summary>
        ///
        /// Compares element to element
        /// ==
        ///   | 0   1   A   B   ?
        /// --|---------------------
        /// 0 | T   F   F   F   F
        /// 3 | F   F   F   F   F
        /// A | F   F   T   F   F
        /// ? | F   F   F   F   F
        ///
        /// </summary>
        public static bool operator ==(PartialTensorElement<T> a, PartialTensorElement<T> b)
        {
            if (a.isValue && b.isValue)
                return Eq(a.value, b.value);
            if (a.isParam && b.isParam)
                return a.param == b.param;
            return false;
        }

        /// <summary>
        ///
        /// Compares element to element
        /// !=
        ///   | 0   1   A   B   ?
        /// --|---------------------
        /// 0 | F   T   F   F   F
        /// 3 | T   T   F   F   F
        /// A | F   F   F   F   F
        /// ? | F   F   F   F   F
        ///
        /// </summary>
        public static bool operator !=(PartialTensorElement<T> a, PartialTensorElement<T> b)
        {
            if (a.isValue && b.isValue)
                return !Eq(a.value, b.value);
            return false;
        }

        public static bool operator >=(PartialTensorElement<T> a, PartialTensorElement<T> b)
        {
            if (a.isValue && b.isValue)
                return Ge(a.value, b.value);
            return a == b;
        }

        public static bool operator <=(PartialTensorElement<T> a, PartialTensorElement<T> b)
        {
            if (a.isValue && b.isValue)
                return Le(a.value, b.value);
            return a == b;
        }

        public static bool operator >(PartialTensorElement<T> a, PartialTensorElement<T> b)
        {
            if (a.isValue && b.isValue)
                return Gt(a.value, b.value);
            return false;
        }

        public static bool operator <(PartialTensorElement<T> a, PartialTensorElement<T> b)
        {
            if (a.isValue && b.isValue)
                return Lt(a.value, b.value);
            return false;
        }

        public static PartialTensorElement<T> operator &(PartialTensorElement<T> a, PartialTensorElement<T> b)
        {
            if (a == b)
                return a;
            if (a == Zero || b == Zero)
                return Zero;
            if (a.isValue && b.isValue)
                return Value(BitwiseAnd(a.value, b.value));
            return Unknown;
        }

        public static PartialTensorElement<T> operator |(PartialTensorElement<T> a, PartialTensorElement<T> b)
        {
            if (a == b)
                return a;
            if (a == Zero)
                return b;
            if (b == Zero)
                return a;
            if (a.isValue && b.isValue)
                return Value(BitwiseOr(a.value, b.value));
            return Unknown;
        }

        public static PartialTensorElement<T> operator ^(PartialTensorElement<T> a, PartialTensorElement<T> b)
        {
            if (a == b)
                return Zero;
            if (a == Zero)
                return b;
            if (b == Zero)
                return a;
            if (a.isValue && b.isValue)
                return Value(BitwiseXor(a.value, b.value));
            return Unknown;
        }

        public static PartialTensorElement<T> operator ~(PartialTensorElement<T> a)
        {
            if (a.isValue)
                return Value(BitwiseNot(a.value));
            return Unknown;
        }

        /// <summary>
        ///
        /// Negates element
        ///
        ///   | 0   1   A   B   ?
        /// --|---------------------
        ///   | 0   -1  ?   ?   ?
        ///
        /// </summary>
        public static PartialTensorElement<T> operator -(PartialTensorElement<T> a)
        {
            if (a.isValue)
                return Value(Neg(a.value));
            return Unknown;
        }

        /// <summary>
        ///
        /// Adds element to element
        ///
        ///   | 0   1   A   B   ?
        /// --|---------------------
        /// 0 | 0   1   A   B   ?
        /// 3 | 3   4   ?   ?   ?
        /// A | A   ?   ?   ?   ?
        /// ? | ?   ?   ?   ?   ?
        ///
        /// </summary>
        public static PartialTensorElement<T> operator +(PartialTensorElement<T> a, PartialTensorElement<T> b)
        {
            if (a.isValue && b.isValue)
                return Value(Add(a.value, b.value));
            if (a == Zero)
                return b;
            if (b == Zero)
                return a;
            return Unknown;
        }

        /// <summary>
        ///
        /// Subtracts element from element
        ///
        ///   | 0   1   A   B   ?
        /// --|---------------------
        /// 3 | 3   2   ?   ?   ?
        /// A | A   ?   0   ?   ?
        /// ? | ?   ?   ?   ?   ?
        ///
        /// </summary>
        public static PartialTensorElement<T> operator -(PartialTensorElement<T> a, PartialTensorElement<T> b)
        {
            if (a.isValue && b.isValue)
                return Value(Sub(a.value, b.value));
            if (b == Zero)
                return a;
            if (a == b)
                return Zero;
            return Unknown;
        }

        /// <summary>
        /// Multiplies element by element
        ///
        ///   | 0   1   3   A   B   ?
        /// --|-----------------------
        /// 0 | 0   0   0   0   0   0
        /// 2 | 0   2   6   ?   ?   ?
        /// A | 0   A   ?   ?   ?   ?
        /// ? | 0   ?   ?   ?   ?   ?
        ///
        /// </summary>
        public static PartialTensorElement<T> operator *(PartialTensorElement<T> a, PartialTensorElement<T> b)
        {
            if (a.isValue && b.isValue)
                return Value(Mul(a.value, b.value));
            if (a == Zero || b == Zero)
                return Zero;
            if (a == One)
                return b;
            if (b == One)
                return a;
            return Unknown;
        }

        /// <summary>
        /// Divides element by element
        ///
        ///   |  0    1   2   A   B   ?
        /// --|-------------------------
        /// 0 | err   0   0   0   0   0
        /// 6 | err   6   3   ?   ?   ?
        /// A | err   A   ?   1   ?   ?
        /// ? | err   ?   ?   ?   ?   ?
        ///
        /// </summary>
        public static PartialTensorElement<T> operator /(PartialTensorElement<T> a, PartialTensorElement<T> b)
        {
            if (b == Zero)
                throw new DivideByZeroException();
            if (a.isValue && b.isValue)
                return Value(Div(a.value, b.value));
            if (a == Zero)
                return Zero;
            if (b == One)
                return a;
            if (a == b)
                return One;
            return Unknown;
        }

        public static PartialTensorElement<T> Sign(PartialTensorElement<T> a)
        {
            if (a.isValue)
                return Value(Sign(a.value));
            return Unknown;
        }

        public static PartialTensorElement<T> Pow(PartialTensorElement<T> a, PartialTensorElement<T> b)
        {
            if (a.isValue && b.isValue)
                return Value(Pow(a.value, b.value));
            if (b == Zero)
                return One;
            if (b == One)
                return a;
            return Unknown;
        }

        public static PartialTensorElement<T> Mod(PartialTensorElement<T> a, PartialTensorElement<T> b)
        {
            if (a.isValue && b.isValue)
                return Value(Mod(a.value, b.value));
            if (a == Zero)
                return Zero;
            return Unknown;
        }

        public static PartialTensorElement<T> FMod(PartialTensorElement<T> a, PartialTensorElement<T> b)
        {
            if (a.isValue && b.isValue)
                return Value(FMod(a.value, b.value));
            if (a == Zero)
                return Zero;
            return Unknown;
        }

        /// <summary>
        /// Returns the better known of two elements known to be equal, throws error if both elements are values and not equal
        ///
        ///   | 2   3   A   B   ?
        /// --|-------------------
        /// 2 | 2  Err  2   2   2
        /// A | 2   3   A   A   A
        /// ? | 2   3   A   B   ?
        ///
        /// </summary>
        public static PartialTensorElement<T> MaxDefinedElement(PartialTensorElement<T> a, PartialTensorElement<T> b)
        {
            if (a.isUnknown)
                return b;
            if (b.isUnknown)
                return a;
            if (b.isValue)
            {
                Logger.AssertIsTrue(!a.isValue || b == a, "ValueError: value elements must be equal");
                return b;
            }

            return a;
        }

        public static PartialTensorElement<T> Min(PartialTensorElement<T> a, PartialTensorElement<T> b)
        {
            if (a.isValue && b.isValue)
                return Value(Min(a.value, b.value));
            if (a == b)
                return a;
            return Unknown;
        }

        public static PartialTensorElement<T> Max(PartialTensorElement<T> a, PartialTensorElement<T> b)
        {
            if (a.isValue && b.isValue)
                return Value(Max(a.value, b.value));
            if (a == b)
                return a;
            return Unknown;
        }

        public static PartialTensorElement<int> Eq(PartialTensorElement<T> a, PartialTensorElement<T> b)
        {
            if (a == b)
                return PartialTensorElement<int>.Value(1);
            if (a != b)
                return PartialTensorElement<int>.Value(0);
            return PartialTensorElement<int>.Unknown;
        }

        public static PartialTensorElement<int> Ne(PartialTensorElement<T> a, PartialTensorElement<T> b)
        {
            if (a != b)
                return PartialTensorElement<int>.Value(1);
            if (a == b)
                return PartialTensorElement<int>.Value(0);
            return PartialTensorElement<int>.Unknown;
        }

        public static PartialTensorElement<int> Gt(PartialTensorElement<T> a, PartialTensorElement<T> b)
        {
            if (a.isValue && b.isValue)
                return PartialTensorElement<int>.Value(Gt(a.value, b.value) ? 1 : 0);
            if (a == b)
                return PartialTensorElement<int>.Value(0);
            return PartialTensorElement<int>.Unknown;
        }

        public static PartialTensorElement<int> Ge(PartialTensorElement<T> a, PartialTensorElement<T> b)
        {
            if (a.isValue && b.isValue)
                return PartialTensorElement<int>.Value(Ge(a.value, b.value) ? 1 : 0);
            if (a == b)
                return PartialTensorElement<int>.Value(1);
            return PartialTensorElement<int>.Unknown;
        }

        public static PartialTensorElement<int> Lt(PartialTensorElement<T> a, PartialTensorElement<T> b) => Gt(b, a);

        public static PartialTensorElement<int> Le(PartialTensorElement<T> a, PartialTensorElement<T> b) => Ge(b, a);

        static bool Eq(T a, T b)
        {
            if (a is int aInt && b is int bInt)
                return aInt == bInt;
            if (a is float aFloat && b is float bFloat)
                return aFloat == bFloat;
            throw new ArgumentException($"Cannot compare {a} to {b}.");
        }

        static bool Gt(T a, T b)
        {
            if (a is int aInt && b is int bInt)
                return aInt > bInt;
            if (a is float aFloat && b is float bFloat)
                return aFloat > bFloat;
            throw new ArgumentException($"Cannot compare {a} to {b}.");
        }

        static bool Ge(T a, T b)
        {
            if (a is int aInt && b is int bInt)
                return aInt >= bInt;
            if (a is float aFloat && b is float bFloat)
                return aFloat >= bFloat;
            throw new ArgumentException($"Cannot compare {a} to {b}.");
        }

        static bool Lt(T a, T b) => Gt(b, a);

        static bool Le(T a, T b) => Ge(b, a);

        static T BitwiseAnd(T a, T b)
        {
            if (a is int aInt && b is int bInt)
                return (T)Convert.ChangeType(aInt & bInt, typeof(T));
            throw new ArgumentException($"Cannot bitwise and {a} and {b}.");
        }

        static T BitwiseOr(T a, T b)
        {
            if (a is int aInt && b is int bInt)
                return (T)Convert.ChangeType(aInt | bInt, typeof(T));
            throw new ArgumentException($"Cannot bitwise or {a} and {b}.");
        }

        static T BitwiseXor(T a, T b)
        {
            if (a is int aInt && b is int bInt)
                return (T)Convert.ChangeType(aInt ^ bInt, typeof(T));
            throw new ArgumentException($"Cannot bitwise or {a} and {b}.");
        }

        static T BitwiseNot(T a)
        {
            if (a is int aInt)
                return (T)Convert.ChangeType(~aInt, typeof(T));
            throw new ArgumentException($"Cannot bitwise not {a}.");
        }

        static T Mul(T a, T b)
        {
            if (a is int aInt && b is int bInt)
                return (T)Convert.ChangeType(aInt * bInt, typeof(T));
            if (a is float aFloat && b is float bFloat)
                return (T)Convert.ChangeType(aFloat * bFloat, typeof(T));
            throw new ArgumentException($"Cannot mul {a} and {b}.");
        }

        static T Div(T a, T b)
        {
            if (a is int aInt && b is int bInt)
                return (T)Convert.ChangeType(aInt / bInt, typeof(T));
            if (a is float aFloat && b is float bFloat)
                return (T)Convert.ChangeType(aFloat / bFloat, typeof(T));
            throw new ArgumentException($"Cannot mul {a} and {b}.");
        }

        static T Sign(T a)
        {
            if (a is int aInt)
                return (T)Convert.ChangeType((int)Mathf.Sign(aInt), typeof(T));
            if (a is float aFloat)
                return (T)Convert.ChangeType(Mathf.Sign(aFloat), typeof(T));
            throw new ArgumentException($"Cannot neg {a}.");
        }

        static T Neg(T a)
        {
            if (a is int aInt)
                return (T)Convert.ChangeType(-aInt, typeof(T));
            if (a is float aFloat)
                return (T)Convert.ChangeType(-aFloat, typeof(T));
            throw new ArgumentException($"Cannot neg {a}.");
        }

        static T Add(T a, T b)
        {
            if (a is int aInt && b is int bInt)
                return (T)Convert.ChangeType(aInt + bInt, typeof(T));
            if (a is float aFloat && b is float bFloat)
                return (T)Convert.ChangeType(aFloat + bFloat, typeof(T));
            throw new ArgumentException($"Cannot add {a} and {b}.");
        }

        static T Sub(T a, T b)
        {
            if (a is int aInt && b is int bInt)
                return (T)Convert.ChangeType(aInt - bInt, typeof(T));
            if (a is float aFloat && b is float bFloat)
                return (T)Convert.ChangeType(aFloat - bFloat, typeof(T));
            throw new ArgumentException($"Cannot sub {a} and {b}.");
        }

        static T Max(T a, T b)
        {
            if (a is int aInt && b is int bInt)
                return (T)Convert.ChangeType(Mathf.Max(aInt, bInt), typeof(T));
            if (a is float aFloat && b is float bFloat)
                return (T)Convert.ChangeType(Mathf.Max(aFloat, bFloat), typeof(T));
            throw new ArgumentException($"Cannot max {a} and {b}.");
        }

        static T Min(T a, T b)
        {
            if (a is int aInt && b is int bInt)
                return (T)Convert.ChangeType(Mathf.Min(aInt, bInt), typeof(T));
            if (a is float aFloat && b is float bFloat)
                return (T)Convert.ChangeType(Mathf.Min(aFloat, bFloat), typeof(T));
            throw new ArgumentException($"Cannot min {a} and {b}.");
        }

        static T Pow(T a, T b)
        {
            if (a is int aInt && b is int bInt)
                return (T)Convert.ChangeType(Mathf.Pow(aInt, bInt), typeof(T));
            if (a is float aFloat && b is float bFloat)
                return (T)Convert.ChangeType(Mathf.Pow(aFloat, bFloat), typeof(T));
            throw new ArgumentException($"Cannot pow {a} and {b}.");
        }

        static T Mod(T a, T b)
        {
            if (a is int aInt && b is int bInt)
                return (T)Convert.ChangeType((aInt % bInt + bInt) % bInt, typeof(T));
            if (a is float aFloat && b is float bFloat)
                return (T)Convert.ChangeType((aFloat % bFloat + bFloat) % bFloat, typeof(T));
            throw new ArgumentException($"Cannot pow {a} and {b}.");
        }

        static T FMod(T a, T b)
        {
            if (a is int aInt && b is int bInt)
                return (T)Convert.ChangeType(aInt % bInt, typeof(T));
            if (a is float aFloat && b is float bFloat)
                return (T)Convert.ChangeType(math.fmod(aFloat, bFloat), typeof(T));
            throw new ArgumentException($"Cannot pow {a} and {b}.");
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(m_ElementType, m_Param, m_Value);
        }
    }
}
