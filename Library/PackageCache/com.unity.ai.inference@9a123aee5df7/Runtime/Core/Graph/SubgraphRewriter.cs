using System;
using System.Collections.Generic;

namespace Unity.InferenceEngine.Graph
{
    class ReplacedPatterns
    {
        public Node anchor;
        public Dictionary<Node, Node> nodesMap;
        public List<Node> replacements;
    }

    /// <summary>
    /// Replaces subgraphs of a graph that match a pattern.
    /// See https://github.com/pytorch/pytorch/blob/main/torch/fx/subgraph_rewriter.py
    /// </summary>
    static class SubgraphRewriter
    {
        public static List<ReplacedPatterns> ReplacePattern(GraphModule gm, object pattern, object replacement = null, List<Func<InternalMatch, Graph, Graph, bool>> matchFilters = null, bool ignoreLiterals = false, Func<InternalMatch, Graph, Graph, Graph> replacementCallback = null)
        {
            if (matchFilters == null)
                matchFilters = new List<Func<InternalMatch, Graph, Graph, bool>>();

            var originalGraph = gm.graph;

            var patternGraph = pattern switch
            {
                GraphModule pgm => pgm.graph,
                Graph pg => pg,
                _ => throw new ArgumentException("Unsupported pattern type")
            };

            var matcher = new SubgraphMatcher(
                patternGraph,
                matchOutput: false,
                matchPlaceholder: false,
                removeOverlappingMatches: true,
                ignoreLiterals: ignoreLiterals
            );

            var matches = matcher.Match(originalGraph);

            // Filter matches
            for (var i = matches.Count - 1; i >= 0; i--)
            {
                var m = matches[i];
                var nonMatch = false;
                foreach (var filter in matchFilters)
                {
                    if (filter(m, originalGraph, patternGraph))
                        continue;
                    nonMatch = true;
                    break;
                }
                if (nonMatch)
                    matches.RemoveAt(i);
            }

            var commonReplacementGraph = replacement switch
            {
                null => null,
                GraphModule rgm => rgm.graph,
                Graph rg => rg,
                _ => throw new ArgumentException("Unsupported replacement type")
            };

            Dictionary<Node, Node> matchChangedNode = new();
            var matchAndReplacements = new List<ReplacedPatterns>();

            foreach (var match in matches)
            {
                var replacementGraph = replacementCallback != null ? replacementCallback(match, originalGraph, patternGraph) : commonReplacementGraph;

                var replacementPlaceholders = replacementGraph.FindNodes(Node.kOpPlaceholder);

                Logger.AssertIsTrue(match.placeholderNodes.Count == replacementPlaceholders.Count, "");

                var valMap = new Dictionary<Node, Node>();
                for (var i = 0; i < replacementPlaceholders.Count; i++)
                {
                    var rn = replacementPlaceholders[i];
                    var gn = match.placeholderNodes[i];
                    if (gn is Node gnNode)
                    {
                        valMap[rn] = matchChangedNode.TryGetValue(gnNode, out var newNode) ? newNode : gnNode;
                        if (!Equals(gnNode, valMap[rn]))
                        {
                            var index = match.placeholderNodes.IndexOf(gnNode);
                            match.placeholderNodes[index] = matchChangedNode[gnNode];
                            Node key = null;
                            foreach (var kvp in match.nodesMap)
                            {
                                if (kvp.Value == gnNode)
                                {
                                    key = kvp.Key;
                                    break;
                                }
                            }
                            match.nodesMap[key] = matchChangedNode[gnNode];
                        }
                    }
                    else
                    {
                        valMap[rn] = gn;
                    }
                }

                // Find insert point
                var userNodes = new HashSet<Node>();
                foreach (var n in match.returningNodes)
                foreach (var u in n.users)
                    userNodes.Add(u);

                Node firstUserNode = null;
                if (userNodes.Count == 1)
                {
                    foreach (var n in userNodes)
                    {
                        firstUserNode = n;
                        break;
                    }
                }
                else if (userNodes.Count > 1)
                {
                    foreach (var n in originalGraph.Nodes())
                    {
                        if (userNodes.Contains(n))
                        {
                            firstUserNode = n;
                            break;
                        }
                    }
                }

                Node firstNextNode = null;
                if (firstUserNode == null)
                {
                    Node next = null;
                    foreach (var n in originalGraph.Nodes().GetReversed())
                    {
                        if (match.returningNodes.Contains(n))
                        {
                            firstNextNode = next;
                            break;
                        }
                        next = n;
                    }
                }

                var insertPoint = firstUserNode ?? firstNextNode;

                Logger.AssertIsTrue(insertPoint != null, "Insert point cannot be null");

                Argument[] copiedReturningNodes;
                using (originalGraph.InsertingBefore(insertPoint))
                {
                    copiedReturningNodes = originalGraph.GraphCopy(replacementGraph, valMap).AsArguments;
                }

                var replacementNodes = new List<Node>();
                foreach (var v in valMap.Values)
                {
                    if (!match.placeholderNodes.Contains(v))
                        replacementNodes.Add(v);
                }

                Logger.AssertIsTrue(match.returningNodes.Count == copiedReturningNodes.Length, "");

                for (var i = 0; i < match.returningNodes.Count; i++)
                {
                    var gn = match.returningNodes[i];
                    var copied = copiedReturningNodes[i].AsNode;
                    gn.ReplaceAllUsesWith(copied);
                    matchChangedNode[gn] = copied;
                }

                foreach (var node in patternGraph.Nodes().GetReversed())
                {
                    // don't delete small constants such as reshape shapes as they can be shared across subgraphs
                    if (node.op != Node.kOpPlaceholder && node.op != Node.kOpOutput && !SubgraphMatcher.IsSmallConstant(node))
                    {
                        var gn = match.nodesMap[node];
                        gm.graph.EraseNode(gn);
                    }
                }

                matchAndReplacements.Add(new ReplacedPatterns
                {
                    anchor = match.anchors[0],
                    nodesMap = match.nodesMap,
                    replacements = replacementNodes
                });
            }

            return matchAndReplacements;
        }
    }
}
