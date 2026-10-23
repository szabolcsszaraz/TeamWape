Shader "Hidden/Sentis/STFT"
{
    Properties
    {
    }

    HLSLINCLUDE

    // We use the pass system to control the ROP stage:
    // Some output modes have the texel carry 4 channels with them containing 4 successive values
    // for the innermost axis, but this is just the tuple real/ima selection, so only 2 possible values.
    // While there's a waste of storage in this mode, we could avoid wasting output bandwidth
    // by configuring the ROP with the only 2 channels used.

    #include "CommonVertexShader.cginc"
    #include "CommonPixelShader.cginc"


    // Available paths:
    // X real, out blocked on last axis, matrix NO MAT_SPLIT_REAL_IMA_ON_ALTERNATE_ROWS
    // X real, out blocked on dft freq, matrix MAT_SPLIT_REAL_IMA_ON_ALTERNATE_ROWS
    // X real, out blocked on dft freq, matrix NO MAT_SPLIT_REAL_IMA_ON_ALTERNATE_ROWS
    // X complex, X blocked on last(real / ima, axis = -1)
    // X complex, X blocked on time(axis = -2)
    //     (here out blocked on last axis, matrix NO MAT_SPLIT_REAL_IMA_ON_ALTERNATE_ROWS)

    #pragma multi_compile_local SIG_REAL_OUT_4PACK_ON_REALIMA SIG_REAL_OUT_4PACK_ON_FREQ_MAT_SPLIT_REAL_IMA_ON_ROWS SIG_REAL_OUT_4PACK_ON_FREQ SIG_COMPLEX_4PACK_ON_REALIMA SIG_COMPLEX_4PACK_ON_TIME
    #pragma multi_compile_local _ FINAL_SCALAR_MUL

    #if defined(SIG_COMPLEX_4PACK_ON_REALIMA) || defined(SIG_COMPLEX_4PACK_ON_TIME)
        #define COMPLEX_SIGNAL
    #endif

    #if defined(SIG_REAL_OUT_4PACK_ON_FREQ_MAT_SPLIT_REAL_IMA_ON_ROWS)
        #define MAT_SPLIT_REAL_IMA_ON_ALTERNATE_ROWS
    #endif

    #if defined(COMPLEX_SIGNAL) || defined(SIG_REAL_OUT_4PACK_ON_REALIMA)
        #define OUT_FRAG_4PACK_ON_REALIMA_AXIS
    #endif

    #pragma vertex vert
    #pragma fragment frag


    DECLARE_TENSOR(X, float);
    DECLARE_TENSOR(K, float);

    uint O_width;     // number of frames
    uint O_channels;  // number of discrete frequencies
    uint O_channelsDiv4; // actually ceil(channels/4.0)
    uint X_width; // this is the total signal length
    uint X_widthDiv4;
    uint K_width; // this is just the frameLength
    uint maxK;
    uint maxX;
    uint K_MaxIdx;
    //uint FrameLength;
    //uint FrameLengthDiv4;

    uint StrideX; // frame step

    #if defined(FINAL_SCALAR_MUL)
    float Scale;
    #else
    #define Scale (1.0)
    #endif

    #if defined(COMPLEX_SIGNAL)
    #define IF_COMPLEX_SIGNAL_ELSE(a,b) (a)
    #else
    #define IF_COMPLEX_SIGNAL_ELSE(a,b) (b)
    #endif

    #if !defined(SHADER_API_D3D11)
    #define CLAMP_K_IF_NON_D3D(a) min(a, maxK)
    #define CLAMP_X_IF_NON_D3D(a) min(a, maxX)
    #else
    #define CLAMP_K_IF_NON_D3D(a) (a)
    #define CLAMP_X_IF_NON_D3D(a) (a)
    #endif


    float2 ComplexMUL(float2 z1, float2 z2)
    {
        float2 ret;
        ret[0] = (z1[0] * z2[0] - z1[1] * z2[1]);
        ret[1] = (z1[0] * z2[1] + z1[1] * z2[0]);
        return ret;
    }

    #define ADVANCE_NON_SPLIT_ROW_DFT_MATRIX_TEXEL_AND_CHANNEL(realOrIma, dftMatrix2TimeSample, dftMatrixFreqSeqIdxDiv4, dftMatrixFreqSeqIdxChannel) \
                    /* we do >= 2 since channel is 0 or 1 for 1rst element real/ima, next is +2 channel away and only have 2 elements in 4-c texel */ \
                    if (dftMatrixFreqSeqIdxChannel >= 2)           \
                    {                                              \
                        dftMatrixFreqSeqIdxChannel = (realOrIma);  \
                        dftMatrixFreqSeqIdxDiv4 += 1;              \
                        dftMatrix2TimeSample = SampleBlockK(CLAMP_K_IF_NON_D3D(dftMatrixFreqSeqIdxDiv4)); \
                    } \
                    else \
                    { \
                        dftMatrixFreqSeqIdxChannel += 2; \
                    }

    #define ADVANCE_SPLIT_ROW_DFT_MATRIX_TEXEL_AND_CHANNEL(dftMatrix4TimeSample, dftMatrixFreqSeqIdxDiv4, dftMatrixFreqSeqIdxChannel) \
                    if (dftMatrixFreqSeqIdxChannel == 3)           \
                    {                                              \
                        dftMatrixFreqSeqIdxChannel = 0;            \
                        dftMatrixFreqSeqIdxDiv4 += 1;              \
                        dftMatrix4TimeSample = SampleBlockK(CLAMP_K_IF_NON_D3D(dftMatrixFreqSeqIdxDiv4)); \
                    } \
                    else \
                    { \
                        dftMatrixFreqSeqIdxChannel += 1; \
                    }


    float4 frag(v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
    {
        // In conv shader, O is 4-chunked on output channels (dimension 1),
        // K is 4-chunked on input channels (dimension 1),
        // X is 4-chunked on input channels (dimension 1).
        //
        // So we will need 4 output channels per pixel in output,
        // hence we sample 4 times the kernel texture in the inner loop.

        uint blockIndexO = GetBlockIndexO(screenPos);
        uint n = blockIndexO;


    #if defined(OUT_FRAG_4PACK_ON_REALIMA_AXIS)
        uint outdFreqIdx = n % O_channels;
        n /= O_channels;
    #else
        // else each output fragment should contain for 4 different frequencies,
        // innermore axis of output has only size 2, real/ima selection of the complex freq tuple.
        uint realOrIma = n & 1;
        n = n >> 1;
        uint outdFreqIdxDiv4 = n % O_channelsDiv4;
        uint4 out4dFreqIdx = (outdFreqIdxDiv4 << 2) + uint4(0, 1, 2, 3);

        n /= O_channelsDiv4;
    #endif
        uint outFrameIdx = n % O_width;
        n /= O_width;
        uint batchIdx = n;

        //
        // Get the address of the start of the signal to convolve
        //
    #if defined(SIG_COMPLEX_4PACK_ON_REALIMA)
        // Valid only if X is a complex signal
        // also in that case, output is packed on innermost real/ima axis too, matrix has no split of real/ima on alternating rows
        #if !defined(OUT_FRAG_4PACK_ON_REALIMA_AXIS)
        #error "Signal packed on real/ima axis is expected complex and the output in this mode is expected to also 4-pack on innermost (real/ima) axis"
        #endif
        #if defined(MAT_SPLIT_REAL_IMA_ON_ALTERNATE_ROWS)
        #error "This DFT matrix format is not supported when the signal is complex"
        #endif
        uint signalStartIdx = batchIdx * X_width + outFrameIdx * StrideX;
        uint signalIdx = signalStartIdx;
    #else
        // The time axis is the one that is 4-packed
        uint signalStartIdxDiv4 = (batchIdx * X_widthDiv4 + ((outFrameIdx * StrideX) >> 2)) * IF_COMPLEX_SIGNAL_ELSE(2, 1);
        // * 2 for real/ima part if signal is complex, also note we have to handle the packing carefully as we stride along
        // the axis (time) that is 4-packed so first we divide by 4 the frame offset to get an offset in texel
        // (4-element "blocks") number, but that doesn't give us where in that texel we should start:
        uint signalStartIdxStartChannel = (outFrameIdx * StrideX) & 3;

        uint signalIdxDiv4 = signalStartIdxDiv4;
        uint signalIdxChannel = signalStartIdxStartChannel;
    #endif

        //
        // Get the address of the start of the windowed DFT matrix for the frequencies we want
        //
    #if defined(OUT_FRAG_4PACK_ON_REALIMA_AXIS)
        // Here the matrix can't be in the row split mode:
        #if defined(MAT_SPLIT_REAL_IMA_ON_ALTERNATE_ROWS)
        #error "This DFT matrix format is not supported when output packs real/ima in fragment's channels"
        #endif
        uint dftMatrixFreqSeqStartIdxDiv4 = (outdFreqIdx * K_width * 2) >> 2;
        uint dftMatrixFreqSeqStartIdxChannel = (outdFreqIdx * K_width * 2) & 3; // will be 0 or 2

        uint dftMatrixFreqSeqIdxDiv4 = dftMatrixFreqSeqStartIdxDiv4;
        uint dftMatrixFreqSeqIdxChannel = dftMatrixFreqSeqStartIdxChannel;
    #else
        #if defined(MAT_SPLIT_REAL_IMA_ON_ALTERNATE_ROWS)
        uint4 dftMatrix4FreqSeqStartIdxDiv4 = ((realOrIma + out4dFreqIdx * 2) * K_width) >> 2;
        uint4 dftMatrix4FreqSeqStartIdxChannel = ((realOrIma + out4dFreqIdx * 2) * K_width) & 3;

        uint4 dftMatrix4FreqSeqIdxDiv4 = dftMatrix4FreqSeqStartIdxDiv4;
        uint4 dftMatrix4FreqSeqIdxChannel = dftMatrix4FreqSeqStartIdxChannel;

        #else

        uint4 dftMatrix4FreqSeqStartIdxDiv4 = (out4dFreqIdx * K_width * 2) >> 2;
        uint4 dftMatrix4FreqSeqStartIdxChannel = ((out4dFreqIdx * K_width * 2) & 3) + realOrIma;

        uint4 dftMatrix4FreqSeqIdxDiv4 = dftMatrix4FreqSeqStartIdxDiv4;
        uint4 dftMatrix4FreqSeqIdxChannel = dftMatrix4FreqSeqStartIdxChannel;
        #endif
    #endif

        float4 outFor4Freq = 0;
        float2 outC = 0;

        bool done = false;
        uint nTime = 0;

        //////////////////////////////////////////////////////////////////////////////////////
        // Sample initial blocks
        //////////////////////////////////////////////////////////////////////////////////////
        #if defined(SIG_COMPLEX_4PACK_ON_REALIMA)
            // Signal is complex, real/ima part in 1 texel (rest empty)
            // output 4-packed on innermost axis (real/ima), matrix has no split of real/ima on alternating rows
            float2 signalSampleC = SampleBlockX(signalIdx).xy;
            float4 dftMatrix2TimeSample = SampleBlockK(CLAMP_K_IF_NON_D3D(dftMatrixFreqSeqIdxDiv4));
        #else
            #if defined(COMPLEX_SIGNAL)
            //{
                // Signal is complex, 4 time values per texel (all real or all ima, alternating)
                // output 4-packed on innermost axis (real/ima), matrix has no split of real/ima on alternating rows
                float4 signal4SampleReal = SampleBlockX(signalIdxDiv4);
                float4 signal4SampleIma = SampleBlockX(signalIdxDiv4 + 1);

                float4 dftMatrix2TimeSample = SampleBlockK(CLAMP_K_IF_NON_D3D(dftMatrixFreqSeqIdxDiv4));
            //}
            #else
            //{
                //
                // signal is real
                float4 signal4SampleReal = SampleBlockX(signalIdxDiv4);

                // if OUT_FRAG_4PACK_ON_REALIMA_AXIS, matrix has no split of real/ima on alternating rows
                // else (out 4-pack freq) matrix can be either
                #if defined(OUT_FRAG_4PACK_ON_REALIMA_AXIS)
                //{
                    float4 dftMatrix2TimeSample = SampleBlockK(CLAMP_K_IF_NON_D3D((dftMatrixFreqSeqIdxDiv4)));
                //}
                #else
                //{
                    // In this case we output for 4 frequencies, but only the real or imaginary parts
                    #if defined(MAT_SPLIT_REAL_IMA_ON_ALTERNATE_ROWS)
                        float4 dftMatrix4TimeSampleF0 = SampleBlockK(CLAMP_K_IF_NON_D3D(dftMatrix4FreqSeqIdxDiv4[0]));
                        float4 dftMatrix4TimeSampleF1 = SampleBlockK(CLAMP_K_IF_NON_D3D(dftMatrix4FreqSeqIdxDiv4[1]));
                        float4 dftMatrix4TimeSampleF2 = SampleBlockK(CLAMP_K_IF_NON_D3D(dftMatrix4FreqSeqIdxDiv4[2]));
                        float4 dftMatrix4TimeSampleF3 = SampleBlockK(CLAMP_K_IF_NON_D3D(dftMatrix4FreqSeqIdxDiv4[3]));
                    #else
                        float4 dftMatrix2TimeSampleF0 = SampleBlockK(CLAMP_K_IF_NON_D3D(dftMatrix4FreqSeqIdxDiv4[0]));
                        float4 dftMatrix2TimeSampleF1 = SampleBlockK(CLAMP_K_IF_NON_D3D(dftMatrix4FreqSeqIdxDiv4[1]));
                        float4 dftMatrix2TimeSampleF2 = SampleBlockK(CLAMP_K_IF_NON_D3D(dftMatrix4FreqSeqIdxDiv4[2]));
                        float4 dftMatrix2TimeSampleF3 = SampleBlockK(CLAMP_K_IF_NON_D3D(dftMatrix4FreqSeqIdxDiv4[3]));
                    #endif
                //}
                #endif // #if defined(OUT_FRAG_4PACK_ON_REALIMA_AXIS)
            //}
            #endif
        #endif


        //////////////////////////////////////////////////////////////////////////////////////
        // Convolve / inner product
        //////////////////////////////////////////////////////////////////////////////////////
        for(;;)
        {
            // Grab the next values of signal
        #if defined(SIG_COMPLEX_4PACK_ON_REALIMA)
            // Signal is complex, output 4-packed on innermost axis (real/ima), matrix has no split of real/ima on alternating rows
            {
                float2 dftMatrixSampleC = float2(dftMatrix2TimeSample[dftMatrixFreqSeqIdxChannel], dftMatrix2TimeSample[dftMatrixFreqSeqIdxChannel + 1]);

                outC += ComplexMUL(signalSampleC, dftMatrixSampleC);

                nTime++;
                if (nTime >= K_width)
                    break;

                if (dftMatrixFreqSeqIdxChannel == 2)
                {
                    dftMatrixFreqSeqIdxChannel = 0;
                    dftMatrixFreqSeqIdxDiv4 += 1;
                    dftMatrix2TimeSample = SampleBlockK(CLAMP_K_IF_NON_D3D(dftMatrixFreqSeqIdxDiv4));
                }
                else
                {
                    dftMatrixFreqSeqIdxChannel += 2;
                }
                signalIdx++;
                signalSampleC = SampleBlockX(CLAMP_X_IF_NON_D3D(signalIdx)).xy;
            }
        #else
            #if defined(COMPLEX_SIGNAL)
            {
                // Signal is complex, 4 time values per texel (all real or all ima, alternating)
                // output 4-packed on innermost axis (real/ima), matrix has no split of real/ima on alternating rows
                float2 signalSampleC = float2(signal4SampleReal[signalIdxChannel], signal4SampleIma[signalIdxChannel]);
                float2 dftMatrixSampleC = float2(dftMatrix2TimeSample[dftMatrixFreqSeqIdxChannel], dftMatrix2TimeSample[dftMatrixFreqSeqIdxChannel + 1]);

                outC += ComplexMUL(signalSampleC, dftMatrixSampleC);

                nTime++;
                if (nTime >= K_width)
                    break;

                if (dftMatrixFreqSeqIdxChannel == 2)
                {
                    dftMatrixFreqSeqIdxChannel = 0;
                    dftMatrixFreqSeqIdxDiv4 += 1;
                    dftMatrix2TimeSample = SampleBlockK(CLAMP_K_IF_NON_D3D(dftMatrixFreqSeqIdxDiv4));
                }
                else
                {
                    dftMatrixFreqSeqIdxChannel += 2;
                }
                if (signalIdxChannel == 3)
                {
                    signalIdxChannel = 0;
                    signalIdxDiv4 += 2; // the next time sample group is 2 blocks away since +1 just switches between real/ima
                    signal4SampleReal = SampleBlockX(CLAMP_X_IF_NON_D3D(signalIdxDiv4));
                    signal4SampleIma = SampleBlockX(CLAMP_X_IF_NON_D3D(signalIdxDiv4 + 1));
                }
                else
                {
                    signalIdxChannel += 1;
                }
            }
            #else
            {
                // signal is real:
                //      At current time, we have the signal sample: signal4SampleReal[signalIdxChannel]
                //
                // if OUT_FRAG_4PACK_ON_REALIMA_AXIS, matrix has no split of real/ima on alternating rows
                // else (out 4-pack freq) matrix can be either

                #if defined(OUT_FRAG_4PACK_ON_REALIMA_AXIS)
                {
                    float2 dftMatrixSampleC = float2(dftMatrix2TimeSample[dftMatrixFreqSeqIdxChannel], dftMatrix2TimeSample[dftMatrixFreqSeqIdxChannel + 1]);

                    outC += signal4SampleReal[signalIdxChannel] * dftMatrixSampleC;

                    nTime++;
                    if (nTime >= K_width)
                        break;

                    if (dftMatrixFreqSeqIdxChannel == 2)
                    {
                        dftMatrixFreqSeqIdxChannel = 0;
                        dftMatrixFreqSeqIdxDiv4 += 1;
                        dftMatrix2TimeSample = SampleBlockK(CLAMP_K_IF_NON_D3D((dftMatrixFreqSeqIdxDiv4)));
                    }
                    else
                    {
                        dftMatrixFreqSeqIdxChannel += 2;
                    }
                    if (signalIdxChannel == 3)
                    {
                        signalIdxChannel = 0;
                        signalIdxDiv4 += 1;
                        signal4SampleReal = SampleBlockX(CLAMP_X_IF_NON_D3D(signalIdxDiv4));
                    }
                    else
                    {
                        signalIdxChannel += 1;
                    }
                }
                #else
                {
                    // In this case we output for 4 frequencies, but only the real or imaginary parts
                    #if defined(MAT_SPLIT_REAL_IMA_ON_ALTERNATE_ROWS)

                        float4 dftMatrixSampleRealOrImaFor4F = 0;
                        dftMatrixSampleRealOrImaFor4F[0] = dftMatrix4TimeSampleF0[dftMatrix4FreqSeqIdxChannel[0]];
                        dftMatrixSampleRealOrImaFor4F[1] = dftMatrix4TimeSampleF1[dftMatrix4FreqSeqIdxChannel[1]];
                        dftMatrixSampleRealOrImaFor4F[2] = dftMatrix4TimeSampleF2[dftMatrix4FreqSeqIdxChannel[2]];
                        dftMatrixSampleRealOrImaFor4F[3] = dftMatrix4TimeSampleF3[dftMatrix4FreqSeqIdxChannel[3]];

                        outFor4Freq += signal4SampleReal[signalIdxChannel] * dftMatrixSampleRealOrImaFor4F;

                        nTime++;
                        if (nTime >= K_width)
                            break;

                        // Mobile problem seems to be a watchdog timeout (at least on Android, this outputs data, but ~ 1500 you get all 0s)
                        //if (nTime >= 1000)
                        //    return dftMatrix4FreqSeqIdxDiv4;


                        ADVANCE_SPLIT_ROW_DFT_MATRIX_TEXEL_AND_CHANNEL(dftMatrix4TimeSampleF0, dftMatrix4FreqSeqIdxDiv4[0], dftMatrix4FreqSeqIdxChannel[0]);
                        ADVANCE_SPLIT_ROW_DFT_MATRIX_TEXEL_AND_CHANNEL(dftMatrix4TimeSampleF1, dftMatrix4FreqSeqIdxDiv4[1], dftMatrix4FreqSeqIdxChannel[1]);
                        ADVANCE_SPLIT_ROW_DFT_MATRIX_TEXEL_AND_CHANNEL(dftMatrix4TimeSampleF2, dftMatrix4FreqSeqIdxDiv4[2], dftMatrix4FreqSeqIdxChannel[2]);
                        ADVANCE_SPLIT_ROW_DFT_MATRIX_TEXEL_AND_CHANNEL(dftMatrix4TimeSampleF3, dftMatrix4FreqSeqIdxDiv4[3], dftMatrix4FreqSeqIdxChannel[3]);

                        if (signalIdxChannel == 3)
                        {
                            signalIdxChannel = 0;
                            signalIdxDiv4 += 1;
                            signal4SampleReal = SampleBlockX(CLAMP_X_IF_NON_D3D(signalIdxDiv4));
                        }
                        else
                        {
                            signalIdxChannel += 1;
                        }


                    #else

                        float dftMatrixSampleRealOrIma_F0 = dftMatrix2TimeSampleF0[dftMatrix4FreqSeqIdxChannel[0]];
                        float dftMatrixSampleRealOrIma_F1 = dftMatrix2TimeSampleF1[dftMatrix4FreqSeqIdxChannel[1]];
                        float dftMatrixSampleRealOrIma_F2 = dftMatrix2TimeSampleF2[dftMatrix4FreqSeqIdxChannel[2]];
                        float dftMatrixSampleRealOrIma_F3 = dftMatrix2TimeSampleF3[dftMatrix4FreqSeqIdxChannel[3]];

                        outFor4Freq[0] += signal4SampleReal[signalIdxChannel] * dftMatrixSampleRealOrIma_F0;
                        outFor4Freq[1] += signal4SampleReal[signalIdxChannel] * dftMatrixSampleRealOrIma_F1;
                        outFor4Freq[2] += signal4SampleReal[signalIdxChannel] * dftMatrixSampleRealOrIma_F2;
                        outFor4Freq[3] += signal4SampleReal[signalIdxChannel] * dftMatrixSampleRealOrIma_F3;

                        nTime++;
                        if (nTime >= K_width)
                            break;

                        ADVANCE_NON_SPLIT_ROW_DFT_MATRIX_TEXEL_AND_CHANNEL(realOrIma, dftMatrix2TimeSampleF0, dftMatrix4FreqSeqIdxDiv4[0], dftMatrix4FreqSeqIdxChannel[0]);
                        ADVANCE_NON_SPLIT_ROW_DFT_MATRIX_TEXEL_AND_CHANNEL(realOrIma, dftMatrix2TimeSampleF1, dftMatrix4FreqSeqIdxDiv4[1], dftMatrix4FreqSeqIdxChannel[1]);
                        ADVANCE_NON_SPLIT_ROW_DFT_MATRIX_TEXEL_AND_CHANNEL(realOrIma, dftMatrix2TimeSampleF2, dftMatrix4FreqSeqIdxDiv4[2], dftMatrix4FreqSeqIdxChannel[2]);
                        ADVANCE_NON_SPLIT_ROW_DFT_MATRIX_TEXEL_AND_CHANNEL(realOrIma, dftMatrix2TimeSampleF3, dftMatrix4FreqSeqIdxDiv4[3], dftMatrix4FreqSeqIdxChannel[3]);

                        if (signalIdxChannel == 3)
                        {
                            signalIdxChannel = 0;
                            signalIdxDiv4 += 1;
                            signal4SampleReal = SampleBlockX(CLAMP_X_IF_NON_D3D(signalIdxDiv4));
                        }
                        else
                        {
                            signalIdxChannel += 1;
                        }

                    #endif
                }
                #endif // #if defined(OUT_FRAG_4PACK_ON_REALIMA_AXIS)

            }
            #endif
        #endif
        }


    #if defined(OUT_FRAG_4PACK_ON_REALIMA_AXIS)
        return float4(outC[0] * Scale, outC[1] * Scale, 0, 0);
    #else
        return outFor4Freq * Scale;
    #endif
    }

    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            ColorMask RGBA
            HLSLPROGRAM
            ENDHLSL
        }

        Pass
        {
            ColorMask RG
            HLSLPROGRAM
            ENDHLSL
        }
    }
}
