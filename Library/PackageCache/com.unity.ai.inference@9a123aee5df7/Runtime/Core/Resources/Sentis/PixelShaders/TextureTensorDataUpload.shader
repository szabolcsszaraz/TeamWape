Shader "Hidden/Sentis/TextureTensorDataUpload"
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
            #pragma multi_compile_local TensorFloat TensorInt
            #pragma vertex vert
            #pragma fragment frag

            #if defined(TensorInt)
            #define O_INT
            #endif

            #include "CommonVertexShader.cginc"
            #include "CommonPixelShader.cginc"

            DECLARE_TENSOR_BLOCK_STRIDE_O;
            DECLARE_TENSOR(X, float);
            #if defined(TensorInt)
            DECLARE_TENSOR(S, float);
            #endif

            #if defined(TensorInt)
            int4 frag(v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
            {
                uint4 index4 = GetIndexO(screenPos);
                float4 lower = SampleElementsX(index4 >> 2, index4 & 3);
                float4 upper = SampleElementsS(index4 >> 2, index4 & 3);
                return asint(((asuint(upper) << 16) & 0xffff0000u) | (asuint(lower) & 0x0000ffffu));
            }
            #else
            float4 frag(v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
            {
                uint4 index4 = GetIndexO(screenPos);
                return SampleElementsX(index4 >> 2, index4 & 3);
            }
            #endif
            ENDCG
        }
    }
}
