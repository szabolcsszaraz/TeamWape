using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Assertions;

namespace Unity.InferenceEngine.Graph
{
    /// <summary>
    /// Represents a node in the graph as part of a linked list.
    /// see https://github.com/pytorch/pytorch/blob/main/torch/fx/node.py
    /// </summary>
    class Node : IComparable<Node>
    {
        public const string kOpPlaceholder = "placeholder";
        public const string kOpCallFunction = "call_function";
        public const string kOpGetAttr = "get_attr";
        public const string kOpOutput = "output";
        public const string kOpRoot = "root";

        public bool erased;
        public Node next;
        public Node prev;
        public Argument[] args;
        public readonly Graph graph;
        public readonly string name;
        public readonly string op;
        public readonly string target;
        public NodeSet inputNodes = new();
        public NodeSet users = new();
        public readonly Type type;
        public SortKey sortKey;
        public Dictionary<string, object> meta = new();

        // This is non-standard, we need it as small int arrays and symbolic int tensors (e.g. shape input to reshape) are stored as tensors in ONNX
        public PartialTensor partialTensor;

        protected Node() { }

        public Node(Graph graph, string name, string op, string target, Argument[] args, Type returnType)
        {
            Assert.IsTrue(op is kOpPlaceholder or kOpCallFunction or kOpGetAttr or kOpOutput or kOpRoot);
            this.graph = graph;
            this.name = name;
            this.op = op;
            this.target = target;
            type = returnType;
            erased = false;
            prev = this;
            next = this;
            sortKey = new SortKey(0);
            UpdateArgs(args);
        }

        public void Prepend(Node x)
        {
            Assert.IsTrue(graph == x.graph);
            if (this == x)
            {
                Debug.Log("Trying to prepend a node to itself. This behavior has no effect on the graph.");
                return;
            }

            x.RemoveFromList();
            var p = prev;
            p.next = x;
            x.prev = p;
            x.next = this;
            prev = x;

            var psk = x.prev.sortKey;
            var nsk = x.next.sortKey;

            if (psk.Length > nsk.Length)
            {
                x.sortKey = psk[..(nsk.Length + 1)];
                x.sortKey[^1] += 1;
            }
            else if (psk.Length < nsk.Length)
            {
                x.sortKey = nsk[..(psk.Length + 1)];
                x.sortKey[^1] -= 1;
            }
            else // same length, increase length by 1
            {
                x.sortKey = psk + 0;
            }
        }

        public int CompareTo(Node other)
        {
            return sortKey.CompareTo(other.sortKey);
        }

        public static bool operator >(Node left, Node right)
        {
            return left.sortKey.CompareTo(right.sortKey) > 0;
        }

        public static bool operator <(Node left, Node right)
        {
            return left.sortKey.CompareTo(right.sortKey) < 0;
        }

        public static bool operator >=(Node left, Node right)
        {
            return left > right || left == right;
        }

        public static bool operator <=(Node left, Node right)
        {
            return left < right || left == right;
        }

        public void Append(Node x)
        {
            next.Prepend(x);
        }

        public void RemoveFromList()
        {
            var p = prev;
            var n = next;
            p.next = n;
            n.prev = p;
        }

        public List<Node> AllInputNodes()
        {
            var nodeList = new List<Node>();
            foreach (var node in inputNodes)
                nodeList.Add(node);
            return nodeList;
        }

        public void UpdateArgs(Argument[] args)
        {
            foreach (var inputNode in inputNodes)
                inputNode.users.Remove(this);
            inputNodes.Clear();

            void Visit(Node node)
            {
                inputNodes.TryAdd(node);
                node.users.TryAdd(this);
            }

            GraphUtils.VisitArg(args, Visit);
            this.args = args;
        }

        public string FormatArg(Argument arg)
        {
            if (arg is null)
                return "null";
            if (arg.IsArguments)
            {
                var args = arg.AsArguments;
                var sb = new StringBuilder("[");
                for (var i = 0; i < args.Length; i++)
                {
                    if (i > 0)
                        sb.Append(", ");
                    sb.Append(FormatArg(args[i]));
                }

                sb.Append("]");
                return sb.ToString();
            }
            if (arg.IsNode)
                return "%" + arg.AsNode;
            return arg.ToString();
        }

