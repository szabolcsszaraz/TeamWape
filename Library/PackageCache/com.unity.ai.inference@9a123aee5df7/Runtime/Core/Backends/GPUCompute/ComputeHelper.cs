using System;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("Unity.InferenceEngine.RuntimeTests")]
[assembly: InternalsVisibleTo("Unity.InferenceEngine.EditorTests")]

namespace Unity.InferenceEngine
{
    static class ComputeHelper
    {
        public const uint SafeDispatchLimit = 65535;

        public static int IDivC(int v, int div)
        {
            return (v + div - 1) / div;
        }

        /// <summary>
        /// Policy to decide the (log2) width of a texture given the number of pixels required
        /// </summary>
        public static int CalculateWidthShift(int numPixels)
        {
            // aim for square, this seems to be faster than using very thin textures (width/height 1 or 2)
            // variant of base 2 bit counting from https://stackoverflow.com/questions/8970101/whats-the-quickest-way-to-compute-log2-of-an-integer-in-c
            var n = numPixels;
            var shift = 0;

            if (n > 0x7FFF)
            {
                n >>= 16;
                shift = 0x8;
            }

            if (n > 0x7F)
            {
                n >>= 8;
                shift |= 0x4;
            }

            if (n > 0x7)
            {
                n >>= 4;
                shift |= 0x2;
            }

            if (n > 0x1)
            {
                shift |= 0x1;
            }

            return shift;
        }

        // Stein's algo, from https://stackoverflow.com/questions/22281661/what-is-the-fastest-way-to-find-the-gcd-of-two-numbers
        public static int GCD(int a, int b)
        {
            // gcd(0, b) == b
            // gcd(a, 0) == a
            // gcd(0, 0) == 0
            if (a == 0)
                return b;
            if (b == 0)
                return a;

            // Finding k, where k is the greatest power of 2 that divides both a and b:
            int k;
            for (k = 0; ((a | b) & 1) == 0; k++)
            {
                a >>= 1;
                b >>= 1;
            }

            // Divide a by 2 until a becomes odd:
            while ((a & 1) == 0)
                a >>= 1;

            // From here on, 'a' is always odd:
            do
            {
                // If b is even, remove all factor of 2 in b
                while ((b & 1) == 0)
                    b >>= 1;

                // Now a and b are both odd.
                // Swap if necessary so a <= b,
                // then set b = b - a (which is even).
                if (a > b)
                {
                    // Swap u and v.
                    int tmp = a;
                    a = b;
                    b = tmp;
                }
                b = (b - a);
            } while (b != 0);

            // restore common factors of 2
            return a << k;
        }

        public static void SetTexture(this ComputeFunction fn, int nameID, Texture tex)
        {
            fn.shader.SetTexture(fn.kernelIndex, nameID, tex);
        }
        public static void SetInt(this ComputeFunction fn, int nameID, int data)
        {
            fn.shader.SetInt(nameID, data);
        }
        public static void SetVector(this ComputeFunction fn, int nameID, Vector4 data)
        {
            fn.shader.SetVector(nameID, data);
        }
        public static void SetTensorAsBuffer(this ComputeFunction fn, int bufferID, ComputeTensorData tensorData)
        {
            fn.shader.SetBuffer(fn.kernelIndex, bufferID, tensorData.buffer);
        }
        public static void Dispatch(this ComputeFunction fn, int workItemsX, int workItemsY, int workItemsZ)
        {
            fn.profilerMarker.Begin();

            var x = IDivC(workItemsX, (int)fn.threadGroupSizeX);
            var y = IDivC(workItemsY, (int)fn.threadGroupSizeY);
            var z = IDivC(workItemsZ, (int)fn.threadGroupSizeZ);

            // some GFX APIs / GPU hw/drivers have limitation of 65535 per dimension
            if (x > ComputeHelper.SafeDispatchLimit || y > ComputeHelper.SafeDispatchLimit || z > ComputeHelper.SafeDispatchLimit)
                D.LogWarning($"Exceeded safe compute dispatch group count limit per dimension [{x}, {y}, {z}] for {fn.shader.ToString()}");

            fn.shader.Dispatch(fn.kernelIndex, x, y, z);

            fn.profilerMarker.End();
        }
    }
}
