Shader "Hidden/Sentis/TextureTensorDataDownload"
{
    Properties
    {
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma multi_compile_local TensorFloat TensorIntUpper TensorIntLower
            #pragma vertex vert
            #pragma fragment frag

            #include "CommonVertexShader.cginc"
            #include "CommonPixelShader.cginc"

            #if defined(TensorIntUpper) | defined(TensorIntLower)
            DECLARE_TENSOR(X, int);
            #define DTYPE4 int4
            #define DTYPE int
            #else
            DECLARE_TENSOR(X, float);
            #define DTYPE4 float4
            #define DTYPE float
            #endif

            DECLARE_TENSOR_BLOCK_STRIDE(X, DTYPE);

            float4 frag(v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
            {
                uint blockIndexO = GetBlockIndexO(screenPos);
                uint4 index4 = UnblockAxis(blockIndexO);
                DTYPE4 v = SampleElementsX(index4);

                #ifdef TensorFloat
                return v;
                #elif TensorIntUpper
                uint4 ret = 0x3f800000u | (asuint(v) >> 16);
                return asfloat(ret);
                #elif TensorIntLower
                uint4 ret = 0x3f800000u | (asuint(v) & 0x0000ffffu);
                return asfloat(ret);
                #else
                #endif
            }
            ENDCG
        }
    }
}
