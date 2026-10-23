Shader "Hidden/Sentis/IsInfNaN"
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
            #pragma multi_compile_local IsInf IsNaN

            #pragma vertex vert
            #pragma fragment frag

            #define O_INT

            #include "CommonVertexShader.cginc"
            #include "CommonPixelShader.cginc"

            DECLARE_TENSOR(X, float);

            bool detectNegative;
            bool detectPositive;

            int4 frag(v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
            {
                uint blockIndexO = GetBlockIndexO(screenPos);
                float4 v = SampleBlockX(blockIndexO);
                int4 vOut;
                #ifdef IsInf
                    vOut = ((asuint(v) == 0x7F800000 && detectPositive) || (asuint(v) == 0xFF800000 && detectNegative)) ? 1 : 0;
                #endif
                #ifdef IsNaN
                    vOut = ((asuint(v) & 0x7FFFFFFF) > 0x7F800000) ? 1 : 0;
                #endif
                return vOut;
            }
            ENDCG
        }
    }
}
