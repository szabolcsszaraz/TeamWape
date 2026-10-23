using System.Collections.Generic;
using UnityEngine;

namespace Unity.GraphAuthoring.Editor.ProviderSystem
{
    internal static class HintUtils
    {
        internal static T GetHint<T>(this IReadOnlyCollection<IStrongHint> hints) where T : class, IStrongHint
        {
            if (hints == null) return null;
            foreach (var h in hints)
                if (h.GetType() == typeof(T)) return (T)h;
            return null;
        }

        internal static bool IsNumeric(string typeName)
        {
            return TryParseTypeInfo(typeName, out _, out _, out _, out _, out _, out _, out _);
        }

        internal static bool TryParseTypeInfo(
            string type,
            out string prim,
            out bool isScalar,
            out bool isVector,
            out bool isMatrix,
            out int rows,
            out int cols,
            out int length)
        {
            prim = null;
            isScalar = false;
            isVector = false;
            isMatrix = false;
            rows = -1;
            cols = -1;
            length = -1;

            // Arrays aren't supported yet.
            if (type.Contains('['))
                return false;

            if (type.StartsWith("uint")) prim = "uint";
            if (type.StartsWith("int")) prim = "int";
            if (type.StartsWith("bool")) prim = "bool";
            if (type.StartsWith("half")) prim = "half";
            if (type.StartsWith("float")) prim = "float";

            if (prim == null)
                return false;

            if (type == prim)
                return isScalar = true;

            var remainder = type.Substring(prim.Length);

            if (remainder.Length == 1)
            {
                return isVector = int.TryParse(remainder, out rows) && rows >= 1 && rows <= 4;
            }
            if (remainder.Length == 3)
            {
                return isMatrix
                    = int.TryParse(remainder.Substring(0, 1), out rows) && rows >= 1 && rows <= 4
                    && int.TryParse(remainder.Substring(2, 1), out cols) && cols >= 1 && cols <= 4;
            }
            return false;
        }

        internal static bool TryCast<T>(string shaderType, float[] values, out T value, T fallback = default)
        {
            try
            {
                return Cast<T>(shaderType, values, out value, fallback);
            }
            catch
            {
                value = default;
                return false;
            }
        }

        private static bool Cast<T>(string shaderType, float[] values, out T value, T fallback = default)
        {
            object obj = default;
            value = fallback;

            if (values == null || values.Length == 0)
                return false;

            float[] v = new float[16];
            for (int i = 0; i < values.Length; ++i)
                v[i] = values[i];

            if (!TryParseTypeInfo(shaderType, out string prim, out bool isScalar, out bool isVector, out bool isMatrix, out int rows, out int cols, out int length))
                return false;

            if (isScalar || isVector && rows == 1 || isMatrix && rows == 1 && cols == 1)
            {
                var fv = v[0];

                switch (prim)
                {
                    case "bool": obj = fv != 0; break;
                    case "uint": obj = fv; break;
                    case "int": obj = fv; break;
                    case "float":
                    case "half": obj = fv; break;
                    default: return false;
                }
            }
            if (isVector)
            {
                switch (rows)
                {
                    case 2: obj = new Vector2(v[0], v[1]); break;
                    case 3: obj = new Vector3(v[0], v[1], v[2]); break;
                    case 4: obj = new Vector4(v[0], v[1], v[2], v[3]); break;
                    default: return false;
                }
            }
            if (isMatrix)
            {
                Matrix4x4 m = new();
                for (int i = 0; i < cols * rows; ++i)
                {
                    m[i % rows + rows * (i / rows)] = v[i];
                }
            }

            value = (T)obj;
            return true;
        }

        internal static float[] LazyTokenFloat(string arg)
        {
            var tokens = LazyTokenize(arg);
            List<float> asfloat = new();

            foreach (var token in tokens)
                if (float.TryParse(token, out float r))
                    asfloat.Add(r);

            return asfloat.ToArray();
        }

        internal static string[] LazyTokenString(string arg)
        {
            List<string> tokens = new(LazyTokenize(arg));
            return tokens.ToArray();
        }

        private static IEnumerable<string> LazyTokenize(string arg)
        {
            foreach (var e in arg.Split(','))
            {
                var result = e.Trim();
                if (!string.IsNullOrWhiteSpace(result))
                    yield return e.Trim();
            }
        }
    }
}
