using System;
using System.Collections.Generic;

namespace Unity.InferenceEngine.Graph
{
    // see https://github.com/pytorch/pytorch/blob/main/torch/fx/passes/utils/matcher_utils.py

    class InternalMatch
    {
        public List<Node> anchors;
        public Dictionary<Node, Node> nodesMap = new();
        public List<Node> placeholderNodes = new();
        public List<Node> returningNodes = new();
        public Dictionary<string, Node> nameNodeMap = new();

        public InternalMatch(List<Node> anchors)
        {
            this.anchors = anchors;
        }

        public InternalMatch ShallowCopy()
        {
            return new InternalMatch(new List<Node>(anchors))
            {
                nodesMap = new Dictionary<Node, Node>(nodesMap),
                placeholderNodes = new List<Node>(placeholderNodes),
                returningNodes = new List<Node>(returningNodes),
                nameNodeMap = new Dictionary<string, Node>(nameNodeMap)
            };
        }
    }

    class SubgraphMatcher
    {
        readonly Graph m_Pattern;
        readonly bool m_MatchOutput;
        readonly bool m_MatchPlaceholder;
        readonly bool m_RemoveOverlappingMatches;
        readonly bool m_IgnoreLiterals;

        readonly List<Node> m_PatternPlaceholderNodes;
        readonly List<Node> m_PatternReturningNodes;
        readonly List<Node> m_PatternAnchors;

        public SubgraphMatcher(Graph pattern, bool matchOutput = false, bool matchPlaceholder = false, bool removeOverlappingMatches = true, bool ignoreLiterals = false)
        {
            if (pattern.len == 0)
                throw new ArgumentException("SubgraphMatcher cannot be initialized with an empty pattern");

            m_Pattern = pattern;
            m_MatchOutput = matchOutput;
            m_MatchPlaceholder = matchPlaceholder;
            m_RemoveOverlappingMatches = removeOverlappingMatches;
            m_IgnoreLiterals = ignoreLiterals;

            foreach (var node in pattern.Nodes())
            {
                if (node.op != Node.kOpOutput && node.users.Count == 0)
                    throw new ArgumentException("Pattern graph contains dead code");
            }

            m_PatternPlaceholderNodes = pattern.FindNodes(Node.kOpPlaceholder);

            var outputNode = pattern.OutputNode();
            m_PatternReturningNodes = outputNode.AllInputNodes();

            m_PatternAnchors = new List<Node>();
            if (matchOutput)
            {
                m_PatternAnchors.Add(outputNode);
            }
            else
            {
                foreach (var node in m_PatternReturningNodes)
                    if (node.users.Count == 1)
                        m_PatternAnchors.Add(node);
            }
        }

        bool NodesAreEqual(Node pn, Node gn)
        {
            if (!m_MatchPlaceholder && pn.op == Node.kOpPlaceholder)
                return true;
            if (pn.op != gn.op)
                return false;
            if (pn.op == Node.kOpPlaceholder || pn.op == Node.kOpOutput)
                return true;
            if (pn.op == Node.kOpGetAttr)
                return MatchAttributes(pn, gn);
            return Equals(pn.target, gn.target);
        }

        bool MatchAttributes(Node pn, Node gn)
        {
            // this is non-standard, we allow small constants such as shapes of reshapes to be matched
            if (IsSmallConstant(pn))
                return IsSmallConstant(gn) && PartialTensor.IsEqual(pn.partialTensor, gn.partialTensor);
            if (IsSmallConstant(gn))
                return false;

            var pnValue = pn.graph.owningModule.attributes[pn.target];
            var gnValue = gn.graph.owningModule.attributes[gn.target];

            if (pnValue.GetType() != gnValue.GetType())
                return false;

            if (pnValue is ConstantTensor && gnValue is ConstantTensor)
                return true;

            throw new InvalidOperationException($"Unsupported attribute type: {pnValue.GetType()}");
        }

        public static bool IsSmallConstant(Node node)
        {
            return node.op == Node.kOpGetAttr && node.partialTensor.IsStatic();
        }

        bool IsContained(Dictionary<Node, Node> nodesMap)
        {
            var lookup = new Dictionary<Node, Node>();
            foreach (var (pn, gn) in nodesMap)
            {
                // small constants can be shared across subgraphs without a problem
                if (pn.op == Node.kOpPlaceholder || IsSmallConstant(pn))
                    continue;
                lookup[gn] = pn;
            }

            foreach (var (gn, pn) in lookup)
            {
                if (m_PatternReturningNodes.Contains(pn))
                    continue;

                foreach (var user in gn.users)
                {
                    if (!lookup.ContainsKey(user))
                        return false;
                }
            }

            return true;
        }

