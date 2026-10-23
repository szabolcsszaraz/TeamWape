using System;

namespace Unity.InferenceEngine.Editor.Visualizer.Extensions
{
    static class ModelExtensions
    {
        public static bool IsConstant(this Model model, int tensorIndex)
        {
            for (var i = 0; i < model.constants.Count; ++i)
            {
                if (model.constants[i].index == tensorIndex)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
