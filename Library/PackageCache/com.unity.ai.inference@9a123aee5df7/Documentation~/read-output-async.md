---
uid: sentis-read-output-async
---
# Read output from a model asynchronously

After you schedule a model and access an output tensor from [`PeekOutput`](xref:Unity.InferenceEngine.Worker.PeekOutput*), the following are true:
- Sentis might not have finished calculating the final tensor data, so there's pending scheduled work.
- If you use a graphics processing unit (GPU) backend, the calculated tensor data might be on the GPU. This requires a read back to copy the data to the central processing unit (CPU) in a readable format.

If either of these conditions is true, [`ReadbackAndClone`](xref:Unity.InferenceEngine.Tensor.ReadbackAndClone*) or [`CompleteAllPendingOperations`](xref:Unity.InferenceEngine.TextureTensorData.CompleteAllPendingOperations*) methods block the main thread until the operations are complete.

To avoid this, follow these two methods to use asynchronous readback:

1. Use the awaitable [`ReadbackAndCloneAsync`](xref:Unity.InferenceEngine.Tensor.ReadbackAndCloneAsync*) method. Sentis returns a CPU copy of the input tensor in a non blocking way.

```
using Unity.InferenceEngine;
using UnityEngine;

public class AsyncReadbackCompute : MonoBehaviour
{
    [SerializeField]
    ModelAsset modelAsset;

    Tensor<float> m_Input;
    Worker m_Worker;

    async void OnEnable()
    {
        var model = ModelLoader.Load(modelAsset);
        m_Input = new Tensor<float>(new TensorShape(1, 1), new[] { 43.0f });
        m_Worker = new Worker(model, BackendType.GPUCompute);
        m_Worker.Schedule(m_Input);

        // Peek the value from Sentis, without taking ownership of the tensor
        var outputTensor = m_Worker.PeekOutput() as Tensor<float>;
        var cpuCopyTensor = await outputTensor.ReadbackAndCloneAsync();

        Debug.Assert(cpuCopyTensor[0] == 42);
        Debug.Log($"Output tensor value {cpuCopyTensor[0]}");
        cpuCopyTensor.Dispose();
    }

    void OnDisable()
    {
        m_Input.Dispose();
        m_Worker.Dispose();
    }
}
```

2. Use a polling mechanism with the [`ReadbackRequest`](xref:Unity.InferenceEngine.Tensor.ReadbackRequest*) and [`Tensor.IsReadbackRequestDone`](xref:Unity.InferenceEngine.Tensor.IsReadbackRequestDone*) methods.

```
bool inferencePending = false;
Tensor<float> outputTensor;

void OnUpdate()
{
    if (!inferencePending)
    {
        m_Worker.Schedule(m_Input);
        outputTensor = m_Worker.PeekOutput() as Tensor<float>;

        // Trigger a non-blocking readback request
        outputTensor.ReadbackRequest();
        inferencePending = true;
    }
    else if (inferencePending && outputTensor.IsReadbackRequestDone())
    {
        // m_Output is now downloaded to the cpu. Using ReadbackAndClone or ToReadOnlyArray will not be blocking
        var array = outputTensor.DownloadToArray();
        inferencePending = false;
    }
}
```
3. Use an awaitable with a callback.
```
bool inferencePending = false;

void Update()
{
    if (!inferencePending)
    {
        m_Worker.Schedule(m_Input);
        var outputTensor = m_Worker.PeekOutput() as Tensor<float>;
        inferencePending = true;

        var awaiter = outputTensor.ReadbackAndCloneAsync().GetAwaiter();
        awaiter.OnCompleted(() =>
        {
            var tensorOut = awaiter.GetResult();
            inferencePending = false;
            tensorOut.Dispose();
        });
    }
}
```

> [!NOTE]
> To avoid a Tensor data mutation to a CPU tensor from calling [`ReadbackAndClone`](xref:Unity.InferenceEngine.Tensor.ReadbackAndClone), call [`tensor.dataOnBackend.Download`](xref:Unity.InferenceEngine.ITensorData.Download*) to get the data directly. This keeps the [`tensor.dataOnBackend`](xref:Unity.InferenceEngine.Tensor.dataOnBackend) on the given backend while providing a CPU copy. Be cautious with synchronization issues: if you re-schedule a worker, make a new download request.

For an example, refer to the `Read output asynchronously` example in the [sample scripts](xref:sentis-package-samples).

## Additional resources

- [Tensor fundamentals](xref:sentis-tensor-fundamentals)
- [Use output data](xref:sentis-use-model-output)
- [Get output from a model](xref:sentis-get-the-output)
