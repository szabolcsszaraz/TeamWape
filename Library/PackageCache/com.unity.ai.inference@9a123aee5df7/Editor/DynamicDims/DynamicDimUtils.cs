using System.Collections.Generic;
using System.Linq;

namespace Unity.InferenceEngine.Editor.DynamicDims
{
    static class DynamicDimUtils
    {
        internal static void InitializeDynamicDimsConfig(this IDynamicDimImporter importer, Model model)
        {
            var newConfigs = model.symbolicDimNames
                .Distinct()
                .ToDictionary(name => name, name => new DynamicDimConfig { name = name, size = -1 });
            if (importer.dynamicDimConfigs != null)
            {
                foreach (var existingConfig in importer.dynamicDimConfigs)
                {
                    if (newConfigs.ContainsKey(existingConfig.name))
                    {
                        newConfigs[existingConfig.name] = existingConfig;
                    }
                }
            }
            importer.dynamicDimConfigs = newConfigs.Values.ToArray();
        }

        internal static void CleanModelDynamicDims(this IDynamicDimImporter importer, Model model)
        {
            if(importer.dynamicDimConfigs == null || importer.dynamicDimConfigs.Length == 0)
                return;

            var configuredDimNames = new HashSet<string>(
                importer.dynamicDimConfigs
                    .Where(d => d.size != -1)
                    .Select(d => d.name)
            );

            var originalSymbolicDimNames = model.symbolicDimNames;
            model.symbolicDimNames = model.symbolicDimNames
                .Where(name => !configuredDimNames.Contains(name))
                .ToArray();

            var indexMapping = new Dictionary<int, int>();
            var newIndex = 0;
            for (var oldIndex = 0; oldIndex < originalSymbolicDimNames.Length; oldIndex++)
            {
                if (!configuredDimNames.Contains(originalSymbolicDimNames[oldIndex]))
                {
                    indexMapping[oldIndex] = newIndex;
                    newIndex++;
                }
            }

            for (var inputIdx = 0; inputIdx < model.inputs.Count; inputIdx++)
            {
                var input = model.inputs[inputIdx];
                var shape = input.shape;
                var shapeModified = false;

                for (var k = 0; k < shape.rank; k++)
                {
                    if (shape[k].isParam)
                    {
                        var oldParamIndex = shape[k].param;
                        if (oldParamIndex < originalSymbolicDimNames.Length &&
                            !configuredDimNames.Contains(originalSymbolicDimNames[oldParamIndex]))
                        {
                            if (indexMapping.TryGetValue(oldParamIndex, out var newParamIndex))
                            {
                                shape[k] = DynamicTensorDim.Param((byte)newParamIndex);
                                shapeModified = true;
                            }
                        }
                    }
                }

                if (shapeModified)
                {
                    var newInput = input;
                    newInput.shape = shape;
                    model.inputs[inputIdx] = newInput;
                }
            }
        }

        internal static void ApplyDynamicDimConfigs(this IDynamicDimImporter importer, Model model)
        {
            var dynamicDimConfigs = importer.dynamicDimConfigs;

            if (dynamicDimConfigs == null || dynamicDimConfigs.Length == 0)
                return;

            var dimConfigLookup = new Dictionary<string, int>();
            foreach (var config in dynamicDimConfigs)
            {
                if (config.size == -1 || string.IsNullOrEmpty(config.name))
                    continue;

                if (!dimConfigLookup.TryAdd(config.name, config.size))
                    Debug.LogWarning($"Static size provided multiple times for dynamic dimension {config.name}.");
            }

            if (dimConfigLookup.Count == 0)
                return;

            ApplyConfigsToAllInputs(model, dimConfigLookup);

        }

        static void ApplyConfigsToAllInputs(Model model, Dictionary<string, int> dimConfigLookup)
        {
            for (var inputIdx = 0; inputIdx < model.inputs.Count; inputIdx++)
            {
                var input = model.inputs[inputIdx];
                if (ApplyConfigsToInputShape(input.shape, model.symbolicDimNames, dimConfigLookup, out var modifiedShape))
                {
                    model.inputs[inputIdx] = new Model.Input(input.name, input.index, input.dataType, modifiedShape);
                }
            }
        }

        static bool ApplyConfigsToInputShape(DynamicTensorShape shape, string[] symbolicDimNames, Dictionary<string, int> dimConfigLookup, out DynamicTensorShape modifiedShape)
        {
            var shapeModified = false;
            modifiedShape = shape;

            for (var dimIdx = 0; dimIdx < shape.rank; dimIdx++)
            {
                var dim = shape[dimIdx];

                // Skip non-parametric dimensions
                if (!dim.isParam)
                    continue;

                // Skip if parameter index is out of bounds
                if (symbolicDimNames == null || dim.param >= symbolicDimNames.Length)
                    continue;

                var dimName = symbolicDimNames[dim.param];
                if (dimConfigLookup.TryGetValue(dimName, out var staticSize))
                {
                    modifiedShape[dimIdx] = DynamicTensorDim.Int(staticSize);
                    shapeModified = true;
                }
            }

            return shapeModified;
        }
    }
}
