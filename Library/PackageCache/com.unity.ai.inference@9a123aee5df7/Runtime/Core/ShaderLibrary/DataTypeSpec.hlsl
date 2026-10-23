#ifndef DATA_TYPE_SPEC_HLSL
#define DATA_TYPE_SPEC_HLSL

#define DATA_TYPE_SPEC_NATIVE 0
#define DATA_TYPE_SPEC_COMPLEX 1

#if !defined(DATA_TYPE_SPEC)
    #define DATA_TYPE_SPEC DATA_TYPE_SPEC_NATIVE
#endif

#if (DATA_TYPE_SPEC == DATA_TYPE_SPEC_NATIVE)
    // Native data type assumes all native ops work:

    #if !defined(DATA_TYPE)
        #define DATA_TYPE float
    #endif

    #define DATA_TYPE_DWORD_SIZE (1)
    #define DATA_TYPE_DWORD_SIZELOG2 (0)

    #define ADD_DATA(a, b) ((a)+(b))
    #define MUL_DATA(a, b) ((a)*(b))
    #define LFLOATMUL_DATA(a, b) ((a)*(b))

    #define DATA_TYPE_ADDITIVE_ZERO (0.0)

    #define REAL_PART(z) ((z))
    #define IMAG_PART(z) (0)

#elif (DATA_TYPE_SPEC == DATA_TYPE_SPEC_COMPLEX)

    #if !defined(DATA_TYPE)
        #define DATA_TYPE Complex
    #endif

    #define DATA_TYPE_DWORD_SIZE (2)
    #define DATA_TYPE_DWORD_SIZELOG2 (1)

    #define ADD_DATA(a, b) (ComplexSADD((a), (b)))
    #define MUL_DATA(a, b) (ComplexSMUL((a), (b)))
    #define LFLOATMUL_DATA(a, b) (RealComplexSMUL((a), (b)))

    #define DATA_TYPE_ADDITIVE_ZERO (ComplexSADD_ConstZero())

    #define REAL_PART(z) ((z).re)
    #define IMAG_PART(z) ((z).im)

#else
    #error "DATA_TYPE_SPEC: Unknown data type"
#endif

struct Complex
{
    float re;
    float im;
};

Complex ComplexSMUL(Complex z1, Complex z2)
{
    Complex ret;
    ret.re = (z1.re * z2.re - z1.im * z2.im);
    ret.im = (z1.re * z2.im + z1.im * z2.re);
    return ret;
}

Complex RealComplexSMUL(float r, Complex z)
{
    Complex ret;
    ret.re = z.re * r;
    ret.im = z.im * r;
    return ret;
}

Complex ComplexSADD(Complex z1, Complex z2)
{
    Complex ret;
    ret.re = (z1.re + z2.re);
    ret.im = (z1.im + z2.im);
    return ret;
}

Complex ComplexSADD_ConstZero()
{
    Complex ret;
    ret.re = 0;
    ret.im = 0;
    return ret;
}


#endif // #ifndef DATA_TYPE_SPEC_HLSL