using System;
using Unity.InferenceEngine.Layers;

namespace Unity.InferenceEngine.Graph
{
    /// <summary>
    /// Represents an argument of a node in the graph.
    /// </summary>
    class Argument
    {
        readonly string m_Value0;
        readonly int m_Value1;
        readonly float m_Value2;
        readonly bool m_Value3;
        readonly Argument[] m_Value4;
        readonly Node m_Value5;
        readonly int m_Index;

        Argument(string value)
        {
            m_Value0 = value;
            m_Index = 0;
        }

        Argument(int value)
        {
            m_Value1 = value;
            m_Index = 1;
        }

        Argument(float value)
        {
            m_Value2 = value;
            m_Index = 2;
        }

        Argument(bool value)
        {
            m_Value3 = value;
            m_Index = 3;
        }

        Argument(Argument[] value)
        {
            m_Value4 = value;
            m_Index = 4;
        }

        Argument(Node value)
        {
            m_Value5 = value;
            m_Index = 5;
        }

        public Argument(string[] values)
        {
            var value = new Argument[values.Length];
            for (var i = 0; i < values.Length; i++)
                value[i] = new Argument(values[i]);
            m_Value4 = value;
            m_Index = 4;
        }

        public Argument(int[] values)
        {
            var value = new Argument[values.Length];
            for (var i = 0; i < values.Length; i++)
                value[i] = new Argument(values[i]);
            m_Value4 = value;
            m_Index = 4;
        }

        public Argument(float[] values)
        {
            var value = new Argument[values.Length];
            for (var i = 0; i < values.Length; i++)
                value[i] = new Argument(values[i]);
            m_Value4 = value;
            m_Index = 4;
        }

        public Argument(bool[] values)
        {
            var value = new Argument[values.Length];
            for (var i = 0; i < values.Length; i++)
                value[i] = new Argument(values[i]);
            m_Value4 = value;
            m_Index = 4;
        }

        public Argument(Node[] values)
        {
            var value = new Argument[values.Length];
            for (var i = 0; i < values.Length; i++)
                value[i] = new Argument(values[i]);
            m_Value4 = value;
            m_Index = 4;
        }

        public int Index => m_Index;

        public bool IsString => m_Index == 0;
        public bool IsInt => m_Index == 1;
        public bool IsFloat => m_Index == 2;
        public bool IsBool => m_Index == 3;
        public bool IsArguments => m_Index == 4;
        public bool IsNode => m_Index == 5;

        public string AsString => m_Index == 0 ? m_Value0 : throw new InvalidOperationException($"Cannot return as string as result is T{m_Index}");
        public int AsInt => m_Index == 1 ? m_Value1 : throw new InvalidOperationException($"Cannot return as int as result is T{m_Index}");
        public float AsFloat =>
            m_Index == 2 ? m_Value2 : throw new InvalidOperationException($"Cannot return as float as result is T{m_Index}");
        public bool AsBool =>
            m_Index == 3 ? m_Value3 : throw new InvalidOperationException($"Cannot return as bool as result is T{m_Index}");
        public Argument[] AsArguments =>
            m_Index == 4 ? m_Value4 : throw new InvalidOperationException($"Cannot return as Argument[] as result is T{m_Index}");
        public Node AsNode =>
            m_Index == 5 ? m_Value5 : throw new InvalidOperationException($"Cannot return as Node as result is T{m_Index}");

        public int[] AsIntArray
        {
            get
            {
                var args = AsArguments;
                var values = new int[args.Length];
                for (var i = 0; i < values.Length; i++)
                    values[i] = args[i].AsInt;
                return values;
            }
        }

        public float[] AsFloatArray
        {
            get
            {
                var args = AsArguments;
                var values = new float[args.Length];
                for (var i = 0; i < values.Length; i++)
                    values[i] = args[i].AsFloat;
                return values;
            }
        }

        public Node[] AsNodeArray
        {
            get
            {
                var args = AsArguments;
                var values = new Node[args.Length];
                for (var i = 0; i < values.Length; i++)
                    values[i] = args[i].AsNode;
                return values;
            }
        }

        public static implicit operator Argument(string _) => _ == null ? null : new Argument(_);
        public static explicit operator string(Argument _) => _.AsString;

        public static implicit operator Argument(int _) => new(_);
        public static explicit operator int(Argument _) => _.AsInt;

        public static implicit operator Argument(bool _) => new(_);
        public static explicit operator bool(Argument _) => _.AsBool;

        public static implicit operator Argument(float _) => new(_);
        public static explicit operator float(Argument _) => _.AsFloat;

        public static implicit operator Argument(Argument[] _) => _ == null ? null : new Argument(_);
        public static explicit operator Argument[](Argument _) => _.AsArguments;

        public static implicit operator Argument(Node _) => _ == null ? null : new Argument(_);
        public static explicit operator Node(Argument _) => _?.AsNode;

        public static implicit operator Argument(int[] _) => _ == null ? null : new Argument(_);
        public static explicit operator int[](Argument _) => _?.AsIntArray;

        public static implicit operator Argument(float[] _) => _ == null ? null : new Argument(_);
        public static explicit operator float[](Argument _) => _?.AsFloatArray;

