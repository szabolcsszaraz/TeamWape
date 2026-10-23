using System.Collections.Generic;
using System.Linq;

namespace Unity.InferenceEngine.Editor.Onnx
{
    static class ONNXModelUtility
    {
        // This is an O(N + E) topological sort on the graph.
        // It is stable, so the ordering is the same as the input graph if it is already sorted.
        public static List<NodeProto> StableTopologicalSort(GraphProto onnxGraph)
        {
            var sortedGraph = new List<NodeProto>();

            // Fast lookups for inputs and initializers
            var graphInputs = new HashSet<string>(onnxGraph.Input.Select(i => i.Name));
            var graphInitializers = new HashSet<string>(onnxGraph.Initializer.Select(i => i.Name));

            // Maps
            var outputToNode = new Dictionary<string, NodeProto>();
            var inDegree = new Dictionary<NodeProto, int>();
            var nodeDependents = new Dictionary<NodeProto, List<NodeProto>>();

            // Build output to node map
            foreach (var node in onnxGraph.Node)
            {
                foreach (var output in node.Output)
                {
                    if (!string.IsNullOrEmpty(output))
                        outputToNode[output] = node;
                }
            }

            // Initialize dependency maps
            foreach (var node in onnxGraph.Node)
            {
                inDegree[node] = 0;
                nodeDependents[node] = new List<NodeProto>();
            }

            // Build dependency graph (edges: A to B if A outputs something B needs)
            foreach (var node in onnxGraph.Node)
            {
                foreach (var input in node.Input)
                {
                    if (string.IsNullOrEmpty(input)) continue;

                    // If input is external, skip it
                    if (graphInputs.Contains(input) || graphInitializers.Contains(input))
                        continue;

                    if (outputToNode.TryGetValue(input, out var dependency))
                    {
                        nodeDependents[dependency].Add(node);
                        inDegree[node]++;
                    }
                }
            }

            // Use a queue to track ready nodes in original order
            var readyQueue = new Queue<NodeProto>();

            foreach (var node in onnxGraph.Node)
            {
                if (inDegree[node] == 0)
                    readyQueue.Enqueue(node);
            }

            while (readyQueue.Count > 0)
            {
                var node = readyQueue.Dequeue();
                sortedGraph.Add(node);

                foreach (var dependent in nodeDependents[node])
                {
                    inDegree[dependent]--;
                    if (inDegree[dependent] == 0)
                        readyQueue.Enqueue(dependent);
                }
            }

            if (sortedGraph.Count != onnxGraph.Node.Count)
            {
                var remaining = onnxGraph.Node.Except(sortedGraph).Select(n => n.Name);
                throw new OnnxImportException("Cycle or missing dependency in graph: " + string.Join(", ", remaining));
            }

            return sortedGraph;
        }
    }
}
