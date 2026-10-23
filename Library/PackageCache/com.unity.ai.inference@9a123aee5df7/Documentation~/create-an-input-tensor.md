---
uid: sentis-create-an-input-tensor
---
# Create input for a model

A model requires input tensors with certain shapes and data types. Use this information to find model inputs and create input tensors for your model.

## Understand the required input

Before you can create input tensors for a model, inspect the model to understand the shape and data types of the model inputs.
For more information, refer to [Model inputs](xref:sentis-models-concept#model-inputs).

The [`TensorShape`](xref:Unity.InferenceEngine.TensorShape) of the [`Tensor`](xref:Unity.InferenceEngine.Tensor) you create must be compatible with the [`DynamicTensorShape`](xref:Unity.InferenceEngine.DynamicTensorShape), which defines the shape of the model input.

## Convert an array to a tensor

To create a central processing unit (CPU) tensor from a one-dimensional data array, follow these steps:

1. Create a [`TensorShape`](xref:Unity.InferenceEngine.TensorShape) object that has the length of each axis.
2. Create a [`Tensor<T>`](xref:Unity.InferenceEngine.Tensor`1) object with the shape and the data array.

For example, the following code creates a tensor for a model that takes an input tensor of shape `3 × 1 × 3`.

```
using UnityEngine;
using Unity.InferenceEngine;

public class ConvertArrayToTensor : MonoBehaviour
{
    void Start()
    {
        // Create a data array with 9 values
        float[] data = new float[] { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f };

        // Create a 3D tensor shape with size 3 × 1 × 3
        TensorShape shape = new TensorShape(3, 1, 3);

        // Create a new tensor from the array
        Tensor<float> tensor = new Tensor<float>(shape, data);
    }
}
```

## Create an empty tensor

You can create a central processing unit (CPU) tensor with zero-initialized memory as follows:

```
var tensor = new Tensor<int>(new TensorShape(1), clearOnInit: true);
```

The `clearOnInit` parameter determines whether the resulting tensor memory might be zero-initialized. Set `clearOnInit` to `false` if initial data isn't important.

## Pass inputs to a worker

If a model needs multiple input tensors, do one of the following:

- Call [`SetInput`](xref:Unity.InferenceEngine.Worker.SetInput*) on the worker for each input, and then call [`Schedule`](xref:Unity.InferenceEngine.Worker.Schedule) on the worker with no arguments.
- Call [`Schedule`](xref:Unity.InferenceEngine.Worker.Schedule(Unity.InferenceEngine.Tensor[])) on the worker with an array of the desired inputs.

```
worker.SetInput("x", xTensor);
worker.SetInput("y", yTensor);
worker.Schedule();
```

```
worker.Schedule(xTensor, yTensor);
```
To avoid garbage collection due to the `params` array creation, you can pre-allocate the input array.
```
var inputs = new [] { xTensor, yTensor };
//...
worker.Schedule(inputs);
```

## Edit a model

Use the functional API to add operations to your model inputs. For more information, refer to [Edit a model](xref:sentis-edit-a-model).

## Additional resources

- [Tensor fundamentals](xref:sentis-tensor-fundamentals)
- [Edit a model](xref:sentis-edit-a-model)
- [Convert a texture to a tensor](xref:sentis-convert-texture-to-tensor)
- [Tokenize text for input](xref:sentis-tokenizer)