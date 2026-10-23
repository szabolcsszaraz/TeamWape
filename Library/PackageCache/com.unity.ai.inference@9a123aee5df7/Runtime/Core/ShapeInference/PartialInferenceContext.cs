using System;
using System.Collections.Generic;

namespace Unity.InferenceEngine
{
    /// <summary>
    /// Represents a dictionary of partial tensors during partial tensor inference.
    /// </summary>
    class PartialInferenceContext
    {
        internal Dictionary<int, PartialTensor> m_PartialTensors;

        /// <summary>
        /// Initializes and returns an empty partial inference context.
        /// </summary>
        public PartialInferenceContext()
        {
            m_PartialTensors = new Dictionary<int, PartialTensor>();
        }

        /// <summary>
        /// Add partial tensor with a given index to context.
        /// </summary>
        public void AddPartialTensor(int index, PartialTensor partialTensor)
        {
            if (index == -1)
                return;
            if (m_PartialTensors.TryGetValue(index, out var prevTensor))
                partialTensor = PartialTensor.MaxDefinedPartialTensor(partialTensor, prevTensor);
            m_PartialTensors[index] = partialTensor;
        }

        /// <summary>
        /// Returns partial tensor from index.
        /// </summary>
        public PartialTensor GetPartialTensor(int index)
        {
            if (index == -1)
                return null;

            return m_PartialTensors[index];
        }
    }
}
