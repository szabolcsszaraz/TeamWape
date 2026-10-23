using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Unity.InferenceEngine
{
    partial class CPUBackend
    {
        [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        unsafe struct LayerNormalizationTailJob : IJobParallelFor, IJobResourceDeclarationXSBWO
        {
            public float epsilon;
            public int axisDim;
            public int outerLength;
            public ReadOnlyMemResource X { get; set; } float* Xptr => (float*)X.ptr;
            public ReadOnlyMemResource S { get; set; } float* Sptr => (float*)S.ptr;
            public ReadOnlyMemResource B { get; set; } float* Bptr => (float*)B.ptr;
            public ReadOnlyMemResource W { get; set; } float* Wptr => (float*)W.ptr;
            public ReadWriteMemResource O { get; set; } float* Optr => (float*)O.ptr;

            const int k_InnerLoopLength = 32;

            [SkipLocalsInit]
            public void Execute(int outerIndex)
            {
                var Xp = Xptr + outerIndex * axisDim;
                var Sp = Sptr;
                var Bp = Bptr;
                var Op = Optr + outerIndex * axisDim;

                float mean = Wptr[outerIndex * 2 + 0];
                float variance = Wptr[outerIndex * 2 + 1];

                var it = stackalloc float[k_InnerLoopLength];

                for (var start = 0; start < axisDim; start += k_InnerLoopLength)
                {
                    var count = math.min(k_InnerLoopLength, axisDim - start);
                    int i;

                    for (i = 0; i < count; i++)
                    {
                        float scale = Sp[i];
                        float bias = Bp[i];
                        float v = Xp[i];

                        v = (v - mean) / math.sqrt(variance + epsilon);
                        v = scale * v + bias;

                        it[i] = v;
                    }

                    UnsafeUtility.MemCpy(Op, it, sizeof(float) * count);

                    Xp += count;
                    Sp += count;
                    Bp += count;
                    Op += count;
                }
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        unsafe struct RMSNormalizationTailJob : IJobParallelFor, IJobResourceDeclarationXSBO
        {
            public float epsilon;
            public int axisDim;
            public int outerLength;
            public ReadOnlyMemResource X { get; set; } float* Xptr => (float*)X.ptr;
            public ReadOnlyMemResource S { get; set; } float* Sptr => (float*)S.ptr;
            public ReadOnlyMemResource B { get; set; } float* Bptr => (float*)B.ptr;
            public ReadWriteMemResource O { get; set; } float* Optr => (float*)O.ptr;

            const int k_InnerLoopLength = 32;

            [SkipLocalsInit]
            public void Execute(int outerIndex)
            {
                var Xp = Xptr + outerIndex * axisDim;
                var Sp = Sptr;
                var Op = Optr + outerIndex * axisDim;

                float variance = Bptr[outerIndex];

                var it = stackalloc float[k_InnerLoopLength];

                for (var start = 0; start < axisDim; start += k_InnerLoopLength)
                {
                    var count = math.min(k_InnerLoopLength, axisDim - start);
                    int i;

                    for (i = 0; i < count; i++)
                    {
                        float scale = Sp[i];
                        float v = Xp[i];

                        v = (v) / math.sqrt(variance + epsilon);
                        v = scale * v;

                        it[i] = v;
                    }

                    UnsafeUtility.MemCpy(Op, it, sizeof(float) * count);

                    Xp += count;
                    Sp += count;
                    Op += count;
                }
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        unsafe struct BatchNormalizationJob : IParallelForBatch
        {
            public float epsilon;
            public int channels;
            public int spatialLength;

            [NoAlias] [NativeDisableUnsafePtrRestriction] [ReadOnly] public float* Xptr;
            [NoAlias] [NativeDisableUnsafePtrRestriction] [ReadOnly] public float* Sptr;
            [NoAlias] [NativeDisableUnsafePtrRestriction] [ReadOnly] public float* Bptr;
            [NoAlias] [NativeDisableUnsafePtrRestriction] [ReadOnly] public float* Mptr;
            [NoAlias] [NativeDisableUnsafePtrRestriction] [ReadOnly] public float* Vptr;

            [NoAlias] [NativeDisableUnsafePtrRestriction] public float* Optr;

            public void Execute(int i, int count)
            {
                float* Op = Optr + i;
                float* Xp = Xptr + i;

                // Extract the starting output position from the index.
                int os = i % spatialLength;
                i = i / spatialLength;
                int oc = i % channels;
                i = i / channels;

                float* Sp = Sptr + oc;
                float* Bp = Bptr + oc;
                float* Mp = Mptr + oc;
                float* Vp = Vptr + oc;

                float scale = Sp[0];
                float bias = Bp[0];
                float mean = Mp[0];
                float variance = Vp[0];

                // Advance to the starting input channel.
                int spatialLengthRemaining = spatialLength - os;

                while (count > 0)
                {
                    int spatialCountW = math.min(count, spatialLengthRemaining);
                    count -= spatialCountW;

                    for (; spatialCountW > 0; spatialCountW -= 1)
                    {
                        float v = Xp[0];
                        v = (v - mean) / math.sqrt(variance + epsilon);
                        v = scale * v + bias;

                        Xp++;
                        *Op++ = v;
                        os++;
                    }

                    if (count > 0)
                    {
                        // Output is now always aligned to the start of a row.
                        os = 0;
                        spatialLengthRemaining = spatialLength;

                        oc++;
                        Sp++;
                        Bp++;
                        Mp++;
                        Vp++;

                        if (oc == channels)
                        {
                            // Advance to the next output batch.
                            oc = 0;

                            Sp = Sptr;
                            Bp = Bptr;
                            Mp = Mptr;
                            Vp = Vptr;
                        }

                        scale = Sp[0];
                        bias = Bp[0];
                        mean = Mp[0];
                        variance = Vp[0];
                    }
                }
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        unsafe struct LocalResponseNormalizationJob : IParallelForBatch
        {
            public int numChannels;
            public int channelsStride;
            public int batchStride;
            public int leftSupportLength;
            public int rightSupportLength;

            public float bias;
            public float beta;
            public float alphaDivSupportLength;


            [NoAlias] [NativeDisableUnsafePtrRestriction] [ReadOnly] public float* Xptr;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public float* Optr;

            public void Execute(int i, int count)
            {
                int countLeftOfFullJobCount = count;

                // Instead of considering the work item indices to match the N x C x D tensor and do
                //
                //      int spatialNum = i % channelsStride;
                //      int xbatchChannelNum = i / channelsStride;
                //      int firstChannelNumOfBatch = xbatchChannelNum % numChannels;
                //
                //      float* Op = Optr + i;
                //      float* Xp = Xptr + i;
                //
                // we will allocate them to work on N x D x C: we do this because the method below
                // overlaps the channel usage as much as possible.
                // This saves memory access and arithmetic intensity as the scan for the sum of squares
                // are effectively 2 additions and mem access per work item instead of being supportSize per work item.
                // In some cases it might be better still to do "for work items distributed spatially" if
                // the sum of squares of the support can be SIMD'ed by burst, as then the support chunks can be
                // loaded in SIMD regs and reduced in-reg there, but that depends on support size too.

                // We consider workitem indices i distributed as N x D x C:
                int firstChannelNumOfNxDSegment = i % numChannels;
                int xbatchSpatialNum = i / numChannels;

                int spatialNum = xbatchSpatialNum % channelsStride; // channelsStride == total spatial range for all spatial dimensions
                int batchNum = xbatchSpatialNum / channelsStride;

                // Tensors are actually N x C x D:
                int ptrStartOffset = batchNum * batchStride + firstChannelNumOfNxDSegment * channelsStride + spatialNum;
                float* Op = Optr + ptrStartOffset;
                float* Xp = Xptr + ptrStartOffset;

                // these are the merged support extents for the first channel we process
                int supportUnclampedStart0 = firstChannelNumOfNxDSegment - leftSupportLength;
                int supportUnclampedEnd0 = firstChannelNumOfNxDSegment + rightSupportLength;

                // these are the clamped merged support extents for the first channel we process
                int runningSumStart0 = math.max(0, supportUnclampedStart0);
                int firstRunningSumEnd0 = math.min(numChannels - 1, supportUnclampedEnd0);

                bool haveMore = countLeftOfFullJobCount > 0;
                while (haveMore)
                {
                    float curSumOfSquares = 0.0f;

                    int curSegmentChannelsTodo = math.min(countLeftOfFullJobCount, numChannels - firstChannelNumOfNxDSegment);
                    int onePastLastChannelNumOfSegment = firstChannelNumOfNxDSegment + curSegmentChannelsTodo;

                    int curReadTail;
                    int curReadHead;

                    // (A)
                    // First do the running sum for the first channel of the job:
                    curReadHead = runningSumStart0;
                    float* readHeadXptr = Xp + (runningSumStart0 - firstChannelNumOfNxDSegment) * channelsStride; // rewind the main input pointer, (runningSumStart0 - firstChannelNumOfNxDSegment) <= 0
                    float* readTailXptr = readHeadXptr;
                    while (curReadHead <= firstRunningSumEnd0)
                    {
                        float val = *readHeadXptr;
                        curReadHead++;
                        readHeadXptr += channelsStride; // compiler should optimize for either readHeadXptr or curReadHead arithmetic, this is just for clarity
                        curSumOfSquares += val * val;
                    }
                    // output for the first running sum and channel element we just we finished:
                    float res = *Xp / math.pow((bias + curSumOfSquares * alphaDivSupportLength), beta);
                    *Op = res;

                    Xp += channelsStride;
                    Op += channelsStride;

                    // (B)
                    // curReadHead is now == (supportUnclampedEnd0 + 1) or == (numChannels - 1 + 1) == numChannels
                    //
                    // Handle the special case where we only need to add to the running sum to output for the
                    // next channels after the firstChannelNumOfNxDSegment:
                    // this is the case if curReadTail == supportUnclampedStart0 < runningSumStart0
                    curReadTail = supportUnclampedStart0;
                    // note we leave readTailXptr at the right place for/if the next while() in (C) runs,
                    // ie at the pos for channel number runningSumStart0,

                    int nextOutChannelNum = firstChannelNumOfNxDSegment + 1;
                    int stepsTodo = math.min(runningSumStart0 - supportUnclampedStart0, numChannels - curReadHead);
                    // ie same as while (curReadTail < runningSumStart0 && curReadHead < numChannels)
                    // with curReadTail++, curReadHead++:
                    int limitExcl = (nextOutChannelNum + stepsTodo);
                    while (nextOutChannelNum < limitExcl)
                    {
                        float val = *readHeadXptr;
                        curReadHead++;
                        readHeadXptr += channelsStride;
                        curSumOfSquares += val * val;

                        res = *Xp / math.pow((bias + curSumOfSquares * alphaDivSupportLength), beta);
                        *Op = res;

                        Xp += channelsStride;
                        Op += channelsStride;

                        nextOutChannelNum++;
                    }
                    // Advance curReadTail to match where the previous loop ended,
                    // this is important if the next loop has no steps:
                    curReadTail += stepsTodo;

                    // (C)
                    // Now curReadHead = supportUnclampedEnd0 + 1 + (runningSumStart0 - supportUnclampedStart0)
                    // also since supportUnclampedEnd0 and supportUnclampedStart0 are both unclamped and inclusive,
                    // supportUnclampedEnd0 - supportUnclampedStart0 + 1 = supportSize
                    // so we have also = supportSize + 1;
                    // OR curReadHead is limited on the right and = numChannels;

                    // Main generic loop that adjust the running sum of squares without scanning the full support:
                    // Just subtract tail val, add head val.
                    // curReadTail now == runningSumStart0 only if the next loop runs
                    // (see in the min() above in (B) and below in (C), (numChannels - curReadHead))
                    // and check also above, readTailXptr already matching for channel runningSumStart0.


                    int curSegmentChannelsLeftTodo = (onePastLastChannelNumOfSegment - nextOutChannelNum);

                    stepsTodo = math.min(numChannels - curReadHead, curSegmentChannelsLeftTodo);
                    limitExcl = (nextOutChannelNum + stepsTodo);

                    while (nextOutChannelNum < limitExcl)
                    {
                        float valToSub = *readTailXptr;
                        float valToAdd = *readHeadXptr;
                        curReadTail++;
                        curReadHead++;
                        readTailXptr += channelsStride;
                        readHeadXptr += channelsStride;

                        curSumOfSquares += (valToAdd * valToAdd) - (valToSub * valToSub);

                        res = *Xp / math.pow((bias + curSumOfSquares * alphaDivSupportLength), beta);
                        *Op = res;

                        Xp += channelsStride;
                        Op += channelsStride;

                        nextOutChannelNum++;
                    }

                    // (D)
                    // Left over outputs that can't add anything to the running sum as we reached the end of the channel axis,
                    // but if we never entered the main loop (C), we can't subtract anything either,
                    // otherwise, keep subtracting:
                    curSegmentChannelsLeftTodo = (onePastLastChannelNumOfSegment - nextOutChannelNum);

                    int stepsToHaveReadTailValid = runningSumStart0 - curReadTail;
                    // ...could be negative, in that case, we don't add more and readTailXptr is valid and pointing
                    // to the next value to subtract.

                    stepsTodo = math.min(curSegmentChannelsLeftTodo, stepsToHaveReadTailValid);
                    for (int j = 0; j < stepsTodo; j++)
                    {
                        res = *Xp / math.pow((bias + curSumOfSquares * alphaDivSupportLength), beta);
                        *Op = res;

                        Xp += channelsStride;
                        Op += channelsStride;

                        nextOutChannelNum++;
                    }

                    //if (stepsTodo == stepsToHaveReadTailValid)
                    //    curReadTail += stepsTodo;

                    // readTailXptr should finally be valid, and we can enter the last loop if need be:
                    // that is, sequentially subtract from the running sum that is no longer added to
                    // until we reach the end of this channel segment:
                    while (nextOutChannelNum < onePastLastChannelNumOfSegment)
                    {
                        float valToSub = *readTailXptr;
                        //curReadTail++;
                        readTailXptr += channelsStride;
                        curSumOfSquares -= (valToSub * valToSub);

                        res = *Xp / math.pow((bias + curSumOfSquares * alphaDivSupportLength), beta);
                        *Op = res;

                        Xp += channelsStride;
                        Op += channelsStride;

                        nextOutChannelNum++;
                    }

                    // (E)
                    // More spatial and batch indices to process ?
                    countLeftOfFullJobCount -= curSegmentChannelsTodo;
                    haveMore = countLeftOfFullJobCount > 0;

                    if (haveMore)
                    {
                        // Reset pointers
                        spatialNum++;
                        if (spatialNum >= channelsStride) // end of all spatial space ? switch batch
                        {
                            spatialNum = 0;
                            batchNum++;
                            ptrStartOffset = batchNum * batchStride; // + 0 * channelsStride + 0;
                            Op = Optr + ptrStartOffset;
                            Xp = Xptr + ptrStartOffset;
                        }
                        else
                        {
                            // Op and Xp are already aligned to a channel axis start, except in potentially past the tensor or the next batch,
                            // so just rewind the channel axis and then we just need to advance the spatial element,
                            // and since the spatial dimensions are the innermost of the tensor, the stride of those combined dimensions
                            // is obviously just 1:
                            Op = Op - batchStride + 1; // batchStride = numChannels * channelStride;
                            Xp = Xp - batchStride + 1; // batchStride = numChannels * channelStride;
                        }

                        // Reset some values for the next batch:
                        // Note that Xp and Op should already point to channel 0 of the next spatial element or batch 
                        firstChannelNumOfNxDSegment = 0;
                        supportUnclampedStart0 = -leftSupportLength; // firstChannelNumOfNxDSegment - leftSupportLength;
                        supportUnclampedEnd0 = rightSupportLength; // firstChannelNumOfNxDSegment + rightSupportLength;
                        runningSumStart0 = 0; // math.max(0, supportUnclampedStart0);
                        firstRunningSumEnd0 = math.min(numChannels - 1, supportUnclampedEnd0);
                    }
                } // while
            } // Exec
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        unsafe struct ScaleBiasJob : IParallelForBatch, IJobResourceDeclarationXSBO
        {
            public int channels;
            public int spatialLength;

            public ReadOnlyMemResource X { get; set; } float* Xptr => (float*)X.ptr;
            public ReadOnlyMemResource S { get; set; } float* Sptr => (float*)S.ptr;
            public ReadOnlyMemResource B { get; set; } float* Bptr => (float*)B.ptr;
            public ReadWriteMemResource O { get; set; } float* Optr => (float*)O.ptr;

            public void Execute(int i, int count)
            {
                float* Op = Optr + i;
                float* Xp = Xptr + i;

                // Extract the starting output position from the index.
                int os = i % spatialLength;
                i = i / spatialLength;
                int oc = i % channels;
                i = i / channels;

                float* Sp = Sptr + oc;
                float* Bp = Bptr + oc;

                float scale = Sp[0];
                float bias = Bp[0];

                // Advance to the starting input channel.
                int spatialLengthRemaining = spatialLength - os;

                while (count > 0)
                {
                    int spatialCountW = math.min(count, spatialLengthRemaining);
                    count -= spatialCountW;

                    for (; spatialCountW > 0; spatialCountW -= 1)
                    {
                        float v = Xp[0];
                        v = scale * v + bias;

                        Xp++;
                        *Op++ = v;
                        os++;
                    }

                    if (count > 0)
                    {
                        // Output is now always aligned to the start of a row.
                        os = 0;
                        spatialLengthRemaining = spatialLength;

                        oc++;
                        Sp++;
                        Bp++;

                        if (oc == channels)
                        {
                            // Advance to the next output batch.
                            oc = 0;

                            Sp = Sptr;
                            Bp = Bptr;
                        }

                        scale = Sp[0];
                        bias = Bp[0];
                    }
                }
            }
        }
    }
}
