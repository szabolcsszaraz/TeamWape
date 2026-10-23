using System;

namespace Unity.InferenceEngine.Editor.Visualizer.LayerAnalysis
{
    static class LayerCategorizer
    {
        // Category definitions
        const int k_CategoryActivationMath = 0;
        const int k_CategoryConvolutionPooling = 1;
        const int k_CategoryTransformationDimension = 2;
        const int k_CategoryUncategorized = -1;

        public static int GetCategoryForLayer(Layer layer)
        {
            return layer.category switch
            {
                "Activation" => k_CategoryActivationMath,
                "ActivationNonLinear" => k_CategoryActivationMath,
                "Convolution" => k_CategoryConvolutionPooling,
                "Dimension" => k_CategoryTransformationDimension,
                "Generator" => k_CategoryTransformationDimension,
                "Indexing" => k_CategoryTransformationDimension,
                "Logical" => k_CategoryActivationMath,
                "Math" => k_CategoryActivationMath,
                "Normalization" => k_CategoryConvolutionPooling,
                "ObjectDetection" => k_CategoryConvolutionPooling,
                "Pooling" => k_CategoryConvolutionPooling,
                "Quantization" => k_CategoryActivationMath,
                "Random" => k_CategoryActivationMath,
                "Recurrent" => k_CategoryConvolutionPooling,
                "Reduction" => k_CategoryConvolutionPooling,
                "Spectral" => k_CategoryConvolutionPooling,
                "Transformation" => k_CategoryTransformationDimension,
                "Trigonometric" => k_CategoryActivationMath,
                _ => k_CategoryUncategorized
            };
        }
    }
}
