Shader "Hidden/Sentis/LocalResponseNormalization"
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
            #include "Packages/com.unity.ai.inference/Runtime/Core/Resources/Sentis/ComputeShaders/Tensor.cginc"

            DECLARE_TENSOR(X, float);

            uint X_channels;
            uint X_channelsDiv4; // CeilDiv4 actually
            uint X_strideC; // channel axis elements stride

            uint LeftSupportLength;
            // ...excludes the center point so (center_pos + support_length) gives the position of the first block, inclusive.
            // Can only be 0 if support is 1.

            uint RightSupportLength;
            uint RightSupportLengthCeilDiv4;
            float AlphaDivSupportLength;

            float Bias;
            float Beta;

            float4 frag(v2f inv2f, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
            {
                uint blockIndexO = GetBlockIndexO(screenPos);
                float4 inputBlockVal = SampleBlockX(blockIndexO);

                uint spatialsNum = blockIndexO % X_strideC;
                uint xbatchoutChannelBlockNum = blockIndexO / X_strideC;
                int outChannelBlockNum = xbatchoutChannelBlockNum % X_channelsDiv4; // main / center block
                uint batchNum = xbatchoutChannelBlockNum / X_channelsDiv4;

                uint channelStride = X_strideC;

                float4 res;
                float sumOfSq[4];
                [unroll]
                for (uint i = 0; i < 4; i++)
                    sumOfSq[i] = 0;


                // We need to handle 4 output channel values per fragment invocation,
                // we will refer to the 4 components "output 0 1 2 3".

                uint channelForOut0 = outChannelBlockNum << 2;
                int4 channelForOutputs = (int4)(channelForOut0) + int4(0, 1, 2, 3);
                // ...this is the shader invocation (pixel) channel number corresponding to component #0 of the center (and output) 4-chunk block.
                int supportUnclampedStartChanForOut0 = (int)channelForOut0 - (int)LeftSupportLength;
                uint supportStartChanForOut0 = max(0, supportUnclampedStartChanForOut0);
                uint4 supportStartChanForOutputs = max(0, channelForOutputs - (int4)(LeftSupportLength));

                uint supportStartBlockNum = supportStartChanForOut0 >> 2;

                int curChanBlockNum = supportStartBlockNum; // current channel block num
                uint curChanBlockTexelIdx = (batchNum * X_channelsDiv4 + curChanBlockNum) * channelStride + spatialsNum;

                // True (saturated to tensor boundary) ending channel number for element 0 and 3:
                // We need both those points for 0 since it is the first to end, and 3 since it's the last:
                uint supportEndChanForOut0 = min(X_channels - 1, channelForOut0 + RightSupportLength);
                uint supportEndChanForLastOut = min(X_channels - 1, channelForOut0 + 3 + RightSupportLength);


                // 4-channel block limit:
                int supportUnclampedEndBlockNum = (int)outChannelBlockNum + (int)RightSupportLengthCeilDiv4; // inclusive
                int supportEndBlockNum = min((int)X_channelsDiv4 - 1, supportUnclampedEndBlockNum);
                //
                // First 1 to 3 blocks processing: these can be partial blocks:
                //
                uint elementsStarted = 0;
                uint nextElementToEnd = 0;

                // Unrolled first iteration of
                // while ((curChanBlockNum <= supportEndBlockNum) && (elementsStarted < 4)) : not reached past the last block and not all sums started (see end of while for comments)
                {
                    // load first block value: there should be at least one block
                    float4 blockVal = SampleBlockX(curChanBlockTexelIdx);

                    uint curBlockChannelForSubElem0 = curChanBlockNum << 2;

                    // note the block could be the only one for very short tensors, so we could have not only 4 elements total
                    uint curBlockChannelForLastBlockElem = min(X_channels - 1, curBlockChannelForSubElem0 + 3);

                    // we start at the first channel num of the first output (0) support's start:
                    uint curChannelIdx = supportStartChanForOut0;
                    uint curSubBlockIdx = curChannelIdx - curBlockChannelForSubElem0; // note supportStartChanForOut0 >= curBlockChannelForSubElem0

                    // Inclusive limit for starting the sum of square sequence in this first channel block:
                    // we need to stop when we reach either the end of the 4-component block or the end of the right support
                    // for overall output number 0:
                    uint curBlockChannelStartingLoopLimit = min(curBlockChannelForLastBlockElem, supportEndChanForOut0);

                    // manually unrolled loop to simplify as we only have max 4 iterations
                    // compiler will remove the ++ etc:
                    //
                    // note here in first block elementsStarted == 0
                    if (curChannelIdx <= curBlockChannelStartingLoopLimit)
                    {
                        float val = blockVal[curSubBlockIdx++];
                        float curSqVal = val * val;
                        sumOfSq[0] = curSqVal;
                        elementsStarted++;

                        // special case in the very first block: could have left support of
                        // other starting element < 0:
                        if (supportStartChanForOutputs[1] == 0)
                            sumOfSq[elementsStarted++] = curSqVal;
                        if (supportStartChanForOutputs[2] == 0)
                            sumOfSq[elementsStarted++] = curSqVal;
                        if (supportStartChanForOutputs[3] == 0)
                            sumOfSq[elementsStarted++] = curSqVal;

                        curChannelIdx++;
                    }
                    if (curChannelIdx <= curBlockChannelStartingLoopLimit)
                    {
                        float val = blockVal[curSubBlockIdx++];
                        float curSqVal = val * val;
                        sumOfSq[0] += curSqVal;

                        // these may already be started too, see above:
                        // we need to add for max(4, all elements started + 1) (+1 is the potentially new one)

                        //if (supportStartChanForOutputs[1] <= 1)
                        if (elementsStarted >= 2)
                            sumOfSq[1] += curSqVal;
                        //if (supportStartChanForOutputs[2] <= 1)
                        if (elementsStarted >= 3)
                            sumOfSq[2] += curSqVal;
                        //if (supportStartChanForOutputs[3] <= 1)
                        if (elementsStarted >= 4)
                            sumOfSq[3] += curSqVal;

                        // potential new one:
                        if (elementsStarted < 4)
                            sumOfSq[elementsStarted] += curSqVal;

                        elementsStarted = min(elementsStarted + 1, 4);
                        curChannelIdx++;
                    }
                    if (curChannelIdx <= curBlockChannelStartingLoopLimit)
                    {
                        float val = blockVal[curSubBlockIdx++];
                        float curSqVal = val * val;
                        sumOfSq[0] += curSqVal;
                        sumOfSq[1] += curSqVal;  // elementsStarted is at least 2, so do the first 2 elements

                        // these may already be started too, see above:
                        if (elementsStarted >= 3)
                            sumOfSq[2] += curSqVal;
                        if (elementsStarted >= 4)
                            sumOfSq[3] += curSqVal;

                        // potential new one:
                        if (elementsStarted < 4)
                            sumOfSq[elementsStarted] += curSqVal;

                        elementsStarted = min(elementsStarted + 1, 4);
                        curChannelIdx++;
                    }
                    if (curChannelIdx <= curBlockChannelStartingLoopLimit)
                    {
                        float val = blockVal[curSubBlockIdx++];
                        float curSqVal = val * val;
                        sumOfSq[0] += curSqVal;
                        sumOfSq[1] += curSqVal;
                        sumOfSq[2] += curSqVal;
                        sumOfSq[3] += curSqVal; // this one is already started or will start now
                        elementsStarted = 4;
                        curChannelIdx++;
                    }

                    // Even if we dont enter the ending sequence below, 
                    // supportEndChanForOut0 could be == curBlockChannelForLastBlockElem
                    // take care of that here: ie we could already have ended out0
                    if ((supportEndChanForOut0 + 1) == curChannelIdx)
                        nextElementToEnd = 1;

                    // If curSubBlockIdx < 4, it's because we reached supportEndChanForOut0,
                    // in that case we need to start the ending sequence:
                    // At each iteration, one sum must end, but also another potentially starts:
                    if (curSubBlockIdx < 4)
                    {
                        nextElementToEnd = 1;

                        curBlockChannelStartingLoopLimit = curBlockChannelForLastBlockElem;
                        // ...note for this limit: the (right) support end of the last channel to process
                        // can't come before the full channel limit (X_channels - 1),
                        // but only when we're in the starting block (left most of out0 support), so in a 4-element block either 
                        // before the output block, or at the same position as the output block itself.
                        // Otherwise the limit would be min(supportEndChanForLastOut, curBlockChannelForLastBlockElem)
                        // We potentially loop for 1 to 3 blocks so we do the full expression here (see at loop body end also):
                        //curBlockChannelStartingLoopLimit = min(supportEndChanForLastOut, curBlockChannelForLastBlockElem);
                        //
                        // Also, since we handle at a max for 4 elements in parallel, we have
                        // at most 3 left, 1 that ends for sure, possibly 1 or 2 that continue,
                        // and possibly one that starts:
                        
                        // manually unrolled loop to simplify as we only have max 3 iterations
                        // compiler will remove the ++ etc:
                        if (curChannelIdx <= curBlockChannelStartingLoopLimit)
                        {
                            // max 3 elements to process
                            float val = blockVal[curSubBlockIdx++];
                            float curSqVal = val * val;
                            // 1 ending element:
                            sumOfSq[nextElementToEnd] += curSqVal;

                            // max 1 to 2 elements to continue: 1 continue + 1 start, or 2 continue
                            uint toContinueIdx = nextElementToEnd + 1;

                            // 2 possible here:
                            if (toContinueIdx < elementsStarted)
                                sumOfSq[toContinueIdx++] += curSqVal;
                            if (toContinueIdx < elementsStarted)
                                sumOfSq[toContinueIdx++] += curSqVal;

                            // max 1 element to start:
                            if (elementsStarted > nextElementToEnd) // in case we have support size == 1 !
                            if (elementsStarted < 4)
                            {
                                sumOfSq[elementsStarted] += curSqVal;
                            }
                            elementsStarted = min(elementsStarted + 1, 4);

                            nextElementToEnd++;
                            curChannelIdx++;
                        }
                        if (curChannelIdx <= curBlockChannelStartingLoopLimit)
                        {
                            // max 2 elements to process
                            float val = blockVal[curSubBlockIdx++];
                            float curSqVal = val * val;
                            // 1 ending element:
                            sumOfSq[nextElementToEnd] += curSqVal;

                            // max 1 element to continue:
                            uint toContinueIdx = nextElementToEnd + 1;
                            
                            // Only 1 possible here:
                            if (toContinueIdx < elementsStarted)
                                sumOfSq[toContinueIdx++] += curSqVal;

                            // max 1 element to start:
                            if (elementsStarted > nextElementToEnd) // in case we have support size == 1 !
                            if (elementsStarted < 4)
                                sumOfSq[elementsStarted] += curSqVal;

                            elementsStarted = min(elementsStarted + 1, 4);

                            nextElementToEnd++;
                            curChannelIdx++;
                        }

#if UNITY_PLATFORM_ANDROID
                        // On some Android platform, there's another bad code generation
                        // that doesn't properly test (curChannelIdx <= curBlockChannelStartingLoopLimit).
                        // This is a workaround: 
                        // test == 10 <=> curChannelIdx == curBlockChannelStartingLoopLimit,
                        // but just adding || (curChannelIdx == curBlockChannelStartingLoopLimit) below obviously
                        // won't work since it's easily folded into the other condition:
                        float test = (curChannelIdx - curBlockChannelStartingLoopLimit + 1) * 10;
                        if (test == 10 || curChannelIdx <= curBlockChannelStartingLoopLimit)
#else
                        if (curChannelIdx <= curBlockChannelStartingLoopLimit)
#endif
                        {
                            // max 1 element to process
                            float val = blockVal[curSubBlockIdx++];
                            float curSqVal = val * val;
                            // 1 ending element:
                            sumOfSq[nextElementToEnd] += curSqVal;
                            nextElementToEnd++;
                            curChannelIdx++;
                        }
                    } // ending sequence has started


                    curChanBlockNum++;
                    curChanBlockTexelIdx += channelStride;

                    // When the first block (call the iteration type "B") is finished, for next part consider:
                    // A - we're all done (curChannelIdx == X_channels or supportEndChanForLastOut+1 but see 
                    // see note above about first block and min(supportEndChanForLastOut, curBlockChannelForLastBlockElem)),
                    // B - we have more elements to start in the next block, with possibly some or all ending
                    // C - we have a series of full blocks to add 
                    //
                    // We've done one "B", The next steps can be thus be:
                    //      A, B-A, B-B-A (start more in one block, all end in the next),
                    //      B-C-A (start more, cont.), B-C-B-A, B-C-B-B-A,
                    //      C-A, C-B-A, ...these 2 is if all 4 sums already started and some full blocks to do:
                    //
                    // So we need to test for B, C (with a condition that excludes there's another B), B again,
                    // with every test for B, C including the condition for A, and with C including a condition for A also.
                    //
                    // In regex style: B (start), B{0,2}, C?, B{0,2}

                    // In this "B" block, we don't loop, it is the first block processed
                }

                while ((curChanBlockNum <= supportEndBlockNum) && (elementsStarted < 4)) // not reached past the last block and not all sums started (see end of while for comments)
                {
                    // load next block value
                    float4 blockVal = SampleBlockX(curChanBlockTexelIdx);

                    uint curBlockChannelForSubElem0 = curChanBlockNum << 2;

                    // the block could be the only one left, so we could have not only 4 elements total
                    uint curBlockChannelForLastBlockElem = min(X_channels - 1, curBlockChannelForSubElem0 + 3);

                    // The first unrolled iteration of the while was started at the first channel num of the first output (0) support's start:
                    //  uint curChannelIdx = supportStartChanForOut0;
                    //  uint curSubBlockIdx = curChannelIdx - curBlockChannelForSubElem0; (with supportStartChanForOut0 >= curBlockChannelForSubElem0)
                    // now we can start at curSubBlockIdx = 0 in the current block::
                    uint curChannelIdx = curBlockChannelForSubElem0;
                    uint curSubBlockIdx = 0;

                    // The sum of square sequence has already started in this loop.
                    // We for sure have elements still to start (see (elementsStarted < 4) above)
                    // and at most, 3 sums to continue:
                    // Inclusive limit for the rest of the startup of the sum of square sequence:
                    // we need to stop when we reach either the end of the 4-component block or the end of the right support
                    // for overall output number 0: 
                    //
                    // NOTE: if we actually already ended the out0 sequence, we skip the first unrolled loop:
                    uint curBlockChannelStartingLoopLimit = min(curBlockChannelForLastBlockElem, supportEndChanForOut0);

                    // manually unrolled loop to simplify as we only have max 4 iterations
                    // compiler will remove the ++ etc:
                    if (curChannelIdx <= curBlockChannelStartingLoopLimit)
                    {
                        float val = blockVal[curSubBlockIdx++];
                        float curSqVal = val * val;

                        //uint toContinueIdx = 0;
                        //if (toContinueIdx < elementsStarted) : need to continue at least one, max 3
                            sumOfSq[0] += curSqVal;
                        if (1 < elementsStarted)
                            sumOfSq[1] += curSqVal;
                        if (2 < elementsStarted)
                            sumOfSq[2] += curSqVal;

                        //if (elementsStarted < 4) : need to start at least one
                        sumOfSq[elementsStarted] = curSqVal;
                        elementsStarted++;
                        curChannelIdx++;
                    }
                    if (curChannelIdx <= curBlockChannelStartingLoopLimit)
                    {
                        float val = blockVal[curSubBlockIdx++];
                        float curSqVal = val * val;

                        //uint toContinueIdx = 0;

                        sumOfSq[0] += curSqVal;
                        sumOfSq[1] += curSqVal;
                        // ...need to continue at least 2, max 4
                        if (2 < elementsStarted)
                            sumOfSq[2] += curSqVal;
                        if (3 < elementsStarted)
                            sumOfSq[3] += curSqVal;

                        if (elementsStarted < 4)
                        {
                            sumOfSq[elementsStarted] = curSqVal;
                            elementsStarted++;
                        }
                        curChannelIdx++;
                    }
                    if (curChannelIdx <= curBlockChannelStartingLoopLimit)
                    {
                        float val = blockVal[curSubBlockIdx++];
                        float curSqVal = val * val;

                        //uint toContinueIdx = 0;

                        sumOfSq[0] += curSqVal;
                        sumOfSq[1] += curSqVal;
                        sumOfSq[2] += curSqVal;
                        // ...need to continue at least 3, max 4
                        if (3 < elementsStarted)
                            sumOfSq[3] += curSqVal;

                        if (elementsStarted < 4)
                        {
                            sumOfSq[elementsStarted] = curSqVal;
                            elementsStarted++;
                        }
                        curChannelIdx++;
                    }
                    if (curChannelIdx <= curBlockChannelStartingLoopLimit)
                    {
                        float val = blockVal[curSubBlockIdx++];
                        float curSqVal = val * val;

                        sumOfSq[0] += curSqVal;
                        sumOfSq[1] += curSqVal;
                        sumOfSq[2] += curSqVal;
                        sumOfSq[3] += curSqVal;
                        // ...need to continue all 4 if we reach here
                        // and none can start.
                        curChannelIdx++;
                    }

                    // Even if we dont enter the ending sequence below, 
                    // supportEndChanForOut0 could be == curBlockChannelForLastBlockElem
                    // take care of that here: ie we could already have ended out0
                    // otherwise we don't touch nextElementToEnd, it is
                    // already correctly set!
                    if ((supportEndChanForOut0 + 1) == curChannelIdx)
                        nextElementToEnd = 1;


                    // If curSubBlockIdx < 4, it's because we reached supportEndChanForOut0,
                    // in that case we need to start the ending sequence:
                    // At each iteration, one sum must end, but also another potentially starts:
                    if (curSubBlockIdx < 4)
                    {
                        // limit: the (right) support end of the last channel to process
                        // or the full channel limit (X_channels - 1),
                        curBlockChannelStartingLoopLimit = min(supportEndChanForLastOut, curBlockChannelForLastBlockElem);
                        //
                        // Also, since we handle at a max for 4 elements in parallel, we have
                        // from 1 to 4 left to end (1 that ends for sure), from 0 to 3 that continue,
                        // and from 0 to 3 that start (can be 0 because of the unrolled loop above):

                        // manually unrolled loop to simplify as we only have max 3 iterations
                        // compiler will remove the ++ etc:
                        if (curChannelIdx <= curBlockChannelStartingLoopLimit)
                        {
                            // max 4 elements to process
                            float val = blockVal[curSubBlockIdx++];
                            float curSqVal = val * val;

                            // 1 ending element max per iteration:
                            sumOfSq[nextElementToEnd] += curSqVal;

                            // max 3 elements to continue (up to those which start)
                            uint toContinueIdx = nextElementToEnd + 1;

                            if (toContinueIdx < elementsStarted)
                                sumOfSq[toContinueIdx++] += curSqVal;
                            if (toContinueIdx < elementsStarted)
                                sumOfSq[toContinueIdx++] += curSqVal;
                            if (toContinueIdx < elementsStarted)
                                sumOfSq[toContinueIdx++] += curSqVal;

                            // max 3 that start
                            if (elementsStarted < 4)
                                sumOfSq[elementsStarted++] += curSqVal;
                            if (elementsStarted < 4)
                                sumOfSq[elementsStarted++] += curSqVal;
                            if (elementsStarted < 4)
                                sumOfSq[elementsStarted++] += curSqVal;

                            nextElementToEnd++;
                            curChannelIdx++;
                        }
                        if (curChannelIdx <= curBlockChannelStartingLoopLimit)
                        {
                            // max 2 elements to process
                            float val = blockVal[curSubBlockIdx++];
                            float curSqVal = val * val;

                            // 1 ending element max per iteration:
                            sumOfSq[nextElementToEnd] += curSqVal;

                            // max 2 elements to continue (up to those which start)
                            uint toContinueIdx = nextElementToEnd + 1;

                            if (toContinueIdx < elementsStarted)
                                sumOfSq[toContinueIdx++] += curSqVal;
                            if (toContinueIdx < elementsStarted)
                                sumOfSq[toContinueIdx++] += curSqVal;

                            // max 2 that start
                            if (elementsStarted < 4)
                                sumOfSq[elementsStarted++] += curSqVal;
                            if (elementsStarted < 4)
                                sumOfSq[elementsStarted++] += curSqVal;

                            nextElementToEnd++;
                            curChannelIdx++;
                        }
                        if (curChannelIdx <= curBlockChannelStartingLoopLimit)
                        {
                            // max 1 elements to process
                            float val = blockVal[curSubBlockIdx++];
                            float curSqVal = val * val;

                            // 1 ending element max per iteration:
                            sumOfSq[nextElementToEnd] += curSqVal;

                            nextElementToEnd++;
                            curChannelIdx++;
                        }
                    } // ending sequence has started

                    curChanBlockNum++;
                    curChanBlockTexelIdx += channelStride;

                    // In this "B" block loop, A is tested by (curChanBlockNum <= supportEndBlockNum),
                    // and we can break fom that loop to potentially do C as soon as all sums are started
                    // and even if the ending sequence has started for some, the condition for C later
                    // will not trigger and there can only need one B block to finish ending so only need
                    // to test: if elementsStarted >= 4
                }



                // C:
                // See if we have full blocks to process:

                int supportEndingStartBlockNum = (supportEndChanForOut0 >> 2);
                // ...supportEndingStartBlockNum is the block in which the support is ending for at least element 0
                // since it will be the first to have its support end
                // (ie the block where the first element (#0 of 0 1 2 3) will have its support end in it.)

                int supportFullBlocksLimit = min((int)X_channelsDiv4 - 2, supportEndingStartBlockNum - 1);  // inclusive
                // We do X_channelsDiv4 - 2 for supportFullBlocksLimit instead of - 1 because the 4-chunked
                // channel axis can have partially valid content at the end irrespective of if the support of every elements
                // reach and fully cover the last (X_channelsDiv4 - 1) block. 
                // ie that last block could be partial not because the support of some elements partially cover it
                // but because some values are invalid there, the tensor ending somewhere in the block,
                // ie before not at block(X_channelsDiv4 - 1)[3].

                // Do any blocks which fully cover the support of the 4 final output elements,
                // that includes left blocks, center block and potential right full blocks if any:
                while (curChanBlockNum <= supportFullBlocksLimit)
                {
                    elementsStarted = 4;
                    // ...all elements should have started if we reach here

                    float4 blockVal = SampleBlockX(curChanBlockTexelIdx);
                    float blockPartialSumOfSq = dot(blockVal * blockVal, float4(1,1,1,1));
                    [unroll]
                    for (uint i = 0; i < 4; i++)
                        sumOfSq[i] += blockPartialSumOfSq;
                    curChanBlockNum++;
                    curChanBlockTexelIdx += channelStride;
                }

                //
                // Potential last partial block: almost same body as while ((curChanBlockNum <= supportEndBlockNum) && (elementsStarted < 4)) above
                // but can have all elementsStarted
                if (curChanBlockNum <= supportEndBlockNum)
                //while (curChanBlockNum <= supportEndBlockNum)
                {
                    // load next block value
                    float4 blockVal = SampleBlockX(curChanBlockTexelIdx);

                    uint curBlockChannelForSubElem0 = curChanBlockNum << 2;

                    // the block could be the only one left, so we could have not only 4 elements total
                    uint curBlockChannelForLastBlockElem = min(X_channels - 1, curBlockChannelForSubElem0 + 3);

                    uint curChannelIdx = curBlockChannelForSubElem0;
                    uint curSubBlockIdx = 0;

                    // The sum of square sequence has already started in this loop.
                    // We could have elements still to start (see (elementsStarted < 4) above)
                    // and at most, 3 sums to continue, at least one to end:
                    // Inclusive limit for the rest of the startup of the sum of square sequence:
                    // we need to stop when we reach either the end of the 4-component block or the end of the right support
                    // for overall output number 0: 
                    //
                    // NOTE: if we actually already ended the out0 sequence, we skip the first unrolled loop:
                    uint curBlockChannelStartingLoopLimit = min(curBlockChannelForLastBlockElem, supportEndChanForOut0);

                    // manually unrolled loop to simplify as we only have max 4 iterations
                    // compiler will remove the ++ etc:
                    if (curChannelIdx <= curBlockChannelStartingLoopLimit)
                    {
                        float val = blockVal[curSubBlockIdx++];
                        float curSqVal = val * val;

                        // need to continue at least one, max 4
                        sumOfSq[0] += curSqVal;

                        if (1 < elementsStarted)
                            sumOfSq[1] += curSqVal;
                        if (2 < elementsStarted)
                            sumOfSq[2] += curSqVal;
                        if (3 < elementsStarted)
                            sumOfSq[3] += curSqVal;

                        if (elementsStarted < 4)
                        {
                            sumOfSq[elementsStarted] = curSqVal;
                            elementsStarted++;
                        }
                        curChannelIdx++;
                    }
                    if (curChannelIdx <= curBlockChannelStartingLoopLimit)
                    {
                        float val = blockVal[curSubBlockIdx++];
                        float curSqVal = val * val;

                        // ...need to continue at least 2, max 4
                        sumOfSq[0] += curSqVal;
                        sumOfSq[1] += curSqVal;

                        if (2 < elementsStarted)
                            sumOfSq[2] += curSqVal;
                        if (3 < elementsStarted)
                            sumOfSq[3] += curSqVal;

                        if (elementsStarted < 4)
                        {
                            sumOfSq[elementsStarted] = curSqVal;
                            elementsStarted++;
                        }
                        curChannelIdx++;
                    }
                    if (curChannelIdx <= curBlockChannelStartingLoopLimit)
                    {
                        float val = blockVal[curSubBlockIdx++];
                        float curSqVal = val * val;

                        // ...need to continue at least 3, max 4
                        sumOfSq[0] += curSqVal;
                        sumOfSq[1] += curSqVal;
                        sumOfSq[2] += curSqVal;

                        if (3 < elementsStarted)
                            sumOfSq[3] += curSqVal;

                        if (elementsStarted < 4)
                        {
                            sumOfSq[elementsStarted] = curSqVal;
                            elementsStarted++;
                        }
                        curChannelIdx++;
                    }
                    if (curChannelIdx <= curBlockChannelStartingLoopLimit)
                    {
                        float val = blockVal[curSubBlockIdx++];
                        float curSqVal = val * val;

                        sumOfSq[0] += curSqVal;
                        sumOfSq[1] += curSqVal;
                        sumOfSq[2] += curSqVal;
                        sumOfSq[3] += curSqVal;
                        // ...need to continue all 4 if we reach here
                        // and none can start.
                        curChannelIdx++;
                    }

                    // Even if we dont enter the ending sequence below, 
                    // supportEndChanForOut0 could be == curBlockChannelForLastBlockElem
                    // take care of that here: ie we could already have ended out0
                    // otherwise we don't touch nextElementToEnd, it is already correctly set!
                    // 
                    // Note we can't just test if (supportEndChanForOut0 == curBlockChannelStartingLoopLimit),
                    // this is because eg for 5 channel for 2nd block, limit == min(4 (the last channel in 2nd block), 1 (supportEndChanForOut0))
                    // so curBlockChannelStartingLoopLimit means we already processed ending of out0, but we may have done more!
                    if ((supportEndChanForOut0 + 1) == curChannelIdx)
                        nextElementToEnd = 1;

                    // If curSubBlockIdx < 4, it's because we reached supportEndChanForOut0,
                    // in that case we need to start the ending sequence:
                    // At each iteration, one sum must end, but also another potentially starts:
                    if (curSubBlockIdx < 4)
                    {
                        // limit: the (right) support end of the last channel to process
                        // or the full channel limit (X_channels - 1),
                        curBlockChannelStartingLoopLimit = min(supportEndChanForLastOut, curBlockChannelForLastBlockElem);
                        //
                        // Also, since we handle at a max for 4 elements in parallel, we have
                        // from 1 to 4 left to end (1 that ends for sure), from 0 to 3 that continue,
                        // and from 0 to 3 that start (can be 0 because of the unrolled loop above):

                        // manually unrolled loop to simplify as we only have max 3 iterations
                        // compiler will remove the ++ etc:
                        if (curChannelIdx <= curBlockChannelStartingLoopLimit)
                        {
                            // max 4 elements to process
                            float val = blockVal[curSubBlockIdx++];
                            float curSqVal = val * val;

                            // 1 ending element max per iteration:
                            sumOfSq[nextElementToEnd] += curSqVal;

                            // max 3 elements to continue (up to those which start)
                            uint toContinueIdx = nextElementToEnd + 1;

                            if (toContinueIdx < elementsStarted)
                                sumOfSq[toContinueIdx++] += curSqVal;
                            if (toContinueIdx < elementsStarted)
                                sumOfSq[toContinueIdx++] += curSqVal;
                            if (toContinueIdx < elementsStarted)
                                sumOfSq[toContinueIdx++] += curSqVal;

                            // max 3 that start
                            if (elementsStarted < 4)
                                sumOfSq[elementsStarted++] += curSqVal;
                            if (elementsStarted < 4)
                                sumOfSq[elementsStarted++] += curSqVal;
                            if (elementsStarted < 4)
                                sumOfSq[elementsStarted++] += curSqVal;

                            nextElementToEnd++;
                            curChannelIdx++;
                        }
                        if (curChannelIdx <= curBlockChannelStartingLoopLimit)
                        {
                            // max 2 elements to process
                            float val = blockVal[curSubBlockIdx++];
                            float curSqVal = val * val;

                            // 1 ending element max per iteration:
                            sumOfSq[nextElementToEnd] += curSqVal;

                            // max 2 elements to continue (up to those which start)
                            uint toContinueIdx = nextElementToEnd + 1;

                            if (toContinueIdx < elementsStarted)
                                sumOfSq[toContinueIdx++] += curSqVal;
                            if (toContinueIdx < elementsStarted)
                                sumOfSq[toContinueIdx++] += curSqVal;

                            // max 2 that start
                            if (elementsStarted < 4)
                                sumOfSq[elementsStarted++] += curSqVal;
                            if (elementsStarted < 4)
                                sumOfSq[elementsStarted++] += curSqVal;

                            nextElementToEnd++;
                            curChannelIdx++;
                        }
                        if (curChannelIdx <= curBlockChannelStartingLoopLimit)
                        {
                            // max 1 elements to process
                            float val = blockVal[curSubBlockIdx++];
                            float curSqVal = val * val;

                            // 1 ending element max per iteration:
                            sumOfSq[nextElementToEnd] += curSqVal;

                            nextElementToEnd++;
                            curChannelIdx++;
                        }
                    } // ending sequence has started

                    curChanBlockNum++;
                    curChanBlockTexelIdx += channelStride;
                }

                // One possible last block: not possible to have elementsStarted < 4 here
                // and out0 must have ended, ie nextElementToEnd >= 1
                if (curChanBlockNum <= supportEndBlockNum)
                {
                    // load next block value
                    float4 blockVal = SampleBlockX(curChanBlockTexelIdx);

                    uint curBlockChannelForSubElem0 = curChanBlockNum << 2;

                    // the block could be the only one left, so we could have not only 4 elements total
                    uint curBlockChannelForLastBlockElem = min(X_channels - 1, curBlockChannelForSubElem0 + 3);

                    uint curChannelIdx = curBlockChannelForSubElem0;
                    uint curSubBlockIdx = 0;

                    // NOTE: if already ended the out0 sequence, we skip the first unrolled loop:
                    uint curBlockChannelStartingLoopLimit = min(curBlockChannelForLastBlockElem, supportEndChanForOut0);

                    //if ((supportEndChanForOut0 + 1) == curChannelIdx)
                    //    nextElementToEnd = 1;
                    // ...but nextElementToEnd has to be >= 1 here already:

                    // At each iteration, one sum must end:
                    elementsStarted = 4; // help compiler
                    {
                        // limit: the (right) support end of the last channel to process
                        // or the full channel limit (X_channels - 1),
                        curBlockChannelStartingLoopLimit = min(supportEndChanForLastOut, curBlockChannelForLastBlockElem);
                        //
                        // manually unrolled loop to simplify as we only have max 3 iterations
                        // compiler will remove the ++ etc:
                        if (curChannelIdx <= curBlockChannelStartingLoopLimit)
                        {
                            // max 4 elements to process
                            float val = blockVal[curSubBlockIdx++];
                            float curSqVal = val * val;

                            // 1 ending element max per iteration:
                            sumOfSq[nextElementToEnd] += curSqVal;

                            // max 3 elements to continue (up to those which started or here 4)
                            uint toContinueIdx = nextElementToEnd + 1;

                            if (toContinueIdx < elementsStarted)
                                sumOfSq[toContinueIdx++] += curSqVal;
                            if (toContinueIdx < elementsStarted)
                                sumOfSq[toContinueIdx++] += curSqVal;
                            if (toContinueIdx < elementsStarted)
                                sumOfSq[toContinueIdx++] += curSqVal;

                            nextElementToEnd++;
                            curChannelIdx++;
                        }
                        if (curChannelIdx <= curBlockChannelStartingLoopLimit)
                        {
                            // max 2 elements to process
                            float val = blockVal[curSubBlockIdx++];
                            float curSqVal = val * val;

                            // 1 ending element max per iteration:
                            sumOfSq[nextElementToEnd] += curSqVal;

                            // max 2 elements to continue (up to those which start)
                            uint toContinueIdx = nextElementToEnd + 1;

                            if (toContinueIdx < elementsStarted)
                                sumOfSq[toContinueIdx++] += curSqVal;
                            if (toContinueIdx < elementsStarted)
                                sumOfSq[toContinueIdx++] += curSqVal;

                            nextElementToEnd++;
                            curChannelIdx++;
                        }
                        if (curChannelIdx <= curBlockChannelStartingLoopLimit)
                        {
                            // max 1 elements to process
                            float val = blockVal[curSubBlockIdx++];
                            float curSqVal = val * val;

                            // 1 ending element max per iteration:
                            sumOfSq[nextElementToEnd] += curSqVal;

                            nextElementToEnd++;
                            curChannelIdx++;
                        }
                    } // ending sequence has started

                    curChanBlockNum++;
                    curChanBlockTexelIdx += channelStride;
                }


                // We're finally done, output the final values:
                float4 sumOfSqVec = float4(sumOfSq[0], sumOfSq[1], sumOfSq[2], sumOfSq[3]);

                //res = inputBlockVal / pow(abs(Bias + sumOfSqVec * AlphaDivSupportLength), Beta);
                res = inputBlockVal / SignedPow(abs(Bias + sumOfSqVec * AlphaDivSupportLength), Beta);

                return res;
            }
            ENDCG
        }
    }
}
