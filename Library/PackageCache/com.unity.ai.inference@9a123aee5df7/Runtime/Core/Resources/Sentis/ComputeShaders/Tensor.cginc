// Disable Integer divides might be much slower, try using uints if possible.
#pragma warning (disable: 3556)

#define MAX_THREAD_COUNT 64
#if (MAX_THREAD_COUNT>=256)
#define NUMTHREADS(t256,t128,t64) [numthreads t256]
#define NUMTHREAD(t256, t128, t64) t256
#elif (MAX_THREAD_COUNT>=128)
#define NUMTHREADS(t256,t128,t64) [numthreads t128]
#define NUMTHREAD(t256,t128,t64) t128
#elif (MAX_THREAD_COUNT>=64)
#define NUMTHREADS(t256,t128,t64) [numthreads t64]
#define NUMTHREAD(t256,t128,t64) t64
#endif

//Simulate C# pow(x<0, n is int) to avoid NaNs on GPU
//https://docs.microsoft.com/en-us/windows/win32/direct3dhlsl/dx-graphics-hlsl-pow
//https://docs.microsoft.com/en-us/dotnet/api/system.math.pow?view=net-6.0
float SignedPow(float f, float e)
{
    // handle negative f
    float v = pow(abs(f), e);
    float sign_f = sign(f);
    float s = (abs(e % 2) == 1) ?
        sign_f:  // exponent is odd  => sign(f) * pow(abs(f), e)
        1;       // exponent is even => pow(abs(f), e)

#if UNITY_PLATFORM_ANDROID
    float factor = 1.0;
    if (frac(e) != 0 && sign_f < 0.0)
        factor = asfloat(0x7FC00000);
#else
    float factor = frac(e) != 0 ? sqrt(sign_f) : 1.0; // generate a NaN if sign is -1.0
#endif
    return v * s * factor;
}


float4 SignedPow(float4 A, float4 B)
{
    float4 O;
    O.x = SignedPow(A.x, B.x);
    O.y = SignedPow(A.y, B.y);
    O.z = SignedPow(A.z, B.z);
    O.w = SignedPow(A.w, B.w);
    return O;
}

int SignedPowInt(int f, int e)
{
    // handle negative f
    float v = pow(abs(f), e);
    float s = (abs(e % 2) == 1) ?
        sign(f):    // exponent is odd  => sign(f) * pow(abs(f), e)
        1;          // exponent is even => pow(abs(f), e)
    return round(v * s);
}

int4 SignedPowInt(int4 A, int4 B)
{
    int4 O;
    O.x = SignedPowInt(A.x, B.x);
    O.y = SignedPowInt(A.y, B.y);
    O.z = SignedPowInt(A.z, B.z);
    O.w = SignedPowInt(A.w, B.w);
    return O;
}

float expm1(float x)
{
    const float THRESH = 1e-5f;

    if (abs(x) < THRESH)
    {
        // e^x - 1 ≈ x + x²/2 + x³/6 + x⁴/24 + x⁵/120 + x⁶/720
        return x * (1.0f + x * (0.5f + x * (0.16666667f + x * (0.041666667f + x * (0.0083333333f + x * 0.0013888889f)))));
    }
    else
    {
        return exp(x) - 1.0f;
    }
}

float4 expm1(float4 A)
{
    float4 O;
    O.x = expm1(A.x);
    O.y = expm1(A.y);
    O.z = expm1(A.z);
    O.w = expm1(A.w);
    return O;
}

float log1p(float x)
{
    // Domain: ln(1+x) is defined only for x > -1.
    // Return NaN for out‑of‑range inputs (matches HLSL's log behavior).
    float result = asfloat(0xFFFFFFFF); // NaN;

    if (x > -1.0f)
    {
        // Threshold below which a Taylor series gives far better precision.
        // 1e‑5 works well for float32; you can tune it if needed.
        const float THRESH = 1e-5f;

        if (abs(x) < THRESH)
        {
            // ln(1+x) ≈ x - x²/2 + x³/3 - x⁴/4 + x⁵/5 - x⁶/6
            result = x * (1 + x * (-0.5f + x * (0.33333334f + x * (-0.25f + x * (0.2f + x * 0.16666667f)))));
        }
        else
        {
            // For the remaining range we can safely use the built‑in log().
            // log(1+x) is numerically stable when |x| is not extremely small.
            result = log(1.0f + x);
        }
    }

    return result;
}

float4 log1p(float4 A)
{
    float4 O;
    O.x = log1p(A.x);
    O.y = log1p(A.y);
    O.z = log1p(A.z);
    O.w = log1p(A.w);
    return O;
}

// @TODO: move all code below into a separate and appropriately named file(s)
//
#define FLT_MAX asfloat(0x7F7FFFFF) //  3.402823466 E + 38
#define FLT_MIN asfloat(0xFF7FFFFF) // -3.402823466 E + 38
#define INT_MAX 0x7FFFFFFF //  2147483647
#define INT_MIN 0x80000000 // –2147483648
#define SHAPE_MAXRANK 8
#define FLT_EPSILON 1e-6

float fastfma(float a, float b, float c)
{
    return dot(float2(a,c), float2(b, 1));
}

// Neumaier's improved Kahan–Babuška algorithm for compensated summation
// see: https://en.wikipedia.org/wiki/Kahan_summation_algorithm
float neumaierAdd(float sum, float value, inout float floatingPointAccuracyCompensation)
{
    float newSum = sum + value;
    if (abs(sum) >= abs(value))
        floatingPointAccuracyCompensation += (sum - newSum) + value;
    else
        floatingPointAccuracyCompensation += (value - newSum) + sum;
    return newSum;
}
