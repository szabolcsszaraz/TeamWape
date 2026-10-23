using System.Collections.Generic;
using UnityEngine.Assertions;

namespace Unity.InferenceEngine.Graph
{
    /// <summary>
    /// Represents an object that tracks, creates and assigns unique names of nodes in a graph.
    /// </summary>
    class GraphNamespace
    {
        Dictionary<object, string> m_ObjToName = new();
        HashSet<string> m_UsedNames = new();
        Dictionary<string, int> m_PrefixCount = new();

        public string CreateName(string candidate, object obj)
        {
            if (obj != null && m_ObjToName.TryGetValue(obj, out var objName))
                return objName;

            var prefix = candidate;
            var number = 0;

            // check if the candidate is of the form X_N where N is a sequence of digits
            var separatorIndex = candidate.LastIndexOf('_');
            if (separatorIndex >= 1)
            {
                if (int.TryParse(candidate.Substring(separatorIndex + 1), out var n))
                {
                    number = n;
                    prefix = candidate.Substring(0, separatorIndex);
                }
            }

            if (prefix == candidate || m_UsedNames.Contains(candidate))
            {
                number = m_PrefixCount.GetValueOrDefault(candidate, 0);
            }

            while (m_UsedNames.Contains(candidate))
            {
                number++;
                candidate = $"{prefix}_{number}";
            }

            m_UsedNames.Add(candidate);
            m_PrefixCount[prefix] = number;
            if (obj != null)
                m_ObjToName[obj] = candidate;
            return candidate;
        }

        public void AssociateNameWithObj(string name, Node node)
        {
            if (m_ObjToName.TryGetValue(node, out var existingName))
            {
                Assert.IsTrue(name == existingName);
                return;
            }

            m_ObjToName[node] = name;
        }
    }
}
