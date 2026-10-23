using System;

namespace Unity.InferenceEngine.Editor.DynamicDims
{
    interface IDynamicDimImporter
    {
        internal DynamicDimConfig[] dynamicDimConfigs { get; set; }
    }
}
