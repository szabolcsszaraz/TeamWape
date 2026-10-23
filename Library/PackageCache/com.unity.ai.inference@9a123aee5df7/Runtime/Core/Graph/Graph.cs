using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine.Assertions;

[assembly: InternalsVisibleTo("Unity.InferenceEngine.EditorTests")]

namespace Unity.InferenceEngine.Graph
{
    /// <summary>
    /// Represents a graph as a series of nodes with strong connections.
    /// see https://github.com/pytorch/pytorch/blob/main/torch/fx/graph.py
    /// </summary>
    class Graph
    {
        public Node root;
        public Action<Node> insert;
        public int len;
        // this provides unique names when adding nodes
        public GraphNamespace graphNamespace = new();
        // the graph module this graph belongs to
        public GraphModule owningModule;
        // used for quick retrieval of function nodes with a given target
        public FindNodesLookupTable findNodesLookupTable = new();

        public Graph(GraphModule owningModule = null)
        {
            root = new Node(this, "", Node.kOpRoot, "", Array.Empty<Argument>(), null);
            insert = root.Prepend;
            this.owningModule = owningModule;
        }

        /// <summary>
        /// Iterator for all nodes.
        /// </summary>
        public NodeList Nodes() => new(this);

        /// <summary>
        /// The single output node of the graph.
        /// </summary>
        public Node OutputNode()
        {
            var outputNode = root.prev;
            Assert.IsNotNull(outputNode);
            Assert.IsTrue(outputNode.op == Node.kOpOutput);
            return outputNode;
        }

        /// <summary>
        /// Returns a list of all nodes in the graph matching the op and target, optionally sorted by their key.
        /// </summary>
        public List<Node> FindNodes(string op, string target = null, bool sort = true)
        {
            var nodeList = findNodesLookupTable.FindNodes(op, target);
            if (sort)
                nodeList.Sort();
            return nodeList;
        }

        /// <summary>
        /// Copies all nodes from a given graph into this graph.
        /// </summary>
        public Argument GraphCopy(Graph g, Dictionary<Node, Node> valMap)
        {
            foreach (var node in g.Nodes())
            {
                if (valMap.ContainsKey(node))
                    continue;

                if (node.op == Node.kOpOutput)
                {
                    var rv = GraphUtils.MapArg(node.args[0], n => valMap[n]);
                    return rv;
                }

                valMap[node] = NodeCopy(node, n => valMap[n]);
            }

            return null;
        }

        /// <summary>
        /// Creates a node at the current insert.
        /// </summary>
        public Node CreateNode(string op, string target, Argument[] args, string name = null, Type type = null)
        {
            var candidate = name ?? target;
            name = graphNamespace.CreateName(candidate, null);
            var n = new Node(this, name, op, target, args, type);
            graphNamespace.AssociateNameWithObj(name, n);
            insert(n);
            findNodesLookupTable.Insert(n);
            len++;
            return n;
        }

        /// <summary>
        /// Erases a node from the graph.
        /// </summary>
        public void EraseNode(Node toErase)
        {
            Assert.IsTrue(toErase.users.Count == 0);
            Assert.IsTrue(toErase.graph == this);

            findNodesLookupTable.Remove(toErase);
            toErase.RemoveFromList();
            toErase.erased = true;
            len--;

            var newArgs = GraphUtils.MapArg(toErase.args, _ => null);
            toErase.UpdateArgs(newArgs);
        }

        /// <summary>
        /// Sets the insert point to be directly before a node.
        /// </summary>
        public IDisposable InsertingBefore(Node node)
        {
            if (node == null)
                return InsertingAfter(root);
            Assert.IsTrue(node.graph == this);
            return new InsertionScope(this, node.Prepend);
        }

        /// <summary>
        /// Sets the insert point to be directly after a node.
        /// </summary>
        public IDisposable InsertingAfter(Node node)
        {
            if (node == null)
                return InsertingBefore(root);
            Assert.IsTrue(node.graph == this);
            return new InsertionScope(this, node.Append);
        }

