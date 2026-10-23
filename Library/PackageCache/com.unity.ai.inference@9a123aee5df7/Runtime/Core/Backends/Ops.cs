using System;
using static Unity.InferenceEngine.CPUTensorData;
using Unity.Jobs;

namespace Unity.InferenceEngine
{
    class CPUOps : Ops
    {
        CPUBackend m_CPUBackend => m_Backend as CPUBackend;

        public CPUOps()
            : base(BackendType.CPU) { }

        public Tensor<float> WindowedDFTMatrix(Tensor<float> window, int dftLength, int inputFrameLength, bool inverse, bool onesided, bool alternateRealImaOnRows)
        {
            int outputXformSignalLength = onesided ? dftLength / 2 + 1 : dftLength;
            int numRows = outputXformSignalLength;
            if (alternateRealImaOnRows)
                numRows *= 2;
            int numCols = alternateRealImaOnRows ? inputFrameLength : inputFrameLength * 2;

            var twiddleMatrixShape = new TensorShape(numRows, numCols);
            var O = new Tensor<float>(twiddleMatrixShape, data:null);

            m_CPUBackend.WindowedDFTMatrix(window, O, dftLength: dftLength, inputFrameLength: inputFrameLength, inverse: inverse, onesided, alternateRealImaOnRows, scale: 1.0f);
            return O;
        }
    }

    abstract class Ops : IDisposable
    {
        protected IBackend m_Backend;

        protected Ops(BackendType backendType)
        {
            m_Backend = BackendFactory.CreateBackend(backendType);
        }

        public void Dispose()
        {
            m_Backend?.Dispose();
        }

        internal Tensor<float> ScalarMad(Tensor<float> X, float s, float b)
        {
            var O = new Tensor<float>(X.shape, data: null);
            if (O.shape.HasZeroDims())
                return O;
            m_Backend.ScalarMad(X, O, s, b);
            return O;
        }

        internal Tensor<int> ScalarMad(Tensor<int> X, int s, int b)
        {
            var O = new Tensor<int>(X.shape, data: null);
            if (O.shape.HasZeroDims())
                return O;
            m_Backend.ScalarMad(X, O, s, b);
            return O;
        }

        public Tensor<float> MatMul2D(Tensor<float> X, Tensor<float> Y, bool xTranspose, bool yTranspose)
        {
            var O = new Tensor<float>(ShapeInference.Gemm(X.shape, Y.shape, xTranspose, yTranspose), data: null);
            if (O.shape.HasZeroDims())
                return O;
            if (X.shape.HasZeroDims() || Y.shape.HasZeroDims())
                m_Backend.MemSet(O, 0.0f);
            else
                m_Backend.MatMul2D(X, Y, O, xTranspose, yTranspose);
            return O;
        }

        public Tensor<float> Dense(Tensor<float> X, Tensor<float> W, Tensor<float> B)
        {
            var O = new Tensor<float>(X.shape.MatMul(W.shape), data: null);
            if (O.shape.HasZeroDims())
                return O;
            m_Backend.Dense(X, W, B, O, Layers.FusableActivation.None);
            return O;
        }

        public Tensor<float> Add(Tensor<float> A, Tensor<float> B)
        {
            var O = new Tensor<float>(TensorShapeHelper.BroadcastShape(A, B), data: null);
            if (O.shape.HasZeroDims())
                return O;
            m_Backend.Add(A, B, O);
            return O;
        }

        public Tensor<int> Add(Tensor<int> A, Tensor<int> B)
        {
            var O = new Tensor<int>(TensorShapeHelper.BroadcastShape(A, B), data: null);
            if (O.shape.HasZeroDims())
                return O;
            m_Backend.Add(A, B, O);
            return O;
        }

        public Tensor<float> Sub(Tensor<float> A, Tensor<float> B)
        {
            var O = new Tensor<float>(TensorShapeHelper.BroadcastShape(A, B), data: null);
            if (O.shape.HasZeroDims())
                return O;
            m_Backend.Sub(A, B, O);
            return O;
        }

        public Tensor<int> Sub(Tensor<int> A, Tensor<int> B)
        {
            var O = new Tensor<int>(TensorShapeHelper.BroadcastShape(A, B), data: null);
            if (O.shape.HasZeroDims())
                return O;
            m_Backend.Sub(A, B, O);
            return O;
        }

        public Tensor<float> Mul(Tensor<float> A, Tensor<float> B)
        {
            var O = new Tensor<float>(TensorShapeHelper.BroadcastShape(A, B), data: null);
            if (O.shape.HasZeroDims())
                return O;
            m_Backend.Mul(A, B, O);
            return O;
        }

        public Tensor<int> Mul(Tensor<int> A, Tensor<int> B)
        {
            var O = new Tensor<int>(TensorShapeHelper.BroadcastShape(A, B), data: null);
            if (O.shape.HasZeroDims())
                return O;
            m_Backend.Mul(A, B, O);
            return O;
        }

        public Tensor<float> Div(Tensor<float> A, Tensor<float> B)
        {
            var O = new Tensor<float>(TensorShapeHelper.BroadcastShape(A, B), data: null);
            if (O.shape.HasZeroDims())
                return O;
            m_Backend.Div(A, B, O);
            return O;
        }

        public Tensor<float> Sqrt(Tensor<float> X)
        {
            var O = new Tensor<float>(X.shape, data: null);
            if (O.shape.HasZeroDims())
                return O;
            m_Backend.Sqrt(X, O);
            return O;
        }

        public Tensor<float> Transpose(Tensor<float> X)
        {
            var O = new Tensor<float>(X.shape.Transpose(), data: null);
            if (O.shape.HasZeroDims())
                return O;
            m_Backend.Transpose(X, O);
            return O;
        }

        public Tensor<float> ConstantOfShape(TensorShape X, float value)
        {
            var O = new Tensor<float>(X, data: null);
            if (O.shape.HasZeroDims())
                return O;
            m_Backend.MemSet(O, value);
            return O;
        }

        public Tensor<T> Copy<T>(Tensor<T> X) where T : unmanaged
        {
            var O = new Tensor<T>(X.shape, data: null);
            if (O.shape.HasZeroDims())
                return O;
            m_Backend.MemCopy(X, O);
            return O;
        }

        public Tensor<T> Expand<T>(Tensor<T> X, TensorShape shape) where T : unmanaged
        {
            var O = new Tensor<T>(X.shape.Broadcast(shape), data: null);
            if (O.shape.HasZeroDims())
                return O;
            m_Backend.Expand(X, O);
            return O;
        }

        public Tensor<T> Reshape<T>(Tensor<T> X, TensorShape shape) where T : unmanaged
        {
            var O = new Tensor<T>(shape, data: null);
            if (O.shape.HasZeroDims())
                return O;
            m_Backend.MemCopy(X, O);
            return O;
        }
    }
}
