---
uid: sentis-do-basic-tensor-operations
---
## Create and modify tensors

Tensor methods in Sentis are similar to methods found in frameworks like NumPy, TensorFlow, and PyTorch.

## Create a tensor

You can create a basic tensor with the methods in the [`Tensor`](xref:Unity.InferenceEngine.Tensor) API.

For more information, refer to [Create input for a model](xref:sentis-create-an-input-tensor).

## Get and set values of a tensor

If your tensor data [`backendType`](xref:Unity.InferenceEngine.ITensorData.backendType) is [`BackendType.CPU`](xref:Unity.InferenceEngine.BackendType.CPU) and has finished being computed ([`IsReadbackRequestDone`](xref:Unity.InferenceEngine.Tensor.IsReadbackRequestDone*)), you can directly set and get values.

```
var tensor = new Tensor<float>(new TensorShape(1, 2, 3));
tensor[0, 1, 2] = 5.2f; // set value at index 0 of dim0 = 1, index 1 of dim1 = 2 and index 2 of dim2 = 3

float value = tensor[0, 1, 2];
Assert.AreEqual(5.2f, value);
```

## Reshape a tensor

You can reshape a tensor directly, for example:

```
var tensor = new Tensor<float>(new TensorShape(10));
tensor.Reshape(new TensorShape(2, 5));
```

The new shape of the tensor must fit in the allocated data on the backend. You can use the [`length`](xref:Unity.InferenceEngine.TensorShape.length) property of a tensor shape and the [`maxCapacity`](xref:Unity.InferenceEngine.ITensorData.maxCapacity) property of the tensor data to check the number of elements.

```
var tensor = new Tensor<float>(new TensorShape(10));
Assert.AreEqual(10, tensor.count);
Assert.AreEqual(10, tensor.dataOnBackend.maxCapacity);

// Reshaping the tensor with a smaller shape

tensor.Reshape(new TensorShape(2, 3));
Assert.AreEqual(6, tensor.count);
Assert.AreEqual(10, tensor.dataOnBackend.maxCapacity);
// The underlying dataOnBackend still contains 10 elements

// reshape to match dataOnBackend.maxCapacity
tensor.Reshape(new TensorShape(1, 10));
```

When you reshape a tensor, Sentis doesn't modify the data or capacity of the underlying [`dataOnBackend`](xref:Unity.InferenceEngine.Tensor.dataOnBackend).

> [!NOTE]
> You can't reshape a tensor on the graphics processing unit (GPU) when using [`BackendType.GPUPixel`](xref:Unity.InferenceEngine.BackendType.GPUPixel) because GPU textures aren't stored linearly.

## Download values of a tensor

You can perform a blocking download to get a copy of the tensor data to a `NativeArray` or `Array` as follows:

```
var nativeArray = tensor.DownloadToNativeArray();
var array = tensor.DownloadToArray();
```

> [!NOTE]
> These methods return copies of your tensor data. Any changes to the downloaded arrays don't affect the original tensor.

This download is a blocking call and will force a wait if [`ReadbackRequest`](xref:Unity.InferenceEngine.Tensor.ReadbackRequest*) hasn't been called or [`IsReadbackRequestDone`](xref:Unity.InferenceEngine.Tensor.IsReadbackRequestDone*) is false. For more information, refer to [Read Outputs Asynchronously](xref:sentis-read-output-async).

## Additional resources

- [Tensor fundamentals](xref:sentis-tensor-fundamentals)
- [Model inputs](xref:sentis-models-concept#model-inputs)