        public static implicit operator Argument(Node[] _) => _ == null ? null : new Argument(_);
        public static explicit operator Node[](Argument _) => _?.AsNodeArray;

        public static implicit operator Argument(DataType _) => (int)_;
        public static explicit operator DataType(Argument _) => (DataType)_.AsInt;

        // TODO let's think about removing uses of enums for our internal representation, this isn't very sustainable to have all these constructors.

        public static implicit operator Argument(FusableActivation _) => (int)_;
        public static explicit operator FusableActivation(Argument _) => (FusableActivation)_.AsInt;

        public static implicit operator Argument(AutoPad _) => (int)_;
        public static explicit operator AutoPad(Argument _) => (AutoPad)_.AsInt;

        public static implicit operator Argument(ScatterReductionMode _) => (int)_;
        public static explicit operator ScatterReductionMode(Argument _) => (ScatterReductionMode)_.AsInt;

        public static implicit operator Argument(CenterPointBox _) => (int)_;
        public static explicit operator CenterPointBox(Argument _) => (CenterPointBox)_.AsInt;

        public static implicit operator Argument(RoiPoolingMode _) => (int)_;
        public static explicit operator RoiPoolingMode(Argument _) => (RoiPoolingMode)_.AsInt;

        public static implicit operator Argument(RoiCoordinateTransformationMode _) => (int)_;
        public static explicit operator RoiCoordinateTransformationMode(Argument _) => (RoiCoordinateTransformationMode)_.AsInt;

        public static implicit operator Argument(RnnDirection _) => (int)_;
        public static explicit operator RnnDirection(Argument _) => (RnnDirection)_.AsInt;

        public static implicit operator Argument(RnnActivation _) => (int)_;
        public static explicit operator RnnActivation(Argument _) => (RnnActivation)_.AsInt;

        public static implicit operator Argument(RnnLayout _) => (int)_;
        public static explicit operator RnnLayout(Argument _) => (RnnLayout)_.AsInt;

        public static implicit operator Argument(PadMode _) => (int)_;
        public static explicit operator PadMode(Argument _) => (PadMode)_.AsInt;

        public static implicit operator Argument(ScaleMode _) => (int)_;
        public static explicit operator ScaleMode(Argument _) => (ScaleMode)_.AsInt;

        public static implicit operator Argument(InterpolationMode _) => (int)_;
        public static explicit operator InterpolationMode(Argument _) => (InterpolationMode)_.AsInt;

        public static implicit operator Argument(NearestMode _) => (int)_;
        public static explicit operator NearestMode(Argument _) => (NearestMode)_.AsInt;

        public static implicit operator Argument(PaddingMode _) => (int)_;
        public static explicit operator PaddingMode(Argument _) => (PaddingMode)_.AsInt;

        public static implicit operator Argument(CoordTransformMode _) => (int)_;
        public static explicit operator CoordTransformMode(Argument _) => (CoordTransformMode)_.AsInt;

        public static implicit operator Argument(TriluMode _) => (int)_;
        public static explicit operator TriluMode(Argument _) => (TriluMode)_.AsInt;

        public static implicit operator Argument(DepthToSpaceMode _) => (int)_;
        public static explicit operator DepthToSpaceMode(Argument _) => (DepthToSpaceMode)_.AsInt;

        public static implicit operator Argument(RnnActivation[] v)
        {
            if (v == null)
                return null;
            var arguments = new Argument[v.Length];
            for (var i = 0; i < arguments.Length; i++)
                arguments[i] = (int)v[i];
            return arguments;
        }
        public static explicit operator RnnActivation[](Argument v)
        {
            if (v == null)
                return null;
            var activations = new RnnActivation[v.AsArguments.Length];
            for (var i = 0; i < activations.Length; i++)
                activations[i] = (RnnActivation)v.AsArguments[i];
            return activations;
        }

        public static implicit operator Argument(FunctionalTensor v) => v == null ? null : new Argument(new FakeNode(v));

        public static implicit operator Argument(FunctionalTensor[] v)
        {
            if (v == null)
                return null;
            var arguments = new Argument[v.Length];
            for (var i = 0; i < arguments.Length; i++)
                arguments[i] = v[i];
            return arguments;
        }

        // This does not iterate through argument arrays and compares nodes by reference, write custom equals calculation if more is required.
        public bool Equals(Argument other) =>
            m_Index == other.m_Index &&
            m_Index switch
            {
                0 => Equals(m_Value0, other.m_Value0),
                1 => Equals(m_Value1, other.m_Value1),
                2 => Equals(m_Value2, other.m_Value2),
                3 => Equals(m_Value3, other.m_Value3),
                4 => Equals(m_Value4, other.m_Value4),
                5 => Equals(m_Value5, other.m_Value5),
                _ => false
            };

        // This does not iterate through argument arrays, write custom hash code calculation if that is required.
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = m_Index switch
                {
                    0 => m_Value0?.GetHashCode(),
                    1 => m_Value1.GetHashCode(),
                    2 => m_Value2.GetHashCode(),
                    3 => m_Value3.GetHashCode(),
                    4 => m_Value4?.GetHashCode(),
                    5 => m_Value5?.GetHashCode(),
                    _ => 0
                } ?? 0;
                return HashCode.Combine(hashCode, m_Index);
            }
        }
    }
}
