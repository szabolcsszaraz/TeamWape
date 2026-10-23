using System.Collections.Generic;
using System.Linq;
using Unity.InferenceEngine.Graph;
using UnityEngine;

namespace Unity.InferenceEngine.Compiler.Passes.Cleanup
{
    /// <summary>
    /// Removes nodes which do not contribute to the calculation of an output.
    /// </summary>
    class RemoveUnusedPass : GraphPass
    {
        public override void Run(GraphModule gm)
        {
            gm.graph.EliminateDeadCode();
            var usedAttributes = new HashSet<string>();
            foreach (var node in gm.graph.Nodes())
            {
                if (node.op == Node.kOpGetAttr)
                    usedAttributes.Add(node.target);
            }

            foreach (var key in gm.attributes.Keys.ToList())
            {
                if (!usedAttributes.Contains(key))
                    gm.attributes.Remove(key);
            }
        }
    }
}
