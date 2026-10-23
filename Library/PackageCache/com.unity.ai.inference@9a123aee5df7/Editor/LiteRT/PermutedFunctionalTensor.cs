using Unity.InferenceEngine.Graph;

namespace Unity.InferenceEngine.Editor.LiteRT
{
    /// <summary>
    /// Represents an intermediate tensor with a permutation.
    ///
    /// The tensor layout of the outputs of certain ops, such as Conv do not match between LiteRT and ONNX.
    /// Rather than applying a Transpose op and returning the Node, we instead return the PermutedNode.
    /// </summary>
    class PermutedFunctionalTensor
    {
        // Whether the functional tensor is a constant, if it is then we will prefer to transpose it, as the resultant ops will be removed in the model optimization pass.
        public readonly bool isConstant;

        // The permuted tensor, permutedTensor = tensor.Transpose(permutation)
        public readonly Node permutedTensor;

        // The transpose which has been applied to this virtual tensor to give the permuted tensor.
        public Permutation permutation;

        GraphModule gm => permutedTensor.graph.owningModule;

        public PermutedFunctionalTensor(Node permutedTensor, Permutation? permutation = null, bool isConstant = false)
        {
            this.permutedTensor = permutedTensor;
            this.permutation = permutation ?? Permutation.Identity(permutedTensor.partialTensor.shape.rank);
            this.isConstant = isConstant;
        }

        /// <summary>
        /// Returns an unpermuted tensor node.
        /// Since permutedTensor = tensor.Transpose(permutation), this means the return value tensor = permutedTensor.Transpose(permutation.Inverse()).
        ///
        /// Sometimes we require the unpermuted tensor node with a different permutation than is stored.
        /// E.g. the input for a liteRT conv2d has to be transposed to NCHW space to be compatible with ONNX Conv, in this case we provided the required permutation as an argument to the method.
        /// This permutation combines with the inverse internal permutation to give a single transpose.
        ///
        /// In some cases the required permutation will match the internal stored permutation.
        /// E.g. the output of a Conv is NCHW format, which is then fed as the input to another Conv.
        /// The permutations will cancel and the permuted tensor can be used directly by the next op without doing any transform.
        /// </summary>
        public Node GetTensor(Permutation? newPermutation = null)
        {
            var relativePermutation = newPermutation.HasValue ? newPermutation.Value.Compound(permutation.Inverse()) : permutation.Inverse();
            return relativePermutation.IsIdentity() ? permutedTensor : permutedTensor.graph.owningModule.Transpose(permutedTensor, relativePermutation.ToArray());
        }

        /// <summary>
        /// Returns a permuted tensor with a specific permutation, even if we have to do a transpose.
        /// This is useful if we are doing a broadcast op with a constant.
        /// E.g. z = x + y
        /// x is a permuted tensor with permutation (1, 2, 0)
        /// y is a constant with no permutation
        /// since a transpose on a constant will be precalculated in the optimization path, we prefer to transpose y to match x
        /// and then do an operation on the permuted tensors, so z is also permuted
        /// </summary>
        public PermutedFunctionalTensor GetPermutedTensorForBroadcastOp(Permutation newPermutation)
        {
            if (newPermutation == permutation)
                return this;
            var tensorNode = GetTensor();
            tensorNode = LiteRTModelConverter.BroadcastToRank(tensorNode, newPermutation.rank);
            if (!(permutedTensor.partialTensor.shape.Length() == DynamicTensorDim.One))
                tensorNode = gm.Transpose(tensorNode, newPermutation.ToArray());
            return new PermutedFunctionalTensor(tensorNode, newPermutation);
        }
    }
}
