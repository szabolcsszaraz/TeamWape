Shader "Hidden/Sentis/WindowedDFTMatrix"
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
            #pragma multi_compile_local _ SPLIT_REAL_IMA_ON_ALTERNATE_ROWS
            #pragma multi_compile_local _ NO_WINDOW
            #pragma multi_compile_local _ INVERSE_DFT


            #pragma vertex vert
            #pragma fragment frag

            #include "CommonVertexShader.cginc"
            #include "CommonPixelShader.cginc"

            #if !defined(NO_WINDOW)
            DECLARE_TENSOR(W, float);
            DECLARE_TENSOR_BLOCK_STRIDE(W, float); // needed if using SampleElementW instead of SampleBlockW only
            #endif


            #if defined(SHADER_API_D3D11)
                #define IF_D3D_ELSE(a, b) (a)
            #else
                #define IF_D3D_ELSE(a, b) (b)
            #endif


            uint O_width;  // window / frame_length, regardless if SPLIT_REAL_IMA_ON_ALTERNATE_ROWS or not

            float DFTFundamentalFreq; // 1 / frame_length

            float2 ComputeTwiddleFactor(uint row, uint col)
            {
                // Returns TwiddleFactor W_{N}^{n * k}
                // N = frameLength
                // k = row
                // n = col
                // fundamental = 1/frameLength
                // Thus returns cossin(-2*pi*k*n*fundamental);
                // Final outputs are window[n] * cossin(-2*pi*k*n*fundamental);
            #if defined(INVERSE_DFT)
                float signFactor = 1.0;
            #else
                float signFactor = -1.0;
            #endif
                float angle = signFactor * 2.0 * (3.14159265358979323846) * row * col * DFTFundamentalFreq;
                float2 cossin;
                sincos(angle, cossin.y, cossin.x);
                return cossin; // .x is real part .y is imaginary part of the sequence
            }

            float4 frag(v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
            {
                uint texel4cLinearIdx = GetBlockIndexO(screenPos);
                uint4 out4LinearIdx = (texel4cLinearIdx  * 4 + uint4(0, 1, 2, 3));

                // We expect a 4-packed linear window, 4 time steps per texel.
                // The matrix output fragment's 4 channels is for 4 time steps if SPLIT_REAL_IMA_ON_ALTERNATE_ROWS
                // or 2 time steps otherwise, and can be for a mix of different real/ima and/or different n-dian frequency number.

            #if defined(SPLIT_REAL_IMA_ON_ALTERNATE_ROWS)
                uint4 out4ndFreqRawRow = (out4LinearIdx / O_width);
                uint4 out4ndFreq = (out4ndFreqRawRow / 2);

                uint4 out4nTimeRawColumn = (out4LinearIdx  % O_width);
                uint4 out4nTime = out4nTimeRawColumn;

                uint4 out4RealOrIma = (out4ndFreqRawRow & 1);
            #else
                uint4 out4ndFreqRawRow = (out4LinearIdx / (O_width * 2));
                uint4 out4ndFreq = out4ndFreqRawRow;

                uint4 out4nTimeRawColumn = (out4LinearIdx % (O_width * 2));
                uint4 out4nTime = (out4nTimeRawColumn / 2);

                uint4 out4RealOrIma = (out4nTimeRawColumn & 1); // parity of the row or column (depending on if we packed real/ima on alternating rows or not)
            #endif

                uint4 out4TimeMask = out4nTime < O_width;

            #if !defined(NO_WINDOW)
                float4 out4WindowVals = (float4)0;
                #if 0
                {
                        // seems wasteful but moot, we calculate this matrix one time, the major cost is 1/4 rate trig, and this trivially hits the texture cache:
                        // (also in non SPLIT_REAL_IMA_ON_ALTERNATE_ROWS mode, out4nTimeRawColumn[0] always starts at an even number - because out4LinearIdx does and we do % (even number O_width*2)
                        // so out4nTime[0] == out4nTime[1] and out4nTime[2] == out4nTime[3])

                    #if !defined(SHADER_API_D3D11)
                        out4WindowVals[0] = SampleElementW(out4nTime[0]);
                        if (out4TimeMask[1] >= 1)
                            out4WindowVals[1] = SampleElementW(out4nTime[1]);
                        if (out4TimeMask[2] >= 1)
                            out4WindowVals[2] = SampleElementW(out4nTime[2]);
                        if (out4TimeMask[3] >= 1)
                            out4WindowVals[3] = SampleElementW(out4nTime[3]);
                    #else
                        out4WindowVals = float4(SampleElementW(out4nTime[0]), SampleElementW(out4nTime[1]), SampleElementW(out4nTime[2]), SampleElementW(out4nTime[3]));
                    #endif
                }
                #else
                {
                        // starting_channel_idx of first window texel we fetch:
                        uint starting_channel_idx = (out4nTime[0] & 3);
                        uint potential_nb_usable_channels = 4 - starting_channel_idx;

                        float4 window_texel_0 = SampleBlockW(out4nTime[0] >> 2);
                        //window_texel_0 = float4((out4nTime[0]) + float4(0,1,2,3) - (float4)starting_channel_idx);


                    #if defined(SPLIT_REAL_IMA_ON_ALTERNATE_ROWS)

                        int outTimeSamplesTodo = dot(out4TimeMask, uint4(1,1,1,1));

                        uint nb_usable_channels = min(potential_nb_usable_channels, O_width - out4nTime[0]);
                        uint cur_channel_limit = min(outTimeSamplesTodo, nb_usable_channels);
                        uint cur_channel = 0;
                        [unroll(4)]
                        for (cur_channel = 0; cur_channel < cur_channel_limit; cur_channel++)
                        {
                            outTimeSamplesTodo--;
                            out4WindowVals[cur_channel] = window_texel_0[starting_channel_idx + cur_channel];
                        }
                        //return (float4)(out4nTime[cur_channel]);
                        //return (float4)out4nTime[0];
                        // From here we could need 2 more texels: either one in current row that has all left-over needed data
                        // or one in current row and one in next (2 texels total), or one in next row that will have all left-over data
                        if (outTimeSamplesTodo > 0)
                        {
                            uint  new_window_texel_time_step = out4nTime[cur_channel];

                            float4 window_texel_1 = SampleBlockW(new_window_texel_time_step >> 2);
                            //window_texel_1 = float4((new_window_texel_time_step) + float4(0,1,2,3));

                            nb_usable_channels = min(4, O_width - new_window_texel_time_step);
                            nb_usable_channels = min(outTimeSamplesTodo, nb_usable_channels);
                            cur_channel_limit = cur_channel + nb_usable_channels;
                            // ...now we can potentially use 4 of the window because
                            // the beginning of the new window texel aligns to the current out channel we've currently reached
                            [unroll(4)]
                            for (int wi = 0; cur_channel < cur_channel_limit; cur_channel++, wi++)
                            {
                                //out4WindowVals[cur_channel] = window_texel_1[0 + wi]; // ...similarly starting_channel_idx == 0
                                if (cur_channel == 1)
                                    out4WindowVals[1] = window_texel_1[0 + wi];
                                else if (cur_channel == 2)
                                    out4WindowVals[2] = window_texel_1[0 + wi];
                                else if (cur_channel == 3)
                                    out4WindowVals[3] = window_texel_1[0 + wi];

                                outTimeSamplesTodo--;
                            }

                            if (outTimeSamplesTodo > 0 )
                            {
                                new_window_texel_time_step = out4nTime[cur_channel];

                                float4 window_texel_2 = SampleBlockW(new_window_texel_time_step >> 2);
                                //window_texel_2 = float4((new_window_texel_time_step) + float4(0,1,2,3));

                                nb_usable_channels = min(4, O_width - new_window_texel_time_step);
                                nb_usable_channels = min(outTimeSamplesTodo, nb_usable_channels);
                                cur_channel_limit = cur_channel + nb_usable_channels;
                                [unroll(4)]
                                for (int wi = 0; cur_channel < cur_channel_limit; cur_channel++, wi++)
                                {
                                    //out4WindowVals[cur_channel] = window_texel_2[0 + wi];
                                    if (cur_channel == 1)
                                        out4WindowVals[1] = window_texel_2[0 + wi];
                                    else if (cur_channel == 2)
                                        out4WindowVals[2] = window_texel_2[0 + wi];
                                    else if (cur_channel == 3)
                                        out4WindowVals[3] = window_texel_2[0 + wi];

                                    outTimeSamplesTodo--; // unused, compiled out
                                }
                            }
                        }
                    #else

                        // When organizing the matrix such that real,ima pairs are next to each other (no SPLIT_REAL_IMA_ON_ALTERNATE_ROWS),
                        // we only need 2 time steps from the window, so at most 2 texels of the window:

                        int outTimeSamplesTodo = dot(out4TimeMask, uint4(1,1,1,1)) >> 1; // we have 2 identical times in there so either 1100 or 1111


                        int nb_usable_channels = min(potential_nb_usable_channels, int(O_width) - out4nTime[0]);
                        nb_usable_channels = min(nb_usable_channels, min(2, outTimeSamplesTodo));

                        int used_window_channels = 0;
                        [unroll(2)]
                        for (used_window_channels = 0; used_window_channels < nb_usable_channels; used_window_channels++)
                        {
                            out4WindowVals[2 * used_window_channels + 0] = window_texel_0[starting_channel_idx + used_window_channels];
                            out4WindowVals[2 * used_window_channels + 1] = window_texel_0[starting_channel_idx + used_window_channels];
                            outTimeSamplesTodo--;
                        }


                        // From here we could need only 1 more texel: either one in current row that has all left-over needed data
                        // or one in next. This is because an out fragment of 4 channels need 2 time steps
                        // (so 2 window samples, these can come from the same texel or at most 2 texels)
                        if (outTimeSamplesTodo > 0) // && used_window_channels < 2)
                        {
                            uint new_window_texel_time_step = out4nTime[2] % O_width;

                            float4 window_texel_1 = SampleBlockW(new_window_texel_time_step >> 2);
                            //window_texel_1 = float4((new_window_texel_time_step) + float4(0,1,2,3));

                            out4WindowVals[2] = window_texel_1[0];
                            out4WindowVals[3] = window_texel_1[0];
                        }
                    #endif // #if defined(SPLIT_REAL_IMA_ON_ALTERNATE_ROWS)

                }// avoid sampling as much as possible
                #endif
            #endif // #if !defined(NO_WINDOW)

                float4 unwindowed4Seq = (float4)0;

            #if defined(SPLIT_REAL_IMA_ON_ALTERNATE_ROWS)
                float2 twiddle0 = ComputeTwiddleFactor(out4ndFreq[0], out4nTime[0]);
                float2 twiddle1 = ComputeTwiddleFactor(out4ndFreq[1], out4nTime[1]);
                float2 twiddle2 = ComputeTwiddleFactor(out4ndFreq[2], out4nTime[2]);
                float2 twiddle3 = ComputeTwiddleFactor(out4ndFreq[3], out4nTime[3]);
                unwindowed4Seq = float4(twiddle0[out4RealOrIma[0]], twiddle1[out4RealOrIma[1]], twiddle2[out4RealOrIma[2]], twiddle3[out4RealOrIma[3]]);
            #else
                // When arranging complex pairs packed together, we can more efficiently generate the DFT matrix:
                float2 twiddle0 = ComputeTwiddleFactor(out4ndFreq[0], out4nTime[0]);
                float2 twiddle1 = ComputeTwiddleFactor(out4ndFreq[2], out4nTime[2]);
                unwindowed4Seq = float4(twiddle0[0], twiddle0[1], twiddle1[0], twiddle1[1]);
            #endif

            #if !defined(NO_WINDOW)
                return unwindowed4Seq * out4WindowVals;
                // debug:
                //return unwindowed4Seq;
                //return out4WindowVals;
                //return float4(out4nTime[0], out4nTime[1], out4nTime[2], out4nTime[3]);
            #else
                return unwindowed4Seq;
            #endif
            }
            ENDCG
        }
    }
}
