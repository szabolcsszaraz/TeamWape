Shader "Hidden/Sentis/Window"
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
            #pragma multi_compile_local BLACKMAN HAMMING HANN

            #pragma vertex vert
            #pragma fragment frag

            #include "CommonVertexShader.cginc"
            #include "CommonPixelShader.cginc"

            float N;

            DECLARE_TENSOR_BLOCK_STRIDE_O;

            float4 frag(v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
            {
                uint blockIndexO = GetBlockIndexO(screenPos);
                uint4 index4 = UnblockAxis(blockIndexO);

                #if defined(BLACKMAN)
                return 0.42f - 0.5f * cos(6.28318530718f * index4 / N) + 0.08f * cos(12.5663706144f * index4 / N);
                #elif defined(HAMMING)
                return 0.54347826087f - 0.45652173913f * cos(6.28318530718f * index4 / N);
                #else // defined(HANN)
                return 0.5f - 0.5f * cos(6.28318530718f * index4 / N);
                #endif
            }
            ENDCG
        }
    }
}
