Shader "Hidden/Sentis/ScatterND"
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
            #pragma multi_compile_local _ ScatterInt
            #pragma multi_compile_local ReduceNone ReduceAdd ReduceMul ReduceMax ReduceMin
            #pragma multi_compile_local _ K_LARGE
            #pragma vertex vert
            #pragma fragment frag

            #define B_INT

            #ifdef ScatterInt
            #define X_INT
            #define W_INT
            #define O_INT
            #endif

            uint ShapeX[8];

            #include "CommonVertexShader.cginc"
            #include "CommonPixelShader.cginc"

            #ifdef ScatterInt
            #define DTYPE int
            #define DTYPE4 int4
            DECLARE_TENSOR(X, int);
            DECLARE_TENSOR(W, int);
            #else
            #define DTYPE float
            #define DTYPE4 float4
            DECLARE_TENSOR(X, float);
            DECLARE_TENSOR(W, float);
            #endif
            DECLARE_TENSOR(B, int);
            DECLARE_TENSOR_BLOCK_STRIDE(X, DTYPE)
            DECLARE_TENSOR_BLOCK_STRIDE(B, int)
            DECLARE_TENSOR_BLOCK_STRIDE(W, DTYPE)
            DECLARE_TENSOR_BLOCK_STRIDE_O;

            uint NumIndices;
            uint SliceLength;
            uint Kdiv4, K, rank;

            DTYPE4 frag(v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
            {
                DTYPE4 updates;

                uint blockIndexO = GetBlockIndexO(screenPos);
                DTYPE4 v = SampleBlockX(blockIndexO);
                uint lowerIndexO = blockIndexO % SliceLength;
                uint upperIndexO = blockIndexO / SliceLength;
                uint n = upperIndexO;
                uint indexX[8];
                uint shapeInitial[8];

                for (uint j = K - 1; j < 8; j--)
                {
                    shapeInitial[j] = ShapeX[rank - 1 - j];
                    indexX[j] = n % shapeInitial[j];
                    n /= shapeInitial[j];
                }
                for (uint j = 0; j < NumIndices; j++)
                {
                    int4 b = SampleBlockB(Kdiv4 * j);
                    if (K > 0 && (b[0] != indexX[0] && b[0] + shapeInitial[0] != indexX[0]))
                        continue;
                    if (K > 1 && (b[1] != indexX[1] && b[1] + shapeInitial[1] != indexX[1]))
                        continue;
                    if (K > 2 && (b[2] != indexX[2] && b[2] + shapeInitial[2] != indexX[2]))
                        continue;
                    if (K > 3 && (b[3] != indexX[3] && b[3] + shapeInitial[3] != indexX[3]))
                        continue;

                    #ifdef K_LARGE
                    b = SampleBlockB(Kdiv4 * j + 1);
                    if (K > 4 && (b[0] != indexX[4] && b[0] + shapeInitial[4] != indexX[4]))
                        continue;
                    if (K > 5 && (b[1] != indexX[5] && b[1] + shapeInitial[5] != indexX[5]))
                        continue;
                    if (K > 6 && (b[2] != indexX[6] && b[2] + shapeInitial[6] != indexX[6]))
                        continue;
                    if (K > 7 && (b[3] != indexX[7] && b[3] + shapeInitial[7] != indexX[7]))
                        continue;
                    #endif

                    updates = SampleBlockW(lowerIndexO + j * SliceLength);

                    #ifdef ReduceNone
                    v = updates;
                    #elif ReduceAdd
                    v = v + updates;
                    #elif ReduceMul
                    v = v * updates;
                    #elif ReduceMax
                    v = max(v, updates);
                    #elif ReduceMin
                    v = min(v, updates);
                    #endif

                    break;
                }


                return v;
            }
            ENDCG
        }
    }
}
