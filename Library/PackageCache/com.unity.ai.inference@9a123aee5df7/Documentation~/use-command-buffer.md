---
uid: sentis-use-command-buffer
---
## Use a command buffer

You can use a [command buffer](https://docs.unity3d.com/ScriptReference/Rendering.CommandBuffer.html) to create a queue of Sentis commands, which can then be run on the graphics processing unit (GPU) at a later time.

To use a command buffer, follow these steps:

1. Create or get an empty command buffer.
2. [Create a worker](xref:sentis-create-an-engine) with the [`BackendType.GPUCompute`](xref:Unity.InferenceEngine.BackendType.GPUCompute) backend type.
3. In a Unity event function, for example [`OnRenderImage`](xref:MonoBehaviour.OnRenderImage), add Sentis methods to the command buffer.
4. Use [`Graphics.ExecuteCommandBuffer`](xref:UnityEngine.Rendering.ScriptableRenderContext.ExecuteCommandBuffer(UnityEngine.Rendering.CommandBuffer)) to run the command buffer.

To add commands to the command buffer, do one of the following:

- Use a Sentis API that takes the command buffer as a parameter, for example [`TextureConverter.ToTensor`](xref:Unity.InferenceEngine.TextureConverter.ToTensor*).
- Use the [`ScheduleWorker`](xref:Unity.InferenceEngine.CommandBufferWorkerExtensions.ScheduleWorker*) method on the command buffer to add a command that runs the model.

For example, the following code creates and runs a command buffer queue. This queue first converts a render texture to an input tensor, then runs the model, and finally converts the output tensor back to a render texture.

```
// Create a worker that uses the GPUCompute backend type
worker = new Worker(ModelLoader.Load(modelAsset), BackendType.GPUCompute);

//...

// Add model execution command buffer that runs the model using the input tensor
commandBuffer.ScheduleWorker(worker, inputs);
```

Build the `CommandBuffer` once, update the input every frame, and run the `CommandBuffer`. This reduces the scheduling time by half.

```
Tensor<float> input0 = new Tensor<float>(new TensorShape(1));
Model model;
CommandBuffer cb;
Worker worker;

void Start()
{
    worker = new Worker(model, BackendType.GPUCompute);

    worker.SetInput("input0", input0);

    cb = new CommandBuffer();
    cb.ScheduleWorker(worker);
}

void Update()
{
    // modify input0
    input0.Upload(new float[] { Time.deltaTime });
    Graphics.ExecuteCommandBuffer(cb);
}
```

For more information, refer to the following:

- [Extending the Built-in Render Pipeline with CommandBuffers](https://docs.unity3d.com/Documentation/Manual/GraphicsCommandBuffers.html)
- The [Unity Discussions group for Sentis](https://discussions.unity.com/tag/sentis), which has a full sample project that uses command buffers.

## Additional resources

* [Get output from a model](xref:sentis-get-the-output)
* [Read output asynchronously](xref:sentis-read-output-async)
