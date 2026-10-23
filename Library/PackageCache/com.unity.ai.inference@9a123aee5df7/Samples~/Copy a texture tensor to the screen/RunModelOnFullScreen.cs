using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.InferenceEngine;

public class RunModelOnFullScreen : MonoBehaviour
{
    Model m_Model;
    Worker m_Worker;
    Tensor<float> m_TensorY;
    Tensor[] m_Inputs;

    void OnEnable()
    {
        Debug.Log("When running this example, the game view should fade from black to white and back.");
        var graph = new FunctionalGraph();
        var x = graph.AddInput<float>(new DynamicTensorShape(1, 4, -1, -1));
        var y = graph.AddInput<float>(new TensorShape());
        graph.AddOutput(x + y);
        m_Model = graph.Compile();
        m_Worker = new Worker(m_Model, BackendType.GPUCompute);

        // The value of Y will be added to all pixel values.
        m_TensorY = new Tensor<float>(new TensorShape(), new[] { 0.1f });

        m_Inputs = new Tensor[] { null, m_TensorY };

        // When using Built-in Render Pipeline, Unity calls OnRenderImage after the camera finished rendering.
        // When using Universal Render Pipeline or High Definition Render Pipeline the game view can be captured in a RenderPipelineManager.endContextRendering callback.
        if (GraphicsSettings.currentRenderPipeline != null)
            RenderPipelineManager.endContextRendering += OnEndContextRendering;
    }

    void OnDisable()
    {
        foreach (var tensor in m_Inputs) { tensor?.Dispose(); }

        m_Worker.Dispose();

        if (GraphicsSettings.currentRenderPipeline != null)
            RenderPipelineManager.endContextRendering -= OnEndContextRendering;
    }

    void OnDestroy()
    {
        if (GraphicsSettings.currentRenderPipeline != null)
            RenderPipelineManager.endContextRendering -= OnEndContextRendering;
    }

    void Update()
    {
        // readback tensor from GPU to CPU to use tensor indexing
        CPUTensorData.Pin(m_TensorY);
        m_TensorY[0] = Mathf.Sin(Time.timeSinceLevelLoad);
    }

    void RunModelAndRenderToScreen(RenderTexture source)
    {
        // ToTensor takes optional parameters for resampling, channel swizzles etc
        using Tensor<float> frameInputTensor = new Tensor<float>(new TensorShape(1, 4, source.height, source.width));
        TextureConverter.ToTensor(source, frameInputTensor);

        m_Inputs[0] = frameInputTensor;

        m_Worker.Schedule(m_Inputs);
        Tensor<float> output = m_Worker.PeekOutput() as Tensor<float>;

        // TextureConverter also has methods for rendering to a render texture with optional parameters
        TextureConverter.RenderToScreen(output);
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        RunModelAndRenderToScreen(source);
        Graphics.SetRenderTarget(destination);
    }

    void OnEndContextRendering(ScriptableRenderContext context, List<Camera> cameras)
    {
        // Blit contents of the game view to a render texture.
        RenderTexture source = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
        CommandBuffer commandBuffer = new CommandBuffer();
        commandBuffer.Blit(BuiltinRenderTextureType.CurrentActive, source);
        context.ExecuteCommandBuffer(commandBuffer);
        context.Submit();
        commandBuffer.Dispose();

        RunModelAndRenderToScreen(source);
        RenderTexture.ReleaseTemporary(source);
    }
}
