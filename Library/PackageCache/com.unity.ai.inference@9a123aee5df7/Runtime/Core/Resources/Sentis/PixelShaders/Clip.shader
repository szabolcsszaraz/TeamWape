Shader "Hidden/Sentis/Clip"
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
            #pragma multi_compile_local _ ClipInt
            #pragma multi_compile_local _ UseMin
            #pragma multi_compile_local _ UseMax

            #pragma vertex vert
            #pragma fragment frag

            #include "CommonVertexShader.cginc"
            #include "CommonPixelShader.cginc"
            #include "../ComputeShaders/Tensor.cginc"

            DECLARE_TENSOR_BLOCK_STRIDE_O;

            #ifdef ClipInt
            #define DTYPE int
            #define DTYPE4 int4
            #else
            #define DTYPE float
            #define DTYPE4 float4
            #endif
            DECLARE_TENSOR(X, DTYPE);
            #ifdef UseMin
            DECLARE_TENSOR(S, DTYPE);
            #endif
            #ifdef UseMax
            DECLARE_TENSOR(B, DTYPE);
            #endif

            DTYPE4 frag(v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
            {
                uint blockIndexO = GetBlockIndexO(screenPos);
                int3 lowerAxisUpper = UnravelO(blockIndexO);
                uint4 unblocked4 = UnblockAxis(lowerAxisUpper[1]);
                uint4 index4 = lowerAxisUpper[0] + StrideAxisO * (unblocked4 + DimAxisO * lowerAxisUpper[2]);
                bool4 mask4 = (index4 < LengthO && unblocked4 < DimAxisO) ? 1 : 0;
                DTYPE4 v = SampleBlockX(blockIndexO);
                #ifdef UseMin
                v = max(v, SampleBlockS(0).x);
                #endif
                #ifdef UseMax
                v = min(v, SampleBlockB(0).x);
                #endif

                if (!mask4.x)
                    v.x = 0;
                if (!mask4.y)
                    v.y = 0;
                if (!mask4.z)
                    v.z = 0;
                if (!mask4.w)
                    v.w = 0;

                return v;
            }
            ENDCG
        }
    }
}
