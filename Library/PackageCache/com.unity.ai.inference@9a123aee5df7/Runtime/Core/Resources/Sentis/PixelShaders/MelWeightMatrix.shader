Shader "Hidden/Sentis/MelWeightMatrix"
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
            #pragma vertex vert
            #pragma fragment frag

            #include "CommonVertexShader.cginc"
            #include "CommonPixelShader.cginc"

            uint dftLength;
            uint numMelBins;
            uint numSpectrogramBins;
            float lowerEdgeMel;
            float melStep;
            uint sampleRate;

            float4 frag(v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
            {
                uint blockIndexO = GetBlockIndexO(screenPos);
                uint idx = blockIndexO;
                uint indexMelBin = idx % numMelBins;
                idx /= numMelBins;
                int indexFreqBin = idx;

                float3 mel = lowerEdgeMel + (indexMelBin + int3(0, 1, 2)) * melStep;
                int3 freqBins = floor((dftLength + 1) * (700 * (pow(10, mel / 2595.0) - 1)) / sampleRate);
                int lowerFreqBin = freqBins.x;
                int centreFreqBin = freqBins.y;
                int higherFreqBin = freqBins.z;

                float4 left  = (float4)(indexFreqBin - lowerFreqBin) / max((float4)(centreFreqBin - lowerFreqBin), 1.0);
                float4 right = (float4)(higherFreqBin - indexFreqBin) / max((float4)(higherFreqBin - centreFreqBin), 1.0);

                bool4 cond_special = (lowerFreqBin == centreFreqBin) & (indexFreqBin == centreFreqBin);
                bool4 cond_left    = !cond_special & (indexFreqBin >= lowerFreqBin) & (indexFreqBin <= centreFreqBin);
                bool4 cond_right   = !cond_special & !cond_left & (indexFreqBin >= centreFreqBin) & (indexFreqBin < higherFreqBin);

                // Step 4: Combine with mutually exclusive masks
                float4 v = cond_special * 1.0 +
                           cond_left    * left +
                           cond_right   * right;

                return v;
            }
            ENDCG
        }
    }
}