        static List<InternalMatch> RemoveOverlappingMatches(List<InternalMatch> matches)
        {
            var nonOverlapping = new List<InternalMatch>();
            var matchedNodes = new HashSet<Node>();

            foreach (var match in matches)
            {
                var isOverlapping = false;
                foreach (var kv in match.nodesMap)
                {
                    // small constants can be shared across subgraphs
                    if (kv.Key.op != Node.kOpPlaceholder && kv.Key.op != Node.kOpOutput && !IsSmallConstant(kv.Key) && matchedNodes.Contains(kv.Value))
                    {
                        isOverlapping = true;
                        break;
                    }
                }

                if (isOverlapping)
                    continue;

                nonOverlapping.Add(match);

                foreach (var kv in match.nodesMap)
                {
                    if (kv.Key.op != Node.kOpPlaceholder && kv.Key.op != Node.kOpOutput && !IsSmallConstant(kv.Key))
                        matchedNodes.Add(kv.Value);
                }
            }

            return nonOverlapping;
        }

        bool MatchArgs(Argument[] args1, Argument[] args2, ref InternalMatch match)
        {
            if (args1.Length != args2.Length)
                return false;

            for (var i = 0; i < args1.Length; i++)
            {
                if (!MatchArg(args1[i], args2[i], ref match))
                    return false;
            }

            return true;
        }

        bool MatchArg(Argument a1, Argument a2, ref InternalMatch match)
        {
            if (a1 is null && a2 is null)
                return true;
            if (a1 == null || a2 == null)
                return false;
            if (a1.Index != a2.Index)
                return false;
            if (a1.IsNode && a2.IsNode)
                return MatchNodes(a1.AsNode, a2.AsNode, ref match);
            if (a1.IsArguments && a2.IsArguments)
                return MatchArgs(a1.AsArguments, a2.AsArguments, ref match);
            return a1.Equals(a2) && !m_IgnoreLiterals;
        }

        bool MatchNodes(Node pn, Node gn, ref InternalMatch match)
        {
            if (match.nodesMap.TryGetValue(pn, out var value))
                return value == gn;
            if (match.nodesMap.ContainsValue(gn))
                return false;
            if (!NodesAreEqual(pn, gn))
                return false;
            var saved = match.ShallowCopy();
            match.nodesMap[pn] = gn;

            if (pn.op == Node.kOpPlaceholder)
                return true;

            if (!MatchArgs(pn.args, gn.args, ref match))
            {
                match = saved;
                return false;
            }

            return true;
        }

        public List<InternalMatch> Match(Graph graph)
        {
            var matchCandidatesList = new List<(Node, List<Node>)>();

            foreach (var patternAnchor in m_PatternAnchors)
            {
                var candidates = new List<Node>();
                foreach (var n in graph.Nodes())
                {
                    if (!NodesAreEqual(patternAnchor, n))
                        continue;
                    candidates.Add(n);
                }
                matchCandidatesList.Add((patternAnchor, candidates));
            }

            var matches = new List<InternalMatch>();

            void Backtrack(int anchorIndex, InternalMatch current)
            {
                if (anchorIndex == matchCandidatesList.Count)
                {
                    current.placeholderNodes = new List<Node>();
                    foreach (var p in m_PatternPlaceholderNodes)
                        current.placeholderNodes.Add(current.nodesMap[p]);

                    current.returningNodes = new List<Node>();
                    foreach (var p in m_PatternReturningNodes)
                        current.returningNodes.Add(current.nodesMap[p]);

                    matches.Add(current);
                    return;
                }

                var (patternAnchor, candidates) = matchCandidatesList[anchorIndex];
                var saved = current.ShallowCopy();

                foreach (var candidate in candidates)
                {
                    if (MatchNodes(patternAnchor, candidate, ref current))
                        Backtrack(anchorIndex + 1, current);

                    current = saved.ShallowCopy();
                }
            }

            var start = new InternalMatch(m_PatternAnchors);
            if (matchCandidatesList.Count > 0)
            {
                Backtrack(0, start);
            }

            for (var i = matches.Count - 1; i >= 0; i--)
            {
                if (IsContained(matches[i].nodesMap))
                    continue;
                matches.RemoveAt(i);
            }

            if (m_RemoveOverlappingMatches)
                matches = RemoveOverlappingMatches(matches);

            return matches;
        }
    }
}
