Shader "Hidden/Sentis/SpaceToDepth"
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
            #pragma multi_compile_local _ INT

            #pragma vertex vert
            #pragma fragment frag

            #include "CommonVertexShader.cginc"
            #include "CommonPixelShader.cginc"

            DECLARE_TENSOR_BLOCK_STRIDE_O;

            #ifdef INT
            #define DTYPE4 int4
            DECLARE_TENSOR(X, int);
            DECLARE_TENSOR_BLOCK_STRIDE(X, int);
            #else
            #define DTYPE4 float4
            DECLARE_TENSOR(X, float);
            DECLARE_TENSOR_BLOCK_STRIDE(X, float);
            #endif

            uint O_width, O_height, O_channels;
            uint X_width, X_height, X_channels;

            uint BlockSize;

            DTYPE4 frag(v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
            {
                uint4 index4 = GetIndexO(screenPos);
                uint4 n4 = index4;
                uint4 w4 = n4 % O_width;
                n4 /= O_width;
                uint4 h4 = n4 % O_height;
                n4 /= O_height;
                uint4 c4 = n4 % O_channels;
                n4 /= O_channels;

                uint4 cx4 = c4 % X_channels;
                uint4 wx4 = w4 * BlockSize + (c4 / X_channels) % BlockSize;
                uint4 hx4 = h4 * BlockSize + (c4 / X_channels) / BlockSize;
                uint4 indexX4 = wx4 + X_width * (hx4 + X_height * (cx4 + X_channels * n4));
                DTYPE4 v = SampleElementsX(indexX4);
                return v;
            }
            ENDCG
        }
    }
}
