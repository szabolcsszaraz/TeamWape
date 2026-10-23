---
uid: sentis-access-tensor-data-directly
---
# Access tensor data directly

To avoid slow readbacks when you access a tensor or pass it between models, read from and write to the tensor’s underlying native data directly.

For more information about how Sentis stores tensor data, refer to [Tensor fundamentals in Sentis](xref:sentis-tensor-fundamentals#memory-location).

## Check where tensor data is stored

Use the [`dataOnBackend.backendType`](xref:Unity.InferenceEngine.ITensorData.backendType) property to determine whether the tensor data is stored in:
* [`BackendType.CPU`](xref:Unity.InferenceEngine.BackendType.CPU)
* [`BackendType.GPUCompute`](xref:Unity.InferenceEngine.BackendType.GPUCompute)
* [`BackendType.GPUPixel`](xref:Unity.InferenceEngine.BackendType.GPUPixel)

For example:

```
using UnityEngine;
using Unity.InferenceEngine;

public class CheckTensorLocation : MonoBehaviour
{
    public Texture2D inputTexture;

    void Start()
    {
        // Create input data as a tensor
        Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 4, inputTexture.height, inputTexture.width));
        TextureConverter.ToTensor(inputTexture, inputTensor);

        // Check if the tensor is stored in CPU or GPU memory, and write to the Console window.
        Debug.Log(inputTensor.dataOnBackend.backendType);
    }
}
```

If you want to force a tensor to the other device, use the following:

- [`ComputeTensorData.Pin`](xref:Unity.InferenceEngine.ComputeTensorData.Pin*) to force a tensor into graphics processing unit (GPU) compute shader memory in a [`ComputeBuffer`](xref:UnityEngine.ComputeBuffer).
- [`CPUTensorData.Pin`](xref:Unity.InferenceEngine.CPUTensorData.Pin*) to force a tensor into CPU memory.

For example:

```
// Create a tensor
Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 3, 2, 2));

// Force the tensor into GPU memory
ComputeTensorData computeTensorData = ComputeTensorData.Pin(inputTensor);
```

> [!NOTE]
> If the tensor data already exists on the target device, the method passes it through without changes. Otherwise, it disposes of the existing data and allocates new memory on the selected backend.

## Access CPU data directly

When the tensor is on the CPU and all operations that depend on it are complete, the tensor becomes readable and writable.

You can use indexers to manipulate the tensor data.

```
var tensor = new Tensor<float>(new TensorShape(1, 2, 3));
//...
if (tensor.backendType == BackendType.CPU && tensor.IsReadbackRequestDone()) {
    // tensor is read-writable directly
    tensor[0, 1, 0] = 1f;
    tensor[0, 1, 1] = 2f;
    tensor[0, 1, 2] = 3f;
    float val = tensor[0, 0, 2];
}
```

You can also get a readable flattened-version of the tensor as a span or `NativeArray`. The data will be row major flattened memory layout of the Tensor.

```
var tensor = new Tensor<float>(new TensorShape(1, 2, 3));
//...
if (tensor.backendType == BackendType.CPU && tensor.IsReadbackRequestDone()) {
    // tensor is readable
    var nativeArray = tensor.AsReadOnlyNativeArray();
    float val010 = nativeArray[3 + 0];
    float val011 = nativeArray[3 + 1];
    float val012 = nativeArray[3 + 2];

    var span = tensor.AsReadOnlySpan();
    float val002 = span[2];
}
```

## Upload data directly to backend memory

Use [`Upload`](xref:Unity.InferenceEngine.Tensor`1.Upload*) to upload data directly to the tensor.

```
var tensor = new Tensor<float>(new TensorShape(1,2,3), new [] { 0f, 1f, 2f, 3f, 4f, 5f });
tensor.Upload(new [] { 6f, 7f, 8f });
// tensor dataOnBackend now contains {6,7,8,3,4,5}
```
This method works for all tensor data backends but might be a blocking call. If the tensor data is on the central processing unit (CPU), Sentis blocks it until the tensor's pending jobs are complete. If the tensor data is on the GPU, Sentis performs a GPU upload.

## Access a tensor in GPU memory

To access a tensor stored in GPU-compute memory, use [`ComputeTensorData.Pin`](xref:Unity.InferenceEngine.ComputeTensorData.Pin*) to retrieve the data as a [`ComputeTensorData`](xref:Unity.InferenceEngine.ComputeTensorData) object.

You can then use the [`buffer`](xref:Unity.InferenceEngine.ComputeTensorData.buffer) property to directly access the tensor data in the compute buffer. For more information about how to access a compute buffer, refer to [`ComputeBuffer`](xref:UnityEngine.ComputeBuffer) in the Unity API reference.

For an example, refer to the `Read output asynchronously` example in the [sample scripts](xref:sentis-package-samples).

## Access a tensor in CPU memory

To access a tensor stored in CPU memory, use [`CPUTensorData.Pin`](xref:Unity.InferenceEngine.CPUTensorData.Pin*) to retrieve the data as a [`CPUTensorData`](xref:Unity.InferenceEngine.CPUTensorData) object.

You can then use this object in a Burst function, such as [`IJobParallelFor`](xref:Unity.Jobs.IJobParallelFor), to read from and write to the tensor data. Use the read and write fence ([`CPUTensorData.fence`](xref:Unity.InferenceEngine.CPUTensorData.fence) and [`CPUTensorData.reuse`](xref:Unity.InferenceEngine.CPUTensorData.reuse) respectively) properties of the object to handle Burst job dependencies.

You can also use the methods in the [`NativeTensorArray`](xref:Unity.InferenceEngine.NativeTensorArray) class to read from and write to the tensor data as a native array.

For examples, refer to the `Use the job system to write data` example in the [sample scripts](xref:sentis-package-samples) and the Unity documentation on [Job System](https://docs.unity3d.com/Manual/JobSystem.html).

## Additional resources

- [Use Tensors](xref:sentis-use-tensors)
- [Tensor fundamentals in Sentis](xref:sentis-tensor-fundamentals)