        static readonly Dictionary<Type, string> k_TypeAliases = new()
        {
            { typeof(int), "int" },
            { typeof(float), "float" },
            { typeof(double), "double" },
            { typeof(bool), "bool" },
            { typeof(string), "string" },
            { typeof(object), "object" },
            { typeof(void), "void" },
            { typeof(byte), "byte" },
            { typeof(short), "short" },
            { typeof(long), "long" },
            { typeof(char), "char" },
        };

        public override string ToString()
        {
            return name;
        }

        public static string GetFriendlyName(Type type)
        {
            if (type.IsArray)
            {
                return $"{GetFriendlyName(type.GetElementType()!)}[]";
            }

            if (type.IsGenericType)
            {
                var sb = new StringBuilder();
                sb.Append(type.Name[..type.Name.IndexOf('`')]);
                sb.Append("<");
                var genericArguments = type.GetGenericArguments();
                for (var i = 0; i < genericArguments.Length; i++)
                {
                    if (i > 0)
                        sb.Append(", ");
                    sb.Append(GetFriendlyName(genericArguments[i]));
                }
                sb.Append(">");
                return sb.ToString();
            }

            return k_TypeAliases.TryGetValue(type, out var alias) ? alias : type.Name;
        }

        public string FormatNode(List<string> placeholderNames, ref string maybeReturnTypename)
        {
            var maybeTypename = type != null ? $"{GetFriendlyName(type)} " : "";
            switch (op)
            {
                case kOpPlaceholder:
                {
                    var argStr = target;
                    argStr += type != null ? $"{GetFriendlyName(type)}" : "";
                    if (placeholderNames != null)
                        placeholderNames.Add(argStr);
                    var defaultVal = args.Length > 0 ? $"(default={args[0]})" : "";
                    return $"%{name} : {maybeTypename}[num_users={users.Count}] = {op}[target={target}]{defaultVal}";
                }
                case kOpGetAttr:
                {
                    return $"%{name} : {maybeTypename}[num_users={users.Count}] = {op}[target={target}]";
                }
                case kOpOutput:
                {
                    maybeReturnTypename = maybeTypename;
                    return $"return {args[0]}";
                }
                default:
                {
                    return $"%{name} : {maybeTypename}[num_users={users.Count}] = {op}[target={target}](args = {FormatArg(args)})";
                }
            }
        }

        public List<Node> ReplaceAllUsesWith(Node replaceWith, Func<Node, bool> deleteUser = null, bool propagateMeta = false, bool propagatePartialTensor = true)
        {
            if (propagateMeta)
            {
                Assert.IsTrue(replaceWith.meta.Count == 0, "Called node.replace_all_uses_with(replace_with, propagate_meta=True), but replace_with already has .meta keys");
                foreach (var kvp in meta)
                {
                    replaceWith.meta[kvp.Key] = kvp.Value;
                }
            }

            if (propagatePartialTensor)
            {
                replaceWith.partialTensor = partialTensor;
            }

            var toProcess = new List<Node>();
            foreach (var n in users)
                toProcess.Add(n);
            var updatedNodes = new List<Node>();
            var skipped = new HashSet<Node>();
            foreach (var useNode in toProcess)
            {
                if (deleteUser is not null && !deleteUser(useNode))
                {
                    skipped.Add(useNode);
                    continue;
                }

                var newArgs = GraphUtils.MapArg(useNode.args, n => n == this ? replaceWith : n);
                useNode.UpdateArgs(newArgs);
                updatedNodes.Add(useNode);
            }
            Assert.IsTrue(users.Count == skipped.Count);
            return updatedNodes;
        }

        public bool IsImpure()
        {
            return op switch
            {
                kOpPlaceholder or kOpOutput => true,
                _ => false
            };
        }
    }
}
