Shader "Hidden/Sentis/ConvTranspose"
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
            #pragma multi_compile_local CONVTRANSPOSE1D CONVTRANSPOSE2D CONVTRANSPOSE3D
            #pragma multi_compile_local NONE RELU
            #pragma multi_compile_local _ USEBIAS

            #pragma multi_compile_local _ DILATIONX_GT_ONE_LTE_STRIDE DILATIONX_GT_STRIDE
            #pragma multi_compile_local _ DILATIONY_GT_ONE_LTE_STRIDE DILATIONY_GT_STRIDE
            #pragma multi_compile_local _ DILATIONZ_GT_ONE_LTE_STRIDE DILATIONZ_GT_STRIDE

            #pragma multi_compile_local _ GROUPS_ENABLED

            // otherwise, will just load unpack all needed input channels at each iteration, 8 loads per input channels to sum
            // TODO: profile if and which cases are worth it then make variants:
            // #pragma multi_compile_local _ INNER_SUM_TRY_BLOCK_REUSE
            #define INNER_SUM_TRY_BLOCK_REUSE

            //debug
            //#define GROUPS_ENABLED

            #pragma vertex vert
            #pragma fragment frag

            #include "CommonVertexShader.cginc"
            #include "CommonPixelShader.cginc"

            #ifdef USEBIAS
            DECLARE_TENSOR(B, float);
            #endif
            DECLARE_TENSOR(K, float);
            DECLARE_TENSOR(X, float);

            uint O_width, O_height, O_depth, O_channelsDiv4;
            uint K_width, K_height, K_depth; //K_mDivGroup;
            uint X_width, X_height, X_depth, X_channels, X_channelsDiv4;

            uint X_channelsPerGroup; // if conv with group > 1, # of input channels per group
            uint O_channelsPerGroup; // if conv with group > 1, # of output channels per group; with or without group, this is always k.shape[1]
            uint X_channelsPerGroupDiv4Floor; // Same as above but int div by 4, ie floor(val/4)

            int StrideZ, StrideY, StrideX;

            // IMPORTANT: leave Pad? as int
            int PadZ, PadY, PadX;
            // = dilation *(kWindowSize - 1) - padorig = KExtentMinusOne - padorig
            int PadOrigZ, PadOrigY, PadOrigX;
            // padorig, which is the beginning padding original specification
            uint DilationZ, DilationY, DilationX;
            uint LCMOfStrideDilationX, LCMOfStrideDilationY, LCMOfStrideDilationZ;
            // = lcm(dilation, stride) * max(dilation, stride)
            uint LCMOfStrideDilationDivStrideX, LCMOfStrideDilationDivStrideY, LCMOfStrideDilationDivStrideZ;
            // = lcm(dilation, stride)/stride

            float4 ApplyFusedActivation(float4 v)
            {
                #ifdef RELU
                return max(v, 0);
                #endif
                return v;
            }

            uint CeilDiv(uint param, uint divisor)
            {
                return ((param + divisor - 1) / divisor);
            }

            uint AlignUpTo(uint param, uint divisor)
            {
                return CeilDiv(param, divisor) * divisor;
            }

            //
            // Dilation vs stride based optimizations:
            //
            // DILATION_GT_ONE_LTE_STRIDE and DILATION_GT_STRIDE
            // will respectively use the lcm(dilation, stride)/stride and lcm(dilation, stride)
            // as iteration step, one in input coord space and the other in output (but pad shifted) coordinate space.
            // If stride >= dilation, we can simply iterate in input coordinate space, but stepping by lcm
            // in output coordinate space is the same as stepping by lcm/stride in input coordinate space, as long as the
            // starting iterator position is aligned to stride (ie an actual input corresponds at that position).
            // When DILATION_GT_ONE_LTE_STRIDE, staying in that input space to iterate (instead of iterating in stride steps in output coord space)
            // avoids a division to convert from the iterating var to input coordinate index, but we still need to know if our filter
            // tap is valid and convert the kernel-window-start-relative-and-dilation-scaled-position
            // (since we iterate the kernel window coordinate in a scaled-by-dilation space to match the output coordinate space)
            // to the actual kernel spatial element index (filter tap) hence why we want to know if dilation is actually used (dilation > 1)

            // debug
            // #define DILATIONX_GT_ONE_LTE_STRIDE
            // #define DILATIONY_GT_ONE_LTE_STRIDE
            // #define DILATIONZ_GT_ONE_LTE_STRIDE
            // #define DILATIONX_GT_STRIDE
            // #define DILATIONY_GT_STRIDE
            // #define DILATIONZ_GT_STRIDE

            #if defined(DILATIONX_GT_ONE_LTE_STRIDE) || defined(DILATIONX_GT_STRIDE)
            #define DILATIONX_GT_ONE
            #endif
            #if defined(DILATIONY_GT_ONE_LTE_STRIDE) || defined(DILATIONY_GT_STRIDE)
            #define DILATIONY_GT_ONE
            #endif
            #if defined(DILATIONZ_GT_ONE_LTE_STRIDE) || defined(DILATIONZ_GT_STRIDE)
            #define DILATIONZ_GT_ONE
            #endif


            #if defined(DILATIONX_GT_ONE)
            #define DilationDef_X DilationX
            #define LCMOfStrideDilationDef_X LCMOfStrideDilationX
            #define LCMOfStrideDilationDivStrideDef_X LCMOfStrideDilationDivStrideX
            #else
            #define DilationDef_X 1
            #define LCMOfStrideDilationDef_X StrideX
            #define LCMOfStrideDilationDivStrideDef_X 1
            #endif
            #if defined(DILATIONY_GT_ONE)
            #define DilationDef_Y DilationY
            #define LCMOfStrideDilationDef_Y LCMOfStrideDilationY
            #define LCMOfStrideDilationDivStrideDef_Y LCMOfStrideDilationDivStrideY
            #else
            #define DilationDef_Y 1
            #define LCMOfStrideDilationDef_Y StrideY
            #define LCMOfStrideDilationDivStrideDef_Y 1
            #endif
            #if defined(DILATIONZ_GT_ONE)
            #define DilationDef_Z DilationZ
            #define LCMOfStrideDilationDef_Z LCMOfStrideDilationZ
            #define LCMOfStrideDilationDivStrideDef_Z LCMOfStrideDilationDivStrideZ
            #else
            #define DilationDef_Z 1
            #define LCMOfStrideDilationDef_Z StrideZ
            #define LCMOfStrideDilationDivStrideDef_Z 1
            #endif


            #if defined(DILATIONX_GT_ONE_LTE_STRIDE)
                #define IF_DILATIONX_GT_ONE_LTE_STRIDE_ELIF_GT_STRIDE_ELSE_FALSE(a, b) (a)
                #define IF_DILATIONX_GT_STRIDE_ELSE(a, b) (b)
            #elif defined(DILATIONX_GT_STRIDE)
                #define IF_DILATIONX_GT_ONE_LTE_STRIDE_ELIF_GT_STRIDE_ELSE_FALSE(a, b) (b)
                #define IF_DILATIONX_GT_STRIDE_ELSE(a, b) (a)
            #else
                #if defined(DILATIONX_GT_ONE)
                #error "Unknown DILATIONX mode!"
                #endif
                // when DILATIONX == 1, no loop iteration could yield an effectively zero entry
                #define IF_DILATIONX_GT_ONE_LTE_STRIDE_ELIF_GT_STRIDE_ELSE_FALSE(a, b) (false)
                #define IF_DILATIONX_GT_STRIDE_ELSE(a, b) (b)
            #endif

            #if defined(DILATIONY_GT_ONE_LTE_STRIDE)
                #define IF_DILATIONY_GT_ONE_LTE_STRIDE_ELIF_GT_STRIDE_ELSE_FALSE(a, b) (a)
                #define IF_DILATIONY_GT_STRIDE_ELSE(a, b) (b)
            #elif defined(DILATIONY_GT_STRIDE)
                #define IF_DILATIONY_GT_ONE_LTE_STRIDE_ELIF_GT_STRIDE_ELSE_FALSE(a, b) (b)
                #define IF_DILATIONY_GT_STRIDE_ELSE(a, b) (a)
            #else
                #if defined(DILATIONY_GT_ONE)
                #error "Unknown DILATIONY mode!"
                #endif
                // when DILATIONY == 1, no loop iteration could yield an effectively zero entry
                #define IF_DILATIONY_GT_ONE_LTE_STRIDE_ELIF_GT_STRIDE_ELSE_FALSE(a, b) (false)
                #define IF_DILATIONY_GT_STRIDE_ELSE(a, b) (b)
            #endif

            #if defined(DILATIONZ_GT_ONE_LTE_STRIDE)
                #define IF_DILATIONZ_GT_ONE_LTE_STRIDE_ELIF_GT_STRIDE_ELSE_FALSE(a, b) (a)
                #define IF_DILATIONZ_GT_STRIDE_ELSE(a, b) (b)
            #elif defined(DILATIONZ_GT_STRIDE)
                #define IF_DILATIONZ_GT_ONE_LTE_STRIDE_ELIF_GT_STRIDE_ELSE_FALSE(a, b) (b)
                #define IF_DILATIONZ_GT_STRIDE_ELSE(a, b) (a)
            #else
                #if defined(DILATIONZ_GT_ONE)
                #error "Unknown DILATIONZ mode!"
                #endif
                // when DILATIONZ == 1, no loop iteration could yield an effectively zero entry
                #define IF_DILATIONZ_GT_ONE_LTE_STRIDE_ELIF_GT_STRIDE_ELSE_FALSE(a, b) (false)
                #define IF_DILATIONZ_GT_STRIDE_ELSE(a, b) (b)
            #endif


            // Note: We will NOT USE Dilation? uniforms directly but DilationDef_?, this allows the compiler to optimize the / 1 or % 1
            // expressions without cluttering the code below.

            float4 frag(v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
            {
                uint blockIndexO = GetBlockIndexO(screenPos);
                uint strideCK = 1;
                uint strideCX = 1;
                uint n = blockIndexO;

                // out x spatial coord
                //
                int ox = n % O_width; // IMPORTANT: out spatial coord index "ox": leave as int
                n /= O_width;
                strideCK *= K_width;
                strideCX *= X_width;

                // Spatial ouput coordinate space iteration parameters:
                const int oxPadShiftedUnaligned = ox - PadX;
                #if defined(DILATIONX_GT_STRIDE)
                int oxPadShiftedUnalignedStart = oxPadShiftedUnaligned;
                if (oxPadShiftedUnalignedStart < 0)
                {
                    int tmp = oxPadShiftedUnalignedStart % (int)DilationDef_X;
                    oxPadShiftedUnalignedStart = (tmp == 0) ? 0 : tmp + DilationDef_X;
                }
                while(oxPadShiftedUnalignedStart < (ox + PadOrigX) && ((oxPadShiftedUnalignedStart % StrideX) != 0))
                    oxPadShiftedUnalignedStart += DilationDef_X;
                #else
                const uint oxPadShiftedUnalignedStart = max(oxPadShiftedUnaligned, 0);
                #endif

                // Spatial input coordinate space iteration parameters:
                uint ixStart = CeilDiv(oxPadShiftedUnalignedStart, StrideX);
                #if defined(DILATIONX_GT_STRIDE)
                const uint oxPadShiftedAlignedStart = oxPadShiftedUnalignedStart;
                #else
                const uint oxPadShiftedAlignedStart = ixStart * StrideX;
                #endif
                //const uint ixEndEx = min(X_width, (((K_width - 1) * DilationDef_X + 1 + ox - PadX + StrideX - 1) / StrideX));
                const uint oxPadShiftedEndEx = min(X_width * StrideX, ox + 1 + PadOrigX);
                const uint ixEndEx = min(X_width, CeilDiv(oxPadShiftedEndEx, StrideX));

                // Spatial kernel (ie kernel window) coordinate space iteration parameters:
                // when dilation < 1, this is directly the kernel spatial window coordinates,
                // when 1 < dilation <= stride, these are kernel window start-relative, but scaled by dilation,
                // ie these coordinates include the skipped over elements, so are like in output space but still kernel window-start relative
                // (hence the reason for testing eg (kx % DilationX == 0) and doing final_kernel_window_idx = kx / DilationX).
                uint kxStart = ox + PadOrigX - ixStart * StrideX; // (K_width - 1) * DilationDef_X - (ixStart * StrideX - ox + PadX)

                #if defined(CONVTRANSPOSE3D) | defined(CONVTRANSPOSE2D)
                // out x spatial coord
                //
                int oy = n % O_height; // IMPORTANT: leave as int
                n /= O_height;
                strideCK *= K_height;
                strideCX *= X_height;

                // Spatial ouput coordinate space iteration parameters:
                const int oyPadShiftedUnaligned = oy - PadY;
                #if defined(DILATIONY_GT_STRIDE)
                int oyPadShiftedUnalignedStart =  oyPadShiftedUnaligned;
                if (oyPadShiftedUnalignedStart < 0)
                {
                    int tmp = oyPadShiftedUnalignedStart % (int)DilationDef_Y;
                    oyPadShiftedUnalignedStart = (tmp == 0) ? 0 : tmp + DilationDef_Y;
                }
                while(oyPadShiftedUnalignedStart < (oy + PadOrigY) && ((oyPadShiftedUnalignedStart % StrideY) != 0))
                    oyPadShiftedUnalignedStart += DilationDef_Y;

                #else
                const uint oyPadShiftedUnalignedStart = max(oyPadShiftedUnaligned, 0);
                #endif

                // Spatial input coordinate space iteration parameters:
                uint iyStart = CeilDiv(oyPadShiftedUnalignedStart, StrideY);
                #if defined(DILATIONY_GT_STRIDE)
                const uint oyPadShiftedAlignedStart = oyPadShiftedUnalignedStart;
                #else
                const uint oyPadShiftedAlignedStart = iyStart * StrideY;
                #endif
                //const uint iyEndEx = min(X_height, (((K_height - 1) * DilationDef_Y + 1 + oy - PadY + StrideY - 1) / StrideY));
                const uint oyPadShiftedEndEx = min(X_height * StrideY, oy + 1 + PadOrigY);
                const uint iyEndEx = min(X_height, CeilDiv(oyPadShiftedEndEx, StrideY));

                // Spatial kernel (ie kernel window) coordinate space iteration parameters:
                uint kyStart = oy + PadOrigY - iyStart * StrideY; // (K_height - 1) * DilationDef_Y - (iyStart * StrideY - oy + PadY)
                #endif // #if defined(CONVTRANSPOSE3D) | defined(CONVTRANSPOSE2D)


                #if defined(CONVTRANSPOSE3D)
                // out z spatial coord
                //
                int oz = n % O_depth; // IMPORTANT: leave as int
                n /= O_depth;
                strideCK *= K_depth;
                strideCX *= X_depth;

                // Spatial ouput coordinate space iteration parameters:
                const int ozPadShiftedUnaligned = oz - PadZ;
                #if defined(DILATIONZ_GT_STRIDE)
                int ozPadShiftedUnalignedStart = ozPadShiftedUnaligned;
                if (ozPadShiftedUnalignedStart < 0)
                {
                    int tmp = ozPadShiftedUnalignedStart % (int)DilationDef_Z;
                    ozPadShiftedUnalignedStart = (tmp == 0) ? 0 : tmp + DilationDef_Z;
                }
                while(ozPadShiftedUnalignedStart < (oz + PadOrigZ) && ((ozPadShiftedUnalignedStart % StrideZ) != 0))
                    ozPadShiftedUnalignedStart += DilationDef_Z;

                #else
                const uint ozPadShiftedUnalignedStart = max(ozPadShiftedUnaligned, 0);
                #endif

                // Spatial input coordinate space iteration parameters:
                uint izStart = CeilDiv(ozPadShiftedUnalignedStart, StrideZ);
                #if defined(DILATIONZ_GT_STRIDE)
                const uint ozPadShiftedAlignedStart = ozPadShiftedUnalignedStart;
                #else
                const uint ozPadShiftedAlignedStart = izStart * StrideZ;
                #endif
                //const uint izEndEx = min(X_depth, (((K_depth - 1) * DilationDef_Z + 1 + oz - PadZ + StrideZ - 1) / StrideZ));
                const uint ozPadShiftedEndEx = min(X_depth * StrideZ, oz + 1 + PadOrigZ);
                const uint izEndEx = min(X_depth, CeilDiv(ozPadShiftedEndEx, StrideZ));

                // Spatial kernel (ie kernel window) coordinate space iteration parameters:
                uint kzStart = oz + PadOrigZ - izStart * StrideZ; // (K_depth - 1) * DilationDef_Z - (izStart * StrideZ - oz + PadZ)
                #endif // #if defined(CONVTRANSPOSE3D)

                uint kDiv4 = n % O_channelsDiv4;
                n /= O_channelsDiv4;
                const uint4 kOutChanXGroupIdx4 = UnblockAxis(kDiv4); // across groups if there are more than 1
                const uint4 kOutChanIdx4 = kOutChanXGroupIdx4 % O_channelsPerGroup;
                const uint4 outChanGroupIdx4 = kOutChanXGroupIdx4 / O_channelsPerGroup; // for each output channel, their group idx

                const uint4 kOutChanAxis1Offset4 = strideCK * kOutChanIdx4; // strideCK = kSpatialDimsTotalSize = ends up being output channel (axis 1 in conv tranpose) stride

                const uint xInChanBlockStride = strideCX;
                const uint kAxis0BlockStride = strideCK * O_channelsPerGroup; // kernel axis 1 is sized "O_channelsPerGroup" and axis 0 is 4-chunked ("blocked")

                #ifdef USEBIAS
                float4 acc4 = SampleBlockB(kDiv4);
                #else
                float4 acc4 = 0;
                #endif

                uint4 indexX = strideCX * X_channelsDiv4 * n; // n is the batch idx at this point
                uint4 indexK = 0;

                //
                // Increment / decrement vars

                uint kxDec = LCMOfStrideDilationDef_X;
                uint ixInc = LCMOfStrideDilationDivStrideDef_X;
                uint kyDec = LCMOfStrideDilationDef_Y;
                uint iyInc = LCMOfStrideDilationDivStrideDef_Y;
                uint kzDec = LCMOfStrideDilationDef_Z;
                uint izInc = LCMOfStrideDilationDivStrideDef_Z;

                #if defined(CONVTRANSPOSE3D)
                #if defined(DILATIONZ_GT_ONE_LTE_STRIDE)
                while(izStart < izEndEx && ((kzStart % DilationDef_Z != 0) != 0))
                {
                    izStart += 1;
                    kzStart -= StrideZ;
                }
                #endif
                #endif

                #if defined(CONVTRANSPOSE3D) || defined(CONVTRANSPOSE2D)
                #if defined(DILATIONY_GT_ONE_LTE_STRIDE)
                while(iyStart < iyEndEx && ((kyStart % DilationDef_Y != 0) != 0))
                {
                    iyStart += 1;
                    kyStart -= StrideY;
                }
                #endif
                #endif

                #if defined(DILATIONX_GT_ONE_LTE_STRIDE)
                while(ixStart < ixEndEx && ((kxStart % DilationDef_X != 0) != 0))
                {
                    ixStart += 1;
                    kxStart -= StrideX;
                }
                #endif

                uint kx = 11, ky = 11, kz = 11;
                uint ix = 12, iy = 12, iz = 12;
                uint oxP = 13, oyP = 13, ozP = 13; // oP spatial output numbering space but padshifted

                #if defined(CONVTRANSPOSE3D)
                for (ozP = ozPadShiftedAlignedStart, iz = izStart, kz = kzStart;
                    IF_DILATIONZ_GT_STRIDE_ELSE(ozP < ozPadShiftedEndEx, iz < izEndEx);
                    iz += izInc, ozP += LCMOfStrideDilationDef_Z, kz -= kzDec) // note ozP (output spatial coord pad-shifted) only used if dilation > stride
                    {
                    // Skip test (continue) for non-contributing position only if dilation > 1:
                    //
                    //  if dilation > 1 and <= stride, we test with kz and do:
                    //      if (kz % DilationDef_Z != 0)
                    //  if dilation > 1 and > stride, we test with ozP and do:
                    //      if (ozP % StrideZ != 0)
                    //if (IF_DILATIONZ_GT_ONE_LTE_STRIDE_ELIF_GT_STRIDE_ELSE_FALSE((kz % DilationDef_Z != 0), (ozP % StrideZ != 0)))
                    // but with the while() alignment loops above, we don't need that anymore
                    if (IF_DILATIONZ_GT_ONE_LTE_STRIDE_ELIF_GT_STRIDE_ELSE_FALSE(false, (ozP % StrideZ != 0)))
                        continue;

                    uint izFinal = IF_DILATIONZ_GT_STRIDE_ELSE(ozP / StrideZ, iz); // note: if DILATIONZ_GT_STRIDE, then iz vars and related code are never used and optimized out
                    indexX[2] = indexX[3] + izFinal * (X_width * X_height);
                    indexK[2] = indexK[3] + kz / DilationDef_Z * (K_width * K_height);
                #endif // #if defined(CONVTRANSPOSE3D)

                    #if defined(CONVTRANSPOSE3D) | defined(CONVTRANSPOSE2D)
                    for (oyP = oyPadShiftedAlignedStart, iy = iyStart, ky = kyStart;
                        IF_DILATIONY_GT_STRIDE_ELSE(oyP < oyPadShiftedEndEx, iy < iyEndEx);
                        iy += iyInc, oyP += LCMOfStrideDilationDef_Y, ky -= kyDec)
                    {
                        //if (IF_DILATIONY_GT_ONE_LTE_STRIDE_ELIF_GT_STRIDE_ELSE_FALSE((ky % DilationDef_Y != 0), (oyP % StrideY != 0)))
                        if (IF_DILATIONY_GT_ONE_LTE_STRIDE_ELIF_GT_STRIDE_ELSE_FALSE(false, (oyP % StrideY != 0)))
                            continue;

                        uint iyFinal = IF_DILATIONY_GT_STRIDE_ELSE(oyP / StrideY, iy); // note: if DILATIONY_GT_STRIDE, then iy vars and related code are never used and optimized out
                        indexX[1] = indexX[2] + iyFinal * X_width;
                        indexK[1] = indexK[2] + ky / DilationDef_Y * K_width;
                    #endif // #if defined(CONVTRANSPOSE3D) | defined(CONVTRANSPOSE2D)

                        for (oxP = oxPadShiftedAlignedStart, ix = ixStart, kx = kxStart;
                            IF_DILATIONX_GT_STRIDE_ELSE(oxP < oxPadShiftedEndEx, ix < ixEndEx);
                            ix += ixInc, oxP += LCMOfStrideDilationDef_X, kx -= kxDec)
                        {
                            //if (IF_DILATIONX_GT_ONE_LTE_STRIDE_ELIF_GT_STRIDE_ELSE_FALSE((kx % DilationDef_X != 0), (oxP % StrideX != 0)))
                            if (IF_DILATIONX_GT_ONE_LTE_STRIDE_ELIF_GT_STRIDE_ELSE_FALSE(false, (oxP % StrideX != 0)))
                                continue;

                            uint ixFinal = IF_DILATIONX_GT_STRIDE_ELSE(oxP / StrideX, ix); // note: if DILATIONX_GT_STRIDE, then ix vars and related code are never used and optimized out
                            indexX[0] = indexX[1] + ixFinal;
                            indexK[0] = indexK[1] + kx / DilationDef_X;

                        #if !defined(GROUPS_ENABLED)
                            for (uint cDiv4 = 0; cDiv4 < X_channelsDiv4; cDiv4++)
                            {
                                // Note blockIndex is the index of a 4-chunk, and we know in this case we setup our X texture to pack axis 0 (input channels) in a 4-chunk (in the 4 texture channels)
                                // and for the K texture also on axis 0 (which are also the input channels in conv transpose).
                                uint blockIndexX = indexX[0] + cDiv4 * xInChanBlockStride;
                                uint4 blockIndexK = indexK[0] + kOutChanAxis1Offset4 + cDiv4 * kAxis0BlockStride; // kOutChanAxis1Offset4 is for 4 output channels, indexK[0] is for spatial offset
                                float4 v = SampleBlockX(blockIndexX); // X input value at calculated spatial pos for 4 input channels
                                v *= (UnblockAxis(cDiv4) < X_channels ? 1.0f : 0.0f); // mask leftovers

                                float4 k0 = SampleBlockK(blockIndexK[0]); // each is 4 outermost channels (axis 0, output in conv non transpose, but input in conv transpose)
                                float4 k1 = SampleBlockK(blockIndexK[1]);
                                float4 k2 = SampleBlockK(blockIndexK[2]);
                                float4 k3 = SampleBlockK(blockIndexK[3]);

                                acc4 += mul(float4x4(k0, k1, k2, k3), v);
                            }
                        #else

                        #if defined(INNER_SUM_TRY_BLOCK_REUSE)

                            // hard method, maybe faster in some cases, but not when there's not enough inner channels
                            // for which to sum

                            // we have to deal with groups

                            // For each of the 4 output channels, their input channel offset in X may be different
                            // (due to groups that may be different).
                            // Depending on group number and the number of input channels per group,
                            // earch may also land start anywhere in a 4-chunk block unit, so we do the possibly
                            // partial blocks for each first, then the full blocks and finally the remainder.

                            uint4 firstInChanXGroupOffsetTmp4 = outChanGroupIdx4 * X_channelsPerGroup;
                            uint4 inChanGroupBlockOffsets4 = (firstInChanXGroupOffsetTmp4) >> 2; // / 4;
                            uint4 xInChanGroupBlockOffsets4 = inChanGroupBlockOffsets4 * xInChanBlockStride;
                            uint4 kInChanGroupBlockOffsets4 = inChanGroupBlockOffsets4 * kAxis0BlockStride;

                            // Note that for the rest of input channels tracking vars, they apply to both kernel and x input data:
                            uint4 inChanGroupFirstBlockSubBlockOffset4 = (firstInChanXGroupOffsetTmp4) & 3; // % 4;

                            uint4 inChanTodoInFirstBlock4 = min((uint4)(X_channelsPerGroup), 4 - inChanGroupFirstBlockSubBlockOffset4) & 3; // %4
                            uint4 inChanFullBlocksNum4 = (X_channelsPerGroup - inChanTodoInFirstBlock4) >> 2; // /4;
                            uint4 inChanTodoInLastBlock4 = (X_channelsPerGroup - inChanTodoInFirstBlock4) & 3; // % 4

                            bool4 activeMask4 = inChanTodoInFirstBlock4 > 0;
                            uint cDiv4 = 0;

                            uint4 blockIndexX = indexX[0] + xInChanGroupBlockOffsets4;// + cDiv4 * xInChanBlockStride;
                            uint4 blockIndexK = indexK[0] + kOutChanAxis1Offset4 + kInChanGroupBlockOffsets4;// + cDiv4 * kAxis0BlockStride;

                            if (any(activeMask4))
                            {
                                uint4 bidx4 = inChanGroupFirstBlockSubBlockOffset4;
                                uint4 limitEx = inChanGroupFirstBlockSubBlockOffset4 + inChanTodoInFirstBlock4;

                                // Note that some blockIndexX[i], maybe all, will refer to the same data,
                                // it depends if each of the 4 output channel number is in the same group or not,
                                // but the texture cache should cover that redundancy pretty well.
                                float4 v0 = 0;
                                float4 v1 = 0;
                                float4 v2 = 0;
                                float4 v3 = 0;

                                float4 k0 = 0;
                                float4 k1 = 0;
                                float4 k2 = 0;
                                float4 k3 = 0;

                                // 4 partial sums of the loaded blocks:
                                if (activeMask4[0])
                                {
                                    v0 = SampleBlockX(blockIndexX[0]);
                                    k0 = SampleBlockK(blockIndexK[0]);
                                    blockIndexX[0] += xInChanBlockStride;
                                    blockIndexK[0] += kAxis0BlockStride;
                                    uint ri = inChanGroupFirstBlockSubBlockOffset4[0];
                                    acc4[0] += k0[ri] * v0[ri];
                                }
                                if (activeMask4[1])
                                {
                                    v1 = SampleBlockX(blockIndexX[1]);
                                    k1 = SampleBlockK(blockIndexK[1]);
                                    blockIndexX[1] += xInChanBlockStride;
                                    blockIndexK[1] += kAxis0BlockStride;
                                    uint ri = inChanGroupFirstBlockSubBlockOffset4[1];
                                    acc4[1] += k1[ri] * v1[ri];
                                }
                                if (activeMask4[2])
                                {
                                    v2 = SampleBlockX(blockIndexX[2]);
                                    k2 = SampleBlockK(blockIndexK[2]);
                                    blockIndexX[2] += xInChanBlockStride;
                                    blockIndexK[2] += kAxis0BlockStride;
                                    uint ri = inChanGroupFirstBlockSubBlockOffset4[2];
                                    acc4[2] += k2[ri] * v2[ri];
                                }
                                if (activeMask4[3])
                                {
                                    v3 = SampleBlockX(blockIndexX[3]);
                                    k3 = SampleBlockK(blockIndexK[3]);
                                    blockIndexX[3] += xInChanBlockStride;
                                    blockIndexK[3] += kAxis0BlockStride;
                                    uint ri = inChanGroupFirstBlockSubBlockOffset4[3];
                                    acc4[3] += k3[ri] * v3[ri];
                                }

                                bidx4 += (uint4)(1);// we just did one position of each
                                [unroll(4)]
                                for (uint ri = 1; ri < 4; ri++)
                                {
                                    if (bidx4[0] < limitEx[0])
                                        acc4[0] += k0[bidx4[0]] * v0[bidx4[0]];
                                    if (bidx4[1] < limitEx[1])
                                        acc4[1] += k1[bidx4[1]] * v1[bidx4[1]];
                                    if (bidx4[2] < limitEx[2])
                                        acc4[2] += k2[bidx4[2]] * v2[bidx4[2]];
                                    if (bidx4[3] < limitEx[3])
                                        acc4[3] += k3[bidx4[3]] * v3[bidx4[3]];

                                    bidx4 += (uint4)(1);// we just did one position of each
                                }
                            }
                            // ...we did the first partial block of input channels


                            // process input channels in 4-chunks if we have more than 4 to sum per group:
                            {
                                activeMask4 = inChanFullBlocksNum4 > 0; // inChanTodoInFirstBlock4 > 0
                                uint4 inc4 = 0;
                                cDiv4 = 0; // debug: limit to avoid hangs in case of a bug
                                while(any(activeMask4) && cDiv4 < X_channelsPerGroupDiv4Floor)
                                {
                                    float4 v0 = 0;
                                    float4 v1 = 0;
                                    float4 v2 = 0;
                                    float4 v3 = 0;

                                    float4 k0 = 0;
                                    float4 k1 = 0;
                                    float4 k2 = 0;
                                    float4 k3 = 0;

                                    if (activeMask4[0])
                                    {
                                        v0 = SampleBlockX(blockIndexX[0]);
                                        k0 = SampleBlockK(blockIndexK[0]);
                                        blockIndexX[0] += xInChanBlockStride;
                                        blockIndexK[0] += kAxis0BlockStride;
                                        inc4[0]++;
                                        activeMask4[0] = inc4[0] < inChanFullBlocksNum4[0];
                                        acc4[0] += dot(k0, v0);
                                    }
                                    if (activeMask4[1])
                                    {
                                        v1 = SampleBlockX(blockIndexX[1]);
                                        k1 = SampleBlockK(blockIndexK[1]);
                                        blockIndexX[1] += xInChanBlockStride;
                                        blockIndexK[1] += kAxis0BlockStride;
                                        inc4[1]++;
                                        activeMask4[1] = inc4[1] < inChanFullBlocksNum4[1];
                                        acc4[1] += dot(k1, v1);
                                    }
                                    if (activeMask4[2])
                                    {
                                        v2 = SampleBlockX(blockIndexX[2]);
                                        k2 = SampleBlockK(blockIndexK[2]);
                                        blockIndexX[2] += xInChanBlockStride;
                                        blockIndexK[2] += kAxis0BlockStride;
                                        inc4[2]++;
                                        activeMask4[2] = inc4[2] < inChanFullBlocksNum4[2];
                                        acc4[2] += dot(k2, v2);
                                    }
                                    if (activeMask4[3])
                                    {
                                        v3 = SampleBlockX(blockIndexX[3]);
                                        k3 = SampleBlockK(blockIndexK[3]);
                                        blockIndexX[3] += xInChanBlockStride;
                                        blockIndexK[3] += kAxis0BlockStride;
                                        inc4[3]++;
                                        activeMask4[3] = inc4[3] < inChanFullBlocksNum4[3];
                                        acc4[3] += dot(k3, v3);
                                    }
                                    cDiv4++;
                                }
                            }
                            // we did the full 4-chunk blocks of input channels

                            // process remainder input channels:
                            {
                                activeMask4 = inChanTodoInLastBlock4 > 0;
                                if (any(activeMask4))
                                {
                                    float4 v0 = 0;
                                    float4 v1 = 0;
                                    float4 v2 = 0;
                                    float4 v3 = 0;

                                    float4 k0 = 0;
                                    float4 k1 = 0;
                                    float4 k2 = 0;
                                    float4 k3 = 0;

                                    if (activeMask4[0])
                                    {
                                        v0 = SampleBlockX(blockIndexX[0]);
                                        k0 = SampleBlockK(blockIndexK[0]);
                                    }
                                    if (activeMask4[1])
                                    {
                                        v1 = SampleBlockX(blockIndexX[1]);
                                        k1 = SampleBlockK(blockIndexK[1]);
                                    }
                                    if (activeMask4[2])
                                    {
                                        v2 = SampleBlockX(blockIndexX[2]);
                                        k2 = SampleBlockK(blockIndexK[2]);
                                    }
                                    if (activeMask4[3])
                                    {
                                        v3 = SampleBlockX(blockIndexX[3]);
                                        k3 = SampleBlockK(blockIndexK[3]);
                                    }

                                    // 4 partial sums of the loaded blocks:
                                    uint ri = 0;

                                    acc4[0] += k0[ri] * v0[ri];
                                    acc4[1] += k1[ri] * v1[ri];
                                    acc4[2] += k2[ri] * v2[ri];
                                    acc4[3] += k3[ri] * v3[ri];
                                    uint4 inc4 = 1;

                                    [unroll(4)]
                                    for (ri = 1; ri < 4; ri++)
                                    {
                                        if (inc4[0] < inChanTodoInLastBlock4[0])
                                            acc4[0] += k0[ri] * v0[ri];
                                        if (inc4[1] < inChanTodoInLastBlock4[1])
                                            acc4[1] += k1[ri] * v1[ri];
                                        if (inc4[2] < inChanTodoInLastBlock4[2])
                                            acc4[2] += k2[ri] * v2[ri];
                                        if (inc4[3] < inChanTodoInLastBlock4[3])
                                            acc4[3] += k3[ri] * v3[ri];

                                        inc4 += (uint4)(1);
                                    }
                                }
                            }

                        // ...#if defined(INNER_SUM_TRY_BLOCK_REUSE)
                        // method to use when GROUPS_ENABLED
                        #else
                            {
                                // This hides 8 loads per iteration
                                for (uint inChan = 0; inChan < X_channelsPerGroup; inChan++)
                                {
                                    uint4 inChanXGroupIdx4 = outChanGroupIdx4 * X_channelsPerGroup + inChan;
                                    uint4 blockIndexX4 = indexX[0] + (inChanXGroupIdx4 >> 2) * xInChanBlockStride;
                                    uint4 blockIndexK4 = indexK[0] + kOutChanAxis1Offset4 + (inChanXGroupIdx4 >> 2) * kAxis0BlockStride;
                                    float4 v = SampleElementsX(blockIndexX4, inChanXGroupIdx4 & 3);
                                    float4 k = SampleElementsK(blockIndexK4, inChanXGroupIdx4 & 3);
                                    acc4 += v * k;
                                }
                            }
                        #endif // #if defined(INNER_SUM_TRY_BLOCK_REUSE)
                        #endif //#if !defined(GROUPS_ENABLED)
                        }
                    #if defined(CONVTRANSPOSE3D) | defined(CONVTRANSPOSE2D)
                    }
                    #endif
                #if defined(CONVTRANSPOSE3D)
                }
                #endif

                return ApplyFusedActivation(acc4);
            }
            ENDCG
        }
    }
}
