using System;

namespace Unity.InferenceEngine.Layers
{
    /// <summary>
    /// Represents the abstract base class for layers which generate random values in the output tensor.
    /// </summary>
    abstract class RandomLayer : Layer
    {
        public bool hasSeed;
        public int seed;
        [NonSerialized]
        Random m_Random;

        protected int NextSeed => m_Random.NextSeed();

        public void ResetSeed()
        {
            m_Random = hasSeed ? new Random(seed) : new Random();
        }
    }

    /// <summary>
    /// Represents a `RandomNormal` random layer. This generates an output tensor of a given shape with random values in a normal distribution with given `mean` and `scale`, and an optional `seed` value.
    /// </summary>
    [Operator(category = "Random")]
    [Inputs(names = new string[0])]
    partial class RandomNormal : RandomLayer
    {
        public float mean;
        public float scale;
        public int[] shape;

        internal static PartialTensor InferPartial(float mean, float scale, int[] shape, bool hasSeed, int seed)
        {
            return new PartialTensor<float>(new DynamicTensorShape(new TensorShape(shape)));
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], new TensorShape(shape), DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.RandomNormal(O, mean, scale, NextSeed);
        }
    }

    /// <summary>
    /// Represents a `RandomNormalLike` random layer. This generates an output tensor with the same shape as the input tensor with random values in a normal distribution, with given `mean` and `scale`, and an optional `seed` value.
    /// </summary>
    [Operator(category = "Random")]
    [Inputs(names = new[] { "input" }, inputNoDataDependency = new[] { 0 })]
    partial class RandomNormalLike : RandomLayer
    {
        public float mean;
        public float scale;

        internal static PartialTensor InferPartial(PartialTensor input, float mean, float scale, bool hasSeed, int seed)
        {
            return new PartialTensor<float>(input.shape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var shapeX = ctx.storage.GetTensorShape(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], shapeX, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.RandomNormal(O, mean, scale, NextSeed);
        }
    }

    /// <summary>
    /// Represents a `RandomUniform` random layer. This generates an output tensor of a given shape with random values in a uniform distribution between a given `low` and `high`, from an optional `seed` value.
    /// </summary>
    [Operator(category = "Random")]
    [Inputs(names = new string[0])]
    partial class RandomUniform : RandomLayer
    {
        public float low;
        public float high;
        public int[] shape;

        internal static PartialTensor InferPartial(float low, float high, int[] shape, bool hasSeed, int seed)
        {
            return new PartialTensor<float>(new DynamicTensorShape(new TensorShape(shape)));
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], new TensorShape(shape), DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.RandomUniform(O, low, high, NextSeed);
        }
    }

    /// <summary>
    /// Represents a `RandomUniformLike` random layer. This generates an output tensor with the same shape as the input tensor random values in a uniform distribution between a given `low` and `high`, from an optional `seed` value.
    /// </summary>
    [Operator(category = "Random")]
    [Inputs(names = new[] { "input" }, inputNoDataDependency = new[] { 0 })]
    partial class RandomUniformLike : RandomLayer
    {
        public float low;
        public float high;

        internal static PartialTensor InferPartial(PartialTensor input, float low, float high, bool hasSeed, int seed)
        {
            return new PartialTensor<float>(input.shape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var shapeX = ctx.storage.GetTensorShape(inputs[0]);
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], shapeX, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.RandomUniform(O, low, high, NextSeed);
        }
    }

    /// <summary>
    /// Represents a `Bernoulli` random layer. This generates an output tensor with values 0 or 1 from a Bernoulli distribution. The input tensor contains the probabilities used for generating the output values.
    /// </summary>
    [Operator(category = "Random")]
    [Inputs(names = new[] { "input" })]
    partial class Bernoulli : RandomLayer
    {
        public DataType dataType;

        internal static PartialTensor InferPartial(PartialTensor input, DataType dataType, bool hasSeed, int seed)
        {
            return PartialTensor.Create(dataType, input.shape);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Bernoulli(X, O, NextSeed);
        }
    }

    /// <summary>
    /// Represents a `Multinomial` random layer. This generates an output tensor with values from a multinomial distribution according to the probabilities given by the input tensor.
    /// </summary>
    [Operator(category = "Random")]
    [Inputs(names = new[] { "input" })]
    partial class Multinomial : RandomLayer
    {
        public int count;

        internal static PartialTensor InferPartial(PartialTensor input, int count, bool hasSeed, int seed)
        {
            var shapeInput = input.shape;
            return new PartialTensor<int>(new DynamicTensorShape(shapeInput[0], DynamicTensorDim.Int(count)));
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], new TensorShape(X.shape[0], count), DataType.Int, ctx.backend.backendType) as Tensor<int>;

            var Xtmp = ctx.storage.AllocateTensor(X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            var random = ctx.storage.AllocateTensor(new TensorShape(X.shape[0], count), DataType.Float, ctx.backend.backendType) as Tensor<float>;

            ctx.backend.RandomUniform(random, 0, 1, NextSeed);
            ctx.backend.Softmax(X, Xtmp, -1);
            ctx.backend.TopP(Xtmp, random, O);

            ctx.storage.Dispose(Xtmp);
            ctx.storage.Dispose(random);
        }
    }
}
