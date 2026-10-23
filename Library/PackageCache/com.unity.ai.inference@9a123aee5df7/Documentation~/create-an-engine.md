---
uid: sentis-create-an-engine
---
# Create an engine to run a model

To run a model, you need to create a worker. A worker is the engine that breaks the model down into runnable tasks. It schedules the tasks to run on a backend, such as the graphics processing unit (GPU) or central processing unit (CPU).

## Create a Worker

Use [`new Worker(...)`](xref:Unity.InferenceEngine.Worker#constructors) to create a worker. You must specify a backend type, which tells Sentis where to run the worker and a [runtime model](xref:sentis-import-a-model-file#create-a-runtime-model).

For example, the following code creates a worker that runs on the GPU with Sentis compute shaders.

```
using UnityEngine;
using Unity.InferenceEngine;

public class CreateWorker : MonoBehaviour
{
    ModelAsset modelAsset;
    Model runtimeModel;
    Worker worker;

    void Start()
    {
        runtimeModel = ModelLoader.Load(modelAsset);
        worker = new Worker(runtimeModel, BackendType.GPUCompute);
    }
}
```

## Backend types

Sentis provides CPU and GPU backend types. To understand how Sentis runs operations with the different backends, refer to [How Sentis runs a model](xref:sentis-how-sentis-runs-a-model).

If a backend type doesn't support a Sentis layer in a model, the worker will assert. For more information, refer to [Supported ONNX operators](xref:sentis-supported-operators), [Supported LiteRT operators](xref:sentis-supported-litert-operators), and [Supported PyTorch operators](xref:sentis-supported-torch-export-operators).

| BackendType | Usage |
| ----------- | ----- |
| [`BackendType.CPU`](xref:Unity.InferenceEngine.BackendType.CPU)               | - Faster than GPU for small models or when inputs/outputs are on the CPU.<br>- On WebGL, Burst compiles to WebAssembly, which may result in slower performance. For more information, refer to [Getting started with WebGL development](https://docs.unity3d.com/Documentation/Manual/webgl-gettingstarted.html).  |
| [`BackendType.GPUCompute`](xref:Unity.InferenceEngine.BackendType.GPUCompute) | - Generally the fastest backend for most models.<br>- Avoids expensive data transfer when outputs remain on the GPU.<br>- Uses [DirectML](https://learn.microsoft.com/en-us/windows/ai/directml/dml) for inference acceleration when running on DirectX12-supported platforms. For more information, refer to [Supported ONNX operators](xref:sentis-supported-operators). |
| [`BackendType.GPUPixel`](xref:Unity.InferenceEngine.BackendType.GPUPixel)     | - Use only on platforms that lack compute shader support.<br>- Check platform support using [SystemInfo.supportsComputeShaders](xref:UnityEngine.SystemInfo.supportsComputeShaders). |

The speed of model performance depends on the platform's support for multithreading in Burst, its full support for compute shaders,
and the resource usage of the game or application.

To understand a model's performance, it’s important to [Profile a model](xref:sentis-profile-a-model).

## Additional resources

- [Create a runtime model](xref:sentis-import-a-model-file#create-a-runtime-model)
- [How Sentis runs a model](xref:sentis-how-sentis-runs-a-model)
- [Supported ONNX operators](xref:sentis-supported-operators)
- [Supported LiteRT operators](xref:sentis-supported-litert-operators)
- [Supported PyTorch operators](xref:sentis-supported-torch-export-operators)
- [Run a model](xref:sentis-run-a-model)
