Shader "Hidden/Sentis/LocalPool"
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
            #pragma multi_compile_local POOL1D POOL2D POOL3D
            #pragma multi_compile_local MAXPOOL AVGPOOL

            #pragma vertex vert
            #pragma fragment frag

            #include "CommonVertexShader.cginc"
            #include "CommonPixelShader.cginc"

            #define FLT_MIN -3.402823466e+38F

            DECLARE_TENSOR(X, float);

            uint O_width, O_height, O_depth, O_channelsDiv4;
            uint X_width, X_height, X_depth, X_channelsDiv4;

            int StrideZ, StrideY, StrideX, PadZ, PadY, PadX, PoolZ, PoolY, PoolX;

            float4 frag(v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
            {
                uint blockIndexO = GetBlockIndexO(screenPos);
                uint n = blockIndexO;
                uint w = n % O_width;
                n /= O_width;
                #if defined(POOL2D) || defined(POOL3D)
                uint h = n % O_height;
                n /= O_height;
                #endif
                #if defined(POOL3D)
                uint d = n % O_depth;
                n /= O_depth;
                #endif
                uint cDiv4 = n % O_channelsDiv4;
                uint outerIdx = n / O_channelsDiv4;

                // init 4 indexX at spatial base
                #if defined(POOL3D)
                uint4 indexX = (X_channelsDiv4 * outerIdx + cDiv4) * X_depth * X_height * X_width;
                #elif defined(POOL2D)
                uint4 indexX = (X_channelsDiv4 * outerIdx + cDiv4) * X_height * X_width;
                #else
                uint4 indexX = (X_channelsDiv4 * outerIdx + cDiv4) * X_width;
                #endif

                float counter = 0.0f;
                float4 accVal = 0.0f;
                #ifdef MAXPOOL
                accVal = FLT_MIN;
                #endif

                #if defined(POOL3D)
                for (int dz = 0; dz < PoolZ; ++dz)
                {
                    uint oz = (d * StrideZ + dz) - PadZ;
                    if (oz >= X_depth) continue;
                    indexX[2] = indexX[3] + oz * (X_width * X_height);
                #endif
                    #if defined(POOL2D) || defined(POOL3D)
                    for (int dy = 0; dy < PoolY; ++dy)
                    {
                        uint oy = (h * StrideY + dy) - PadY;
                        if (oy >= X_height) continue;
                        indexX[1] = indexX[2] + oy * X_width;
                    #endif
                        for (int dx = 0; dx < PoolX; ++dx)
                        {
                            uint ox = (w * StrideX + dx) - PadX;
                            if (ox >= X_width) continue;
                            float4 v = SampleBlockX(indexX[1] + ox);
                            #ifdef MAXPOOL
                            accVal = max(accVal, v);
                            #endif
                            #ifdef AVGPOOL
                            accVal += v;
                            #endif
                            counter += 1.0f;
                        }
                    #if defined(POOL2D) || defined(POOL3D)
                    }
                    #endif
                #if defined(POOL3D)
                }
                #endif

                #ifdef AVGPOOL
                accVal /= counter;
                #endif

                return accVal;
            }
            ENDCG
        }
    }
}
