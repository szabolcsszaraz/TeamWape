---
uid: sentis-get-the-output
---
# Get output from a model

Use this information to get the output from a model.

## Get the tensor output

To get the tensor output, you have two options:
* Use [`PeekOutput`](xref:Unity.InferenceEngine.Worker.PeekOutput*) to get a reference to an output tensor.
* Use [`CopyOutput`](xref:Unity.InferenceEngine.Worker.CopyOutput*) to copy the output into a tensor that you manage outside the scope of the worker.

The following sections provide information on the methods available to retrieve the tensor output, along with their respective strengths and weaknesses.

### Use PeekOutput

Use [`PeekOutput`](xref:Unity.InferenceEngine.Worker.PeekOutput*) to get a reference to the output of the tensor. [`PeekOutput`](xref:Unity.InferenceEngine.Worker.PeekOutput*) returns a [`Tensor`](xref:Unity.InferenceEngine.Tensor) object, so you usually need to cast it to a [`Tensor<float>`](xref:Unity.InferenceEngine.Tensor`1) or a [`Tensor<int>`](xref:Unity.InferenceEngine.Tensor`1).

For example:

```
worker.Schedule(inputTensor);
Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
```

Sentis worker memory allocator owns the reference returned by [`PeekOutput`](xref:Unity.InferenceEngine.Worker.PeekOutput*). It implies the following:

- You don't need to use `Dispose` on the output.
- If you change the output or you rerun the worker, both the worker output and the [`PeekOutput`](xref:Unity.InferenceEngine.Worker.PeekOutput*) copy change.
- Using `Dispose` on the worker disposes the [`PeekOutput`](xref:Unity.InferenceEngine.Worker.PeekOutput*) copy.

If you call `Schedule` again, the tensor is overwritten.

> [!NOTE]
> Be careful when you read data from an output tensor. In many instances, you might unintentionally trigger a blocking wait until the model finishes to run before it downloads the data from the graphics processing unit (GPU) or Burst to the central processing unit (CPU). To mitigate this overhead, consider [reading output from a model asynchronously](xref:sentis-read-output-async). Additionally, [profiling a model](xref:sentis-profile-a-model) can provide valuable insight into its performance.

### Download the data of the original tensor

You can do a blocking download to a read only `NativeArray` or `Array` copy of the output tensor's data.

* Use [`DownloadToNativeArray`](xref:Unity.InferenceEngine.Tensor`1.DownloadToNativeArray*) on the tensor after you use [`PeekOutput`](xref:Unity.InferenceEngine.Worker.PeekOutput*).
* Use [`DownloadToArray`](xref:Unity.InferenceEngine.Tensor`1.DownloadToArray*) on the tensor after you use [`PeekOutput`](xref:Unity.InferenceEngine.Worker.PeekOutput*).

### Wait on the data of the original tensor

To avoid blocking the model while it retrieves data, you can request an asynchronous readback of the output tensor.

```
Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
var result = await outputTensor.ReadbackAndCloneAsync();
```

```
Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
outputTensor.ReadbackRequest();

// when done
outputTensor.ReadbackAndClone(); // not blocking
```

For more information, refer to [Read Outputs Asynchronously](xref:sentis-read-output-async).

### Use CopyOutput

Use `CopyOutput` to copy the output of a worker into a tensor that you manage outside the scope of the worker.

* If you pass in `null`, Sentis creates and returns a new tensor that contains a copy of the worker’s output.
* If you pass in an existing tensor, Sentis reshapes it to match the output shape and copies the output data into it.

```
Tensor myOutputTensor;
//...
void Update () {
   worker.Schedule(inputTensor);
   worker.CopyOutput("output", ref myOutputTensor);
}
```

[`CopyOutput`](xref:Unity.InferenceEngine.Worker.CopyOutput*) reshapes the provided tensor to match calculated output shape. Ensure that the provided tensor has capacity for the output.

```
// The model outputs a tensor of shape (1, 10)

// CopyOutput works on empty tensors, i.e. tensors without a tensor data.
myOutputTensor = new Tensor<float>(new TensorShape(1, 10), data: null);
worker.CopyOutput("output", ref myOutputTensor);

// CopyOutputInto works on tensors of different shape as long as the dataOnBackend has large enough capacity
myOutputTensor = new Tensor<float>(new TensorShape(152));
worker.CopyOutput("output", ref myOutputTensor);
// myOutputTensor now has shape (1, 10) but still has dataOnBackend.maxCapacity == 152
```

When you use `CopyOutput`, you're responsible for managing the tensor that receives the output:

* You must call `Dispose()` on the tensor when you finish using it, to free up memory.
* The tensor isn't automatically updated if you call [`Worker.Schedule`](xref:Unity.InferenceEngine.Worker.Schedule*) again. If you need fresh output, call `CopyOutput` again after scheduling the model.

## Multiple outputs

If the model has multiple outputs, you can use each output name as a parameter in [`Worker.PeekOutput`](xref:Unity.InferenceEngine.Worker#Unity_InferenceEngine_Worker_PeekOutput_System_String_).

## Additional resources

- [Manage memory](xref:sentis-manage-memory)
- [Tensor fundamentals](xref:sentis-tensor-fundamentals)
- [Use output data](xref:sentis-use-model-output)
- [Read output from a model asynchronously](xref:sentis-read-output-async)