        /// <summary>
        /// Represents a scope to use with 'using', when disposed the previous scope will be set.
        /// </summary>
        class InsertionScope : IDisposable
        {
            Graph m_Graph;
            Action<Node> m_PrevInsert;

            public InsertionScope(Graph graph, Action<Node> insert)
            {
                m_Graph = graph;
                m_PrevInsert = graph.insert;
                graph.insert = insert;
            }

            public void Dispose()
            {
                m_Graph.insert = m_PrevInsert;
            }
        }

        /// <summary>
        /// Creates an input node at the current insert.
        /// </summary>
        public Node Placeholder(string name, Type type = null)
        {
            var args = Array.Empty<Argument>();
            return CreateNode(Node.kOpPlaceholder, name, args, type: type);
        }

        /// <summary>
        /// Creates a get attribute node at the current insert. The qualified name should match a name in the graph module attributes.
        /// </summary>
        public Node GetAttr(string qualifiedName, string name = null, Type type = null)
        {
            return CreateNode(Node.kOpGetAttr, qualifiedName, Array.Empty<Argument>(), type: type);
        }

        /// <summary>
        /// Creates a call function node at the current insert.
        /// </summary>
        public Node CallFunction(string functionName, Argument[] args, string name = null, Type type = null)
        {
            return CreateNode(Node.kOpCallFunction, functionName, args, name, type);
        }

        /// <summary>
        /// Creates a copy of a node at the current insert. Arguments are transformed from the original node with a delegate.
        /// </summary>
        public Node NodeCopy(Node node, Func<Node, Node> argTransform)
        {
            var args = GraphUtils.MapArg(node.args, argTransform);

            var resultNode = CreateNode(node.op, node.target, args, node.name, node.type);
            resultNode.meta = new Dictionary<string, object>(node.meta);

            return resultNode;
        }

        /// <summary>
        /// Creates the output node at the current insert.
        /// </summary>
        public Node Output(Node[] outputs, Type type = null)
        {
            return CreateNode(Node.kOpOutput, Node.kOpOutput, new Argument[] { outputs }, type: type);
        }

        /// <summary>
        /// Validates that a graph is topologically valid.
        /// </summary>
        public void Lint()
        {
            var seenNames = new HashSet<string>();
            var seenValues = new HashSet<Node>();

            void CheckArg(Node arg, Node n = null)
            {
                Assert.IsTrue(arg.graph == this);
                Assert.IsTrue(seenValues.Contains(arg));
            }

            foreach (var node in Nodes())
            {
                Assert.IsTrue(node.graph == this);
                Assert.IsTrue(findNodesLookupTable.Contains(node));
                foreach (var arg in node.inputNodes)
                    CheckArg(arg, node);
                seenValues.Add(node);
                Assert.IsTrue(!seenNames.Contains(node.name));
                seenNames.Add(node.name);
            }
        }

        /// <summary>
        /// Removes nodes that do not contribute to the calculation of an output.
        /// </summary>
        public bool EliminateDeadCode(Func<Node, bool> isImpureNode = null)
        {
            Lint();

            var changed = false;
            var reversedNodes = Nodes().GetReversed();
            foreach (var node in reversedNodes)
            {
                var hasSideEffect = isImpureNode?.Invoke(node) ?? node.IsImpure();
                if (!hasSideEffect && node.users.Count == 0)
                {
                    EraseNode(node);
                    changed = true;
                }
            }

            return changed;
        }

        public override string ToString()
        {
            var placeholderNames = new List<string>();
            var maybeReturnTypename = "";
            var nodeStrs = new List<string>();
            var nodes = Nodes();
            foreach (var node in nodes)
                nodeStrs.Add(node.FormatNode(placeholderNames, ref maybeReturnTypename));
            var paramStr = string.Join(", ", placeholderNames);
            var sb = new StringBuilder($"graph({paramStr}){maybeReturnTypename}:");
            foreach (var nodeStr in nodeStrs)
                sb.Append("\n    " + nodeStr);
            return sb.ToString();
        }
    }
}
