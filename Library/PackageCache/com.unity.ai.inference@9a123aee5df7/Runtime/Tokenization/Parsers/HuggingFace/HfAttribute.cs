using System;
using JetBrains.Annotations;
using UnityEngine.Scripting;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace
{
    /// <summary>
    /// Base type for Hugging Face component parsing attributes.
    /// </summary>
    public abstract class HfAttribute : PreserveAttribute
    {
    }
}
