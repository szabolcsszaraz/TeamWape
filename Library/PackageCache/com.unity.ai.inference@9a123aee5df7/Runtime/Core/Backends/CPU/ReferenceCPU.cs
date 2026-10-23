using System;
using UnityEngine;

namespace Unity.InferenceEngine
{
    /// <summary>
    /// Represents a CPU backend ops.
    /// </summary>
    partial class CPUBackend : IBackend
    {
        /// <inheritdoc/>
        public BackendType backendType => BackendType.CPU;

        void ResizeNDRef(Tensor<float> X, Tensor<float> O, ReadOnlySpan<float> scale, Layers.InterpolationMode interpolationMode, Layers.NearestMode nearestMode = Layers.NearestMode.RoundPreferFloor, Layers.CoordTransformMode coordTransformMode = Layers.CoordTransformMode.HalfPixel)
        {
            bool firstAlloc = false;
            for (var i = 0; i < scale.Length; i++)
            {
                var Otmp = i == scale.Length - 1 ? O : AllocTensorFloat(ShapeInference.Resize(X.shape, i, scale[i]));
                Resize1DRef(X, Otmp, i, scale[i], interpolationMode, nearestMode, coordTransformMode);
                if (firstAlloc)
                    ReleaseTensorFloat(X);
                X = Otmp;
                firstAlloc = true;
            }
        }

        void Resize1DRef(Tensor<float> X, Tensor<float> O, int axis, float scale, Layers.InterpolationMode interpolationMode, Layers.NearestMode nearestMode, Layers.CoordTransformMode coordTransformMode)
        {
            CPUTensorData.Pin(X);
            CPUTensorData.Pin(O);

            var itX = new TensorNDIterator(X.shape);

            for (var itO = new TensorNDIterator(O.shape); itO.HasNext(); itO.MoveNext())
            {
                itX.CopyNDIndex(itO);

                OpsUtils.GetScaleAndBias(X.shape[axis], O.shape[axis], scale, coordTransformMode, interpolationMode, nearestMode, out float outputScale, out float outputBias);

                float inputCoord = Math.Max(0.0f, itO[axis] * outputScale + outputBias);

                if (interpolationMode == Layers.InterpolationMode.Linear)
                {
                    int indexValue = (int)inputCoord;
                    float x_c0 = inputCoord - Mathf.Floor(inputCoord);
                    float x_c1 = 1.0f - x_c0;

                    itX[axis] = Mathf.Clamp(indexValue, 0, X.shape[axis] - 1);
                    float x0 = X[itX.index];
                    itX[axis] = Mathf.Clamp(indexValue + 1, 0, X.shape[axis] - 1);
                    float x1 = X[itX.index];

                    O[itO.index] = x_c0 * x1 + x_c1 * x0;
                }
                else
                {
                    int indexValue = 0;
                    switch (nearestMode)
                    {
                        case Layers.NearestMode.RoundPreferFloor:
                        case Layers.NearestMode.Ceil:
                            indexValue = (int)Mathf.Ceil(inputCoord);
                            break;
                        case Layers.NearestMode.RoundPreferCeil:
                        case Layers.NearestMode.Floor:
                            indexValue = (int)Mathf.Floor(inputCoord);
                            break;
                    }

                    itX[axis] = Mathf.Clamp(indexValue, 0, X.shape[axis] - 1);
                    O[itO.index] = X[itX.index];
                }
            }
        }

        void ApplyLocalPoolingOperator(Tensor<float> X, Tensor<float> O, int[] pool, int[] stride, int[] pad, Func<float> initOp, Func<float, float, float> accumulateOp, Func<float, int, float> normalizeOp)
        {
            CPUTensorData.Pin(X);
            CPUTensorData.Pin(O);

            var itX = new TensorNDIterator(X.shape);
            var itP = new TensorNDIterator(new TensorShape(pool));
            for (var itO = new TensorNDIterator(O.shape); itO.HasNext(); itO.MoveNext())
            {
                itX[0] = itO[0];
                itX[1] = itO[1];

                float acc = initOp();
                int elementCount = 0;

                itP.Reset();
                for (; itP.HasNext(); itP.MoveNext())
                {
                    bool outOfBounds = false;
                    for (int i = 0; i < pool.Length; i++)
                    {
                        int ox = itO[2 + i] * stride[i] + itP[i] - pad[i];

                        if ((ox < 0) || (ox >= X.shape[2 + i]))
                        {
                            outOfBounds = true;
                            break;
                        }

                        itX[2 + i] = ox;
                    }

                    if (!outOfBounds)
                    {
                        acc = accumulateOp(acc, X[itX.index]);
                        elementCount++;
                    }
                }

                O[itO.index] = normalizeOp(acc, elementCount);
            }
        }

        void MaxPoolND(Tensor<float> X, Tensor<float> O, int[] pool, int[] stride, int[] pad)
        {
            Func<float> initOp = () => float.MinValue;
            Func<float, float, float> accumulateOp = (acc, v) => Mathf.Max(acc, v);
            Func<float, int, float> normalizeOp = (acc, elementCount) => acc;
            ApplyLocalPoolingOperator(X, O, pool, stride, pad, initOp, accumulateOp, normalizeOp);
        }

        void AveragePoolND(Tensor<float> X, Tensor<float> O, int[] pool, int[] stride, int[] pad)
        {
            Func<float> initOp = () => 0.0f;
            Func<float, float, float> accumulateOp = (acc, v) => acc + v;
            Func<float, int, float> normalizeOp = (acc, elementCount) => acc / elementCount;
            ApplyLocalPoolingOperator(X, O, pool, stride, pad, initOp, accumulateOp, normalizeOp);
        }
    }
}
