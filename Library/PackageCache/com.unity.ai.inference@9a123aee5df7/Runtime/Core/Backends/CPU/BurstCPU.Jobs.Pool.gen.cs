// This is auto-generated -- do not modify directly
using UnityEngine;
using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Burst.Intrinsics;
using static Unity.Burst.Intrinsics.X86.Avx;
using static Unity.Burst.Intrinsics.X86.Avx2;
using static Unity.Burst.Intrinsics.X86.Fma;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace Unity.InferenceEngine {
partial class CPUBackend
{
    [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
    internal unsafe struct GlobalAverageVariancePoolJob : IJobParallelFor, IJobResourceDeclarationXO
    {
        public ReadOnlyMemResource X { get; set; } float* Xptr => (float*)X.ptr;
        public ReadWriteMemResource O { get; set; } float* Optr => (float*)O.ptr;
        public int spatialDims;

        public void Execute(int i)
        {
            float mean = 0.0f;
            float mean2 = 0.0f;
            for (int d = 0; d < spatialDims; ++d)
            {
                float v = Xptr[i * spatialDims + d];
                mean  += v;
                mean2 += v*v;
            }
            mean  /= (float)(spatialDims);
            mean2 /= (float)(spatialDims);

            Optr[2*i+0] = mean;
            Optr[2*i+1] = (mean2 - mean * mean);
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal unsafe struct MaxPool2DJob : IParallelForBatch, IJobResourceDeclarationXO
    {
        public ReadOnlyMemResource X { get; set; } float* Xptr => (float*)X.ptr;
        public ReadWriteMemResource O { get; set; } float* Optr => (float*)O.ptr;
        public int inputHeight;
        public int inputWidth;
        public int outputHeight;
        public int outputWidth;
        public int poolHeight;
        public int poolWidth;
        public int strideHeight;
        public int strideWidth;
        public int padHeight;
        public int padWidth;

        public void Execute(int i, int count)
        {
            float* Op = Optr + i;

            // Extract the starting output position from the index.
            int ow = i % outputWidth;
            i = i / outputWidth;
            int oh = i % outputHeight;
            i = i / outputHeight;

            // Advance to the starting input channel.
            int inputSize = inputWidth * inputHeight;
            float* Xp = Xptr + i * inputSize;

            // Compute the base input indices from the starting output position.
            int iwBase = ow * strideWidth - padWidth;
            int ihBase = oh * strideHeight - padHeight;

            // Compute the first element index that is not eligible to use the vectorized path due to
            // the use of right padding. This index also serves as a flag to exclude unhandled stride
            // widths.
            //
            // For example, consider inputWidth=20 and poolWidth=3. The number of vector elements is 4
            // from float4, so an input row looks like the below:
            //
            //          01234567890123456789        input row, strideWidth=1
            //                        vvvv          iteration pw=0
            //                         vvvv         iteration pw=1
            //                          vvvv        iteration pw=2
            //
            //          01234567890123456789        input row, strideWidth=2
            //                     v v v v          iteration pw=0
            //                      v v v v         iteration pw=1
            //                       v v v v        iteration pw=2
            //
            // The vectorization path requires all of the input to be contained in the input tensor, so
            // the last element index of the last iteration of poolWidth is 19. The first element of the
            // first iteration of poolWidth can then be computed from the strideWidth and poolWidth as
            // shown below (vectorElementsSpanSize). Any starting width index after this position would not
            // provide enough valid data to be used in the pooling operation. This index is biased up by
            // one to allow the value to be used with the unsigned compare trick below.
            int iwBaseVectorLimit = 0;
            if (strideWidth == 1 || strideWidth == 2)
            {
                int vectorElementsSpanSize = ((4 - 1) * strideWidth + 1) + (poolWidth - 1);
                if (vectorElementsSpanSize <= inputWidth)
                    iwBaseVectorLimit = inputWidth - vectorElementsSpanSize + 1;
            }

            while (count > 0)
            {
                int outputCountW = math.min(count, outputWidth - ow);
                count -= outputCountW;

                // Clamp the effective range of the pooling to the input height.
                int ihClamped = math.max(0, ihBase);
                int ihClampedEnd = math.min(ihBase + poolHeight, inputHeight);

                float* initialXph = &Xp[ihClamped * inputWidth];
                int ihCount = ihClampedEnd - ihClamped;

                if (ihCount > 0)
                {
                    do
                    {
                        // Clamp the effective range of the pooling to the input width.
                        int iwClamped = math.max(0, iwBase);
                        int iwClampedEnd = math.min(iwBase + poolWidth, inputWidth);

                        float* Xph = initialXph;

                        // Test if an entire vector can be produced and that all lanes of the vector are using data
                        // from inside the input tensor. If the stride width is unsupported, then the vector limit
                        // is zero and this test will always be false.
                        if (outputCountW >= 4 && (uint)iwBase < (uint)iwBaseVectorLimit)
                        {
                            float4 accVal = new float4(float.MinValue);

                            if (strideWidth == 1)
                            {
                                iwBase += 4 * 1;

                                for (int ih = 0; ih < ihCount; ih++)
                                {
                                    for (int iw = iwClamped; iw < iwClampedEnd; iw++)
                                    {
                                        float4 v = ((float4*)&Xph[iw])[0];
                                        accVal = math.max(accVal, v);
                                    }

                                    Xph += inputWidth;
                                }
                            }
                            else
                            {
                                iwBase += 4 * 2;

                                for (int ih = 0; ih < ihCount; ih++)
                                {
                                    for (int iw = iwClamped; iw < iwClampedEnd; iw++)
                                    {
                                        float4 v = VectorUtils.PackStride2(((float4*)&Xph[iw])[0], ((float4*)&Xph[iw])[1]);
                                        accVal = math.max(accVal, v);
                                    }

                                    Xph += inputWidth;
                                }
                            }

                            *((float4*)Op) = accVal;
                            Op += 4;
                            outputCountW -= 4;
                        }
                        else
                        {
                            float accVal = float.MinValue;

                            for (int ih = 0; ih < ihCount; ih++)
                            {
                                for (int iw = iwClamped; iw < iwClampedEnd; iw++)
                                {
                                    float v = Xph[iw];
                                    accVal = math.max(accVal, v);
                                }

                                Xph += inputWidth;
                            }

                            *Op++ = accVal;
                            iwBase += strideWidth;
                            outputCountW -= 1;
                        }
                    }
                    while (outputCountW != 0);
                }
                else
                {
                    // The output row is entirely in the padding region.
                    for (float* lastOp = Op + outputCountW; Op < lastOp;)
                        *Op++ = (float)float.MinValue;
                }

                if (count > 0)
                {
                    // Output is now always aligned to the start of a row.
                    ow = 0;
                    iwBase = -padWidth;
                    ihBase += strideHeight;

                    if (++oh == outputHeight)
                    {
                        // Advance to the next input channel.
                        oh = 0;
                        ihBase = -padHeight;
                        Xp += inputSize;
                    }
                }
            }
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal unsafe struct MaxPool3DJob : IParallelForBatch, IJobResourceDeclarationXO
    {
        public ReadOnlyMemResource X { get; set; }
        float* Xptr => (float*)X.ptr;
        public ReadWriteMemResource O { get; set; }
        float* Optr => (float*)O.ptr;
        public int inputDepth;
        public int inputHeight;
        public int inputWidth;
        public int outputDepth;
        public int outputHeight;
        public int outputWidth;
        public int poolDepth;
        public int poolHeight;
        public int poolWidth;
        public int strideDepth;
        public int strideHeight;
        public int strideWidth;
        public int padDepth;
        public int padHeight;
        public int padWidth;

        public void Execute(int i, int count)
        {
            float* Op = Optr + i;

            // Extract the starting output position from the index.
            int ow = i % outputWidth;
            i = i / outputWidth;
            int oh = i % outputHeight;
            i = i / outputHeight;
            int od = i % outputDepth;
            i = i / outputDepth;
            // i is now the channel num

            // Advance to the starting input channel.
            int inputDepthSliceSize = inputWidth * inputHeight;
            int inputSize = inputDepthSliceSize * inputDepth;
            float* Xp = Xptr + i * inputSize;

            // Compute the base input indices from the starting output position.
            int iwBase = ow * strideWidth - padWidth;
            int ihBase = oh * strideHeight - padHeight;
            int idBase = od * strideDepth - padDepth;

            int iwBaseVectorLimitExclusive = 0;
            if (strideWidth == 1 || strideWidth == 2) // only paths supported for vectorization for now
            {
                int vectorElementsSpanSize = ((4 - 1) * strideWidth + 1) + (poolWidth - 1);
                if (vectorElementsSpanSize <= inputWidth)
                    iwBaseVectorLimitExclusive = inputWidth - vectorElementsSpanSize + 1;
                // ...+1 for exclusive limit, that way we can use an unsigned compare to combine everything we want to test in one compare:
                // see below, iwBase can be 0, but iwBaseVectorLimitExclusive == 0 is reserved for unsupported vectorization path
            }

            while (count > 0)
            {
                // We process one row at a time:
                int outputCountW = math.min(count, outputWidth - ow);
                count -= outputCountW;

                // Clamp the effective range of the pooling to the input height and depth
                int ihClamped = math.max(0, ihBase);
                int ihClampedEnd = math.min(ihBase + poolHeight, inputHeight);

                int idClamped = math.max(0, idBase);
                int idClampedEnd = math.min(idBase + poolDepth, inputDepth);

                float* hdInitialXptr = &Xp[(idClamped * inputHeight + ihClamped) * inputWidth];
                int ihCount = ihClampedEnd - ihClamped;
                int idCount = idClampedEnd - idClamped;

                // To advance an input pointer below (hdStartXptr), we will calculate a "SkipToNextSliceSize":
                // ie when reaching the end of the input rows to process in the pooling op, the number of rows to skip
                // are the start rows to skip ( = ihClamped index itself) + left over rows,
                //      rows to skip = ihClamped + inputHeight - ihClampedEnd
                // but this is just
                //      rows to skip = inputHeight - ihCount
                int hdSkipToNextSliceSize = (inputHeight - ihCount) * inputWidth;

                if (idCount > 0 && ihCount > 0)
                {
                    do
                    {
                        // Clamp the effective range of the pooling to the input width.
                        int iwClamped = math.max(0, iwBase);
                        int iwClampedEnd = math.min(iwBase + poolWidth, inputWidth);

                        float* hdStartXptr = hdInitialXptr;

                        // Test if an entire vector can be produced and that all lanes of the vector are using data
                        // from inside the input tensor. If the stride width is unsupported, then the vector limit
                        // is zero and this test will always be false.
                        if (outputCountW >= 4 && (uint)iwBase < (uint)iwBaseVectorLimitExclusive)
                        {
                            float4 accVal = new float4(float.MinValue);

                            if (strideWidth == 1)
                            {
                                iwBase += 4 * 1;

                                for (int id = 0; id < idCount; id++)
                                {
                                    for (int ih = 0; ih < ihCount; ih++)
                                    {
                                        for (int iw = iwClamped; iw < iwClampedEnd; iw++)
                                        {
                                            float4 v = ((float4*)&hdStartXptr[iw])[0];
                                            accVal = math.max(accVal, v);
                                        }

                                        hdStartXptr += inputWidth;
                                    }

                                    hdStartXptr += hdSkipToNextSliceSize;
                                }
                            }
                            else
                            {
                                iwBase += 4 * 2;

                                for (int id = 0; id < idCount; id++)
                                {
                                    for (int ih = 0; ih < ihCount; ih++)
                                    {
                                        for (int iw = iwClamped; iw < iwClampedEnd; iw++)
                                        {
                                            float4 v = VectorUtils.PackStride2(((float4*)&hdStartXptr[iw])[0], ((float4*)&hdStartXptr[iw])[1]);
                                            accVal = math.max(accVal, v);
                                        }

                                        hdStartXptr += inputWidth;
                                    }

                                    hdStartXptr += hdSkipToNextSliceSize;
                                }
                            }

                            *((float4*)Op) = accVal;
                            Op += 4;
                            outputCountW -= 4;
                        }
                        else
                        {
                            float accVal = float.MinValue;

                            for (int id = 0; id < idCount; id++)
                            {
                                for (int ih = 0; ih < ihCount; ih++)
                                {
                                    for (int iw = iwClamped; iw < iwClampedEnd; iw++)
                                    {
                                        float v = hdStartXptr[iw];
                                        accVal = math.max(accVal, v);
                                    }

                                    hdStartXptr += inputWidth;
                                }

                                hdStartXptr += hdSkipToNextSliceSize;
                            }

                            *Op++ = accVal;
                            iwBase += strideWidth;
                            outputCountW -= 1;
                        }
                    }
                    while (outputCountW != 0);
                }
                else
                {
                    // The output row is entirely in the padding region.
                    for (float* lastOp = Op + outputCountW; Op < lastOp;)
                        *Op++ = (float)float.MinValue;
                }

                if (count > 0)
                {
                    // Output is now always aligned to the start of a row.
                    ow = 0;
                    iwBase = -padWidth;
                    ihBase += strideHeight;

                    if (++oh == outputHeight)
                    {
                        // Advance to the next depth slice
                        oh = 0;
                        ihBase = -padHeight;
                        idBase += strideDepth;

                        if (++od == outputDepth)
                        {
                            // Advance to the next input channel.
                            od = 0;
                            idBase = -padDepth;
                            Xp += inputSize;
                        }
                    }
                }
            }
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal unsafe struct AveragePool2DJob : IParallelForBatch, IJobResourceDeclarationXO
    {
        public ReadOnlyMemResource X { get; set; } float* Xptr => (float*)X.ptr;
        public ReadWriteMemResource O { get; set; } float* Optr => (float*)O.ptr;
        public int inputHeight;
        public int inputWidth;
        public int outputHeight;
        public int outputWidth;
        public int poolHeight;
        public int poolWidth;
        public int strideHeight;
        public int strideWidth;
        public int padHeight;
        public int padWidth;

        public void Execute(int i, int count)
        {
            float* Op = Optr + i;

            // Extract the starting output position from the index.
            int ow = i % outputWidth;
            i = i / outputWidth;
            int oh = i % outputHeight;
            i = i / outputHeight;

            // Advance to the starting input channel.
            int inputSize = inputWidth * inputHeight;
            float* Xp = Xptr + i * inputSize;

            // Compute the base input indices from the starting output position.
            int iwBase = ow * strideWidth - padWidth;
            int ihBase = oh * strideHeight - padHeight;

            // Compute the first element index that is not eligible to use the vectorized path due to
            // the use of right padding. This index also serves as a flag to exclude unhandled stride
            // widths.
            //
            // For example, consider inputWidth=20 and poolWidth=3. The number of vector elements is 4
            // from float4, so an input row looks like the below:
            //
            //          01234567890123456789        input row, strideWidth=1
            //                        vvvv          iteration pw=0
            //                         vvvv         iteration pw=1
            //                          vvvv        iteration pw=2
            //
            //          01234567890123456789        input row, strideWidth=2
            //                     v v v v          iteration pw=0
            //                      v v v v         iteration pw=1
            //                       v v v v        iteration pw=2
            //
            // The vectorization path requires all of the input to be contained in the input tensor, so
            // the last element index of the last iteration of poolWidth is 19. The first element of the
            // first iteration of poolWidth can then be computed from the strideWidth and poolWidth as
            // shown below (vectorElementsSpanSize). Any starting width index after this position would not
            // provide enough valid data to be used in the pooling operation. This index is biased up by
            // one to allow the value to be used with the unsigned compare trick below.
            int iwBaseVectorLimit = 0;
            if (strideWidth == 1 || strideWidth == 2)
            {
                int vectorElementsSpanSize = ((4 - 1) * strideWidth + 1) + (poolWidth - 1);
                if (vectorElementsSpanSize <= inputWidth)
                    iwBaseVectorLimit = inputWidth - vectorElementsSpanSize + 1;
            }

            while (count > 0)
            {
                int outputCountW = math.min(count, outputWidth - ow);
                count -= outputCountW;

                // Clamp the effective range of the pooling to the input height.
                int ihClamped = math.max(0, ihBase);
                int ihClampedEnd = math.min(ihBase + poolHeight, inputHeight);

                float* initialXph = &Xp[ihClamped * inputWidth];
                int ihCount = ihClampedEnd - ihClamped;

                if (ihCount > 0)
                {
                    do
                    {
                        // Clamp the effective range of the pooling to the input width.
                        int iwClamped = math.max(0, iwBase);
                        int iwClampedEnd = math.min(iwBase + poolWidth, inputWidth);

                        // Compute the number of elements that are averaged in this iteration of the loop.
                        int elementCount = ihCount * math.max(0, iwClampedEnd - iwClamped);
                        float elementCountReciprocal = 1.0f / elementCount;

                        float* Xph = initialXph;

                        // Test if an entire vector can be produced and that all lanes of the vector are using data
                        // from inside the input tensor. If the stride width is unsupported, then the vector limit
                        // is zero and this test will always be false.
                        if (outputCountW >= 4 && (uint)iwBase < (uint)iwBaseVectorLimit)
                        {
                            float4 accVal = new float4(0.0f);

                            if (strideWidth == 1)
                            {
                                iwBase += 4 * 1;

                                for (int ih = 0; ih < ihCount; ih++)
                                {
                                    for (int iw = iwClamped; iw < iwClampedEnd; iw++)
                                    {
                                        float4 v = ((float4*)&Xph[iw])[0];
                                        accVal = accVal + v;
                                    }

                                    Xph += inputWidth;
                                }
                            }
                            else
                            {
                                iwBase += 4 * 2;

                                for (int ih = 0; ih < ihCount; ih++)
                                {
                                    for (int iw = iwClamped; iw < iwClampedEnd; iw++)
                                    {
                                        float4 v = VectorUtils.PackStride2(((float4*)&Xph[iw])[0], ((float4*)&Xph[iw])[1]);
                                        accVal = accVal + v;
                                    }

                                    Xph += inputWidth;
                                }
                            }

                            accVal = accVal * elementCountReciprocal;

                            *((float4*)Op) = accVal;
                            Op += 4;
                            outputCountW -= 4;
                        }
                        else
                        {
                            float accVal = 0.0f;

                            for (int ih = 0; ih < ihCount; ih++)
                            {
                                for (int iw = iwClamped; iw < iwClampedEnd; iw++)
                                {
                                    float v = Xph[iw];
                                    accVal = accVal + v;
                                }

                                Xph += inputWidth;
                            }

                            accVal = accVal * elementCountReciprocal;

                            *Op++ = accVal;
                            iwBase += strideWidth;
                            outputCountW -= 1;
                        }
                    }
                    while (outputCountW != 0);
                }
                else
                {
                    // The output row is entirely in the padding region.
                    for (float* lastOp = Op + outputCountW; Op < lastOp;)
                        *Op++ = (float)float.NaN;
                }

                if (count > 0)
                {
                    // Output is now always aligned to the start of a row.
                    ow = 0;
                    iwBase = -padWidth;
                    ihBase += strideHeight;

                    if (++oh == outputHeight)
                    {
                        // Advance to the next input channel.
                        oh = 0;
                        ihBase = -padHeight;
                        Xp += inputSize;
                    }
                }
            }
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal unsafe struct AveragePool3DJob : IParallelForBatch, IJobResourceDeclarationXO
    {
        public ReadOnlyMemResource X { get; set; }
        float* Xptr => (float*)X.ptr;
        public ReadWriteMemResource O { get; set; }
        float* Optr => (float*)O.ptr;
        public int inputDepth;
        public int inputHeight;
        public int inputWidth;
        public int outputDepth;
        public int outputHeight;
        public int outputWidth;
        public int poolDepth;
        public int poolHeight;
        public int poolWidth;
        public int strideDepth;
        public int strideHeight;
        public int strideWidth;
        public int padDepth;
        public int padHeight;
        public int padWidth;

        public void Execute(int i, int count)
        {
            float* Op = Optr + i;

            // Extract the starting output position from the index.
            int ow = i % outputWidth;
            i = i / outputWidth;
            int oh = i % outputHeight;
            i = i / outputHeight;
            int od = i % outputDepth;
            i = i / outputDepth;
            // i is now the channel num

            // Advance to the starting input channel.
            int inputDepthSliceSize = inputWidth * inputHeight;
            int inputSize = inputDepthSliceSize * inputDepth;
            float* Xp = Xptr + i * inputSize;

            // Compute the base input indices from the starting output position.
            int iwBase = ow * strideWidth - padWidth;
            int ihBase = oh * strideHeight - padHeight;
            int idBase = od * strideDepth - padDepth;

            int iwBaseVectorLimitExclusive = 0;
            if (strideWidth == 1 || strideWidth == 2) // only paths supported for vectorization for now
            {
                int vectorElementsSpanSize = ((4 - 1) * strideWidth + 1) + (poolWidth - 1);
                if (vectorElementsSpanSize <= inputWidth)
                    iwBaseVectorLimitExclusive = inputWidth - vectorElementsSpanSize + 1;
                // ...+1 for exclusive limit, that way we can use an unsigned compare to combine everything we want to test in one compare:
                // see below, iwBase can be 0, but iwBaseVectorLimitExclusive == 0 is reserved for unsupported vectorization path
            }

            while (count > 0)
            {
                // We process one row at a time:
                int outputCountW = math.min(count, outputWidth - ow);
                count -= outputCountW;

                // Clamp the effective range of the pooling to the input height and depth
                int ihClamped = math.max(0, ihBase);
                int ihClampedEnd = math.min(ihBase + poolHeight, inputHeight);

                int idClamped = math.max(0, idBase);
                int idClampedEnd = math.min(idBase + poolDepth, inputDepth);

                float* hdInitialXptr = &Xp[(idClamped * inputHeight + ihClamped) * inputWidth];
                int ihCount = ihClampedEnd - ihClamped;
                int idCount = idClampedEnd - idClamped;

                // To advance an input pointer below (hdStartXptr), we will calculate a "SkipToNextSliceSize":
                // ie when reaching the end of the input rows to process in the pooling op, the number of rows to skip
                // are the start rows to skip ( = ihClamped index itself) + left over rows,
                //      rows to skip = ihClamped + inputHeight - ihClampedEnd
                // but this is just
                //      rows to skip = inputHeight - ihCount
                int hdSkipToNextSliceSize = (inputHeight - ihCount) * inputWidth;

                if (idCount > 0 && ihCount > 0)
                {
                    do
                    {
                        // Clamp the effective range of the pooling to the input width.
                        int iwClamped = math.max(0, iwBase);
                        int iwClampedEnd = math.min(iwBase + poolWidth, inputWidth);

                        // Compute the number of elements that are averaged in this iteration of the loop.
                        int elementCount = idCount * ihCount * math.max(0, iwClampedEnd - iwClamped);
                        float elementCountReciprocal = 1.0f / elementCount;

                        float* hdStartXptr = hdInitialXptr;

                        // Test if an entire vector can be produced and that all lanes of the vector are using data
                        // from inside the input tensor. If the stride width is unsupported, then the vector limit
                        // is zero and this test will always be false.
                        if (outputCountW >= 4 && (uint)iwBase < (uint)iwBaseVectorLimitExclusive)
                        {
                            float4 accVal = new float4(0.0f);

                            if (strideWidth == 1)
                            {
                                iwBase += 4 * 1;

                                for (int id = 0; id < idCount; id++)
                                {
                                    for (int ih = 0; ih < ihCount; ih++)
                                    {
                                        for (int iw = iwClamped; iw < iwClampedEnd; iw++)
                                        {
                                            float4 v = ((float4*)&hdStartXptr[iw])[0];
                                            accVal = accVal + v;
                                        }

                                        hdStartXptr += inputWidth;
                                    }

                                    hdStartXptr += hdSkipToNextSliceSize;
                                }
                            }
                            else
                            {
                                iwBase += 4 * 2;

                                for (int id = 0; id < idCount; id++)
                                {
                                    for (int ih = 0; ih < ihCount; ih++)
                                    {
                                        for (int iw = iwClamped; iw < iwClampedEnd; iw++)
                                        {
                                            float4 v = VectorUtils.PackStride2(((float4*)&hdStartXptr[iw])[0], ((float4*)&hdStartXptr[iw])[1]);
                                            accVal = accVal + v;
                                        }

                                        hdStartXptr += inputWidth;
                                    }

                                    hdStartXptr += hdSkipToNextSliceSize;
                                }
                            }

                            accVal = accVal * elementCountReciprocal;

                            *((float4*)Op) = accVal;
                            Op += 4;
                            outputCountW -= 4;
                        }
                        else
                        {
                            float accVal = 0.0f;

                            for (int id = 0; id < idCount; id++)
                            {
                                for (int ih = 0; ih < ihCount; ih++)
                                {
                                    for (int iw = iwClamped; iw < iwClampedEnd; iw++)
                                    {
                                        float v = hdStartXptr[iw];
                                        accVal = accVal + v;
                                    }

                                    hdStartXptr += inputWidth;
                                }

                                hdStartXptr += hdSkipToNextSliceSize;
                            }

                            accVal = accVal * elementCountReciprocal;

                            *Op++ = accVal;
                            iwBase += strideWidth;
                            outputCountW -= 1;
                        }
                    }
                    while (outputCountW != 0);
                }
                else
                {
                    // The output row is entirely in the padding region.
                    for (float* lastOp = Op + outputCountW; Op < lastOp;)
                        *Op++ = (float)float.NaN;
                }

                if (count > 0)
                {
                    // Output is now always aligned to the start of a row.
                    ow = 0;
                    iwBase = -padWidth;
                    ihBase += strideHeight;

                    if (++oh == outputHeight)
                    {
                        // Advance to the next depth slice
                        oh = 0;
                        ihBase = -padHeight;
                        idBase += strideDepth;

                        if (++od == outputDepth)
                        {
                            // Advance to the next input channel.
                            od = 0;
                            idBase = -padDepth;
                            Xp += inputSize;
                        }
                    }
                }
            }
        }
    }

}
}
