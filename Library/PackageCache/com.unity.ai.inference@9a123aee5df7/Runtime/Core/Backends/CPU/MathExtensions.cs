using Unity.Mathematics;
using UnityEngine;

namespace Unity.InferenceEngine
{
    /// <summary>
    /// Portable expm1 implementation that works on any Unity version.
    /// Returns e^x - 1 with good precision for small |x|.
    /// </summary>
    static class MathExtensions
    {
        public static float Expm1(float x)
        {
            if (math.abs(x) < 1e-5f)
                return x * (1.0f + x * (0.5f + x * (0.16666667f + x * (0.041666667f + x * (0.0083333333f + x * 0.0013888889f)))));

            return math.exp(x) - 1.0f;
        }

        /// <summary>
        /// Computes ln(1 + x) with good accuracy for all float x > -1.
        /// </summary>
        public static float Log1p(float x)
        {
            if (x <= -1f)
                return float.NaN;

            // Very small x → use a truncated Taylor series.
            // ln(1 + x) = x - x²/2 + x³/3 - x⁴/4 + x⁵/5 - …
            // Keeping terms up to x⁶ gives sub‑ULP accuracy for |x| < 1e‑4.
            if (math.abs(x) < 1e-4f)
                return x * (1 + x * (-0.5f + x * (0.33333334f + x * (-0.25f + x * (0.2f + x * 0.16666667f)))));

            // For the rest we can safely use the standard library.
            // Mathf.Log expects a positive argument; 1 + x is guaranteed > 0 here.
            return math.log(1f + x);
        }
    }
}
