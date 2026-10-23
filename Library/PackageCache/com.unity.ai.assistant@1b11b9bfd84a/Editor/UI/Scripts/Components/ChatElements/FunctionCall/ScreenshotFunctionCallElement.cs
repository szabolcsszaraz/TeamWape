using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Tools.Editor;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    [FunctionCallRenderer(typeof(MultiAngleSceneViewTool), nameof(MultiAngleSceneViewTool.CaptureMultiAngleSceneView))]
    class MultiAngleSceneViewFunctionCallElement : ImagePreviewFunctionCallElementBase
    {
        public override string Title => "Capture Multi-Angle Scene View";
        protected override string LoadingMessage => "Loading multi-angle scene view...";
        protected override string ErrorPrefix => "Multi-angle scene view capture failed: ";
        protected override string LoadFailedMessage => "Failed to load multi-angle scene view.";
    }

    [FunctionCallRenderer(typeof(AssetTools), nameof(AssetTools.GetImageAssetContent))]
    class ImageAssetContentFunctionCallElement : ImagePreviewFunctionCallElementBase
    {
        public override string Title => "Get Image Asset Content";
        protected override string LoadingMessage => "Loading image asset...";
        protected override string ErrorPrefix => "Failed to get image asset content: ";
        protected override string LoadFailedMessage => "Failed to load image asset.";
    }

    [FunctionCallRenderer(typeof(Capture2DSceneTools), nameof(Capture2DSceneTools.Capture2DScene))]
    class Capture2DSceneFunctionCallElement : ImagePreviewFunctionCallElementBase
    {
        public override string Title => "Capture 2D Scene";
        protected override string LoadingMessage => "Loading 2D scene capture...";
        protected override string ErrorPrefix => "2D scene capture failed: ";
        protected override string LoadFailedMessage => "Failed to load 2D scene capture.";
    }
}
