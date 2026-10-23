using System;
using System.Collections.Generic;
using Unity.InferenceEngine.Graph;

namespace Unity.InferenceEngine.Compiler.Passes.Optimization
{
    /// <summary>
    /// Matches and replaces nodes which do identical calculations.
    /// </summary>
    class RemoveDuplicatesPass : GraphPass
    {
        static readonly HashSet<string> s_RandomTargets = new() { "RandomNormal", "RandomUniform", "RandomNormalLike", "RandomUniformLike", "Multinomial", "Bernoulli" };

        public override void Run(GraphModule gm)
        {
            var nodesWithHashCode = new Dictionary<int, List<Node>>();

            var nodes = new List<Node>();
            foreach (var node in gm.graph.Nodes())
                nodes.Add(node);
            foreach (var node in nodes)
            {
                var hashCode = CalculateHashCode(node);
                if (!nodesWithHashCode.TryGetValue(hashCode, out var possibleMatchNodes))
                {
                    possibleMatchNodes = new List<Node>();
                    nodesWithHashCode[hashCode] = possibleMatchNodes;
                }

                Node matchNode = null;

                foreach (var possibleMatchNode in possibleMatchNodes)
                {
                    if (!IsEqualNodes(node, possibleMatchNode))
                        continue;
                    matchNode = possibleMatchNode;
                    break;
                }

                if (matchNode is not null)
                {
                    node.ReplaceAllUsesWith(matchNode);
                    gm.graph.EraseNode(node);
                }
                else
                {
                    possibleMatchNodes.Add(node);
                }
            }
        }

        static int CalculateHashCode(Argument[] args)
        {
            if (args is null)
                return 0;
            var hashCode = HashCode.Combine(args.Length);
            foreach (var arg in args)
                hashCode = HashCode.Combine(hashCode, CalculateHashCode(arg));
            return hashCode;
        }

        static int CalculateHashCode(Argument arg)
        {
            if (arg is null)
                return 0;
            if (arg.IsArguments)
                return CalculateHashCode(arg.AsArguments);
            if (arg.IsNode && arg.AsNode.op == Node.kOpGetAttr)
                return arg.AsNode.partialTensor.GetHashCode();
            return arg.GetHashCode();
        }

        static int CalculateHashCode(Node node)
        {
            return HashCode.Combine(node.op, node.target, CalculateHashCode(node.args));
        }

        static bool IsEqualNodes(Node a, Node b)
        {
            if (a.op != b.op)
                return false;
            if (a.target != b.target)
                return false;
            // random functions are never judged equal
            if (s_RandomTargets.Contains(a.target))
                return false;
            return IsEqualArgs(a.args, b.args);
        }

        static bool IsEqualArg(Argument a, Argument b)
        {
            if (a == null && b == null)
                return true;
            if (a == null || b == null)
                return false;
            if (a.Index != b.Index)
                return false;
            if (a.IsArguments)
                return IsEqualArgs(a.AsArguments, b.AsArguments);
            if (a.IsNode)
                return a.AsNode == b.AsNode || AreEqualSmallConstants(a.AsNode, b.AsNode);
            return a.Equals(b);
        }

        static bool IsEqualArgs(Argument[] a, Argument[] b)
        {
            if (a.Length != b.Length)
                return false;
            for (var i = 0; i < a.Length; i++)
            {
                if (!IsEqualArg(a[i], b[i]))
                    return false;
            }
            return true;
        }

        static bool AreEqualSmallConstants(Node a, Node b)
        {
            return a.partialTensor != null && PartialTensor.IsEqual(a.partialTensor, b.partialTensor);
        }
    }
}
