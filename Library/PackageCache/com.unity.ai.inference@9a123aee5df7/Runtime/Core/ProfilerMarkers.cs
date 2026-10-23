using System;
using Unity.Profiling;

namespace Unity.InferenceEngine
{
    static class ProfilerMarkers
    {
        public static readonly ProfilerMarker Schedule = new("Sentis.Schedule");
        public static readonly ProfilerMarker LoadComputeShader = new("Sentis.ComputeShader.Load");
        public static readonly ProfilerMarker LoadPixelShader = new("Sentis.PixelShader.Load");
        public static readonly ProfilerMarker ComputeTensorDataDownload = new("Sentis.ComputeTensorData.DownloadDataFromGPU");
        public static readonly ProfilerMarker TextureTensorDataDownload = new("Sentis.TextureTensorData.DownloadDataFromGPU");
        public static readonly ProfilerMarker TensorDataPoolAdopt = new("Sentis.TensorDataPool.Adopt");
        public static readonly ProfilerMarker TensorDataPoolRelease = new("Sentis.TensorDataPool.Release");
        public static readonly ProfilerMarker InferModelPartialTensors = new("Sentis.Compiler.Analyser.ShapeInferenceAnalysis.InferModelPartialTensors");
        public static readonly ProfilerMarker LoadModelDesc = new("Sentis.ModelLoader.LoadModelDesc");
        public static readonly ProfilerMarker LoadModelWeights = new("Sentis.ModelLoader.LoadModelWeights");
        public static readonly ProfilerMarker SaveModelDesc = new("Sentis.ModelLoader.SaveModelDesc");
        public static readonly ProfilerMarker SaveModelWeights = new("Sentis.ModelLoader.SaveModelWeights");
        public static readonly ProfilerMarker ComputeTensorDataNewEmpty = new("Sentis.ComputeTensorData.NewEmpty");
        public static readonly ProfilerMarker ComputeTensorDataNewArray = new("Sentis.ComputeTensorData.NewArray");
    }
}
