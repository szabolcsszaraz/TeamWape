---
uid: sentis-create-a-new-model
---
# Create a new model

You can use the Sentis [`Functional`](xref:Unity.InferenceEngine.Functional) API to create a new runtime model without an ONNX, LiteRT, or PyTorch file. For example, if you want to perform a series of tensor operations without weights or build your own model serialization from another model format.

## Using the functional API

To create a model with the functional API, follow these steps:

1. Create a [`FunctionalGraph`](xref:Unity.InferenceEngine.FunctionalGraph) object.
2. Add inputs to the graph by specifying their data type, shape, name. This returns [`FunctionalTensor`](xref:Unity.InferenceEngine.FunctionalTensor) objects for each input.
3. Apply a series of functional API methods to the functional tensors objects to create the desired output functional tensors.
4. Add the outputs to the graph by calling the [`AddOutput`](xref:Unity.InferenceEngine.FunctionalGraph.AddOutput*) method with the output functional tensor and name.
5. Compile the model with the [`Compile`](xref:Unity.InferenceEngine.FunctionalGraph.Compile*) method.

The following example shows the creation of a simple model to calculate the dot product of two vectors.

```
using System;
using Unity.InferenceEngine;
using UnityEngine;

public class CreateNewModel : MonoBehaviour
{
    Model model;

    void Start()
    {
        // Create the functional graph.
        FunctionalGraph graph = new FunctionalGraph();

        // Add two inputs to the graph with data types and shapes.
        // Our dot product operates on two vector tensors of the same size `6`.
        FunctionalTensor x = graph.AddInput<float>(new TensorShape(6), "input_x");
        FunctionalTensor y = graph.AddInput<float>(new TensorShape(6), "input_y");

        // Calculate the elementwise product of the input `FunctionalTensor`s using an operator.
        FunctionalTensor prod = x * y;

        // Sum the product along the first axis flattening the summed dimension.
        FunctionalTensor reduce = Functional.ReduceSum(prod, dim: 0, keepdim: false);

        graph.AddOutput(reduce, "output_reduce");
        graph.AddOutput(prod, "output_prod");

        // Build the model using the `Compile` method.
        model = graph.Compile();
    }
}
```

To debug your code, use [`ToString`](xref:Unity.InferenceEngine.FunctionalTensor.ToString) to retrieve information, such as its shape and datatype.

You can then [create an engine to run a model](xref:sentis-create-an-engine).

### Model inputs and outputs

When you compile a model with the [`Compile`](xref:Unity.InferenceEngine.FunctionalGraph.Compile*) method, the resulting model includes the inputs and outputs you defined with  [`AddInput`](xref:Unity.InferenceEngine.FunctionalGraph.AddInput*) and [`AddOutput`](xref:Unity.InferenceEngine.FunctionalGraph.AddOutput*).

You can assign names to inputs and outputs when you add them to the graph. If you don’t specify a name, Sentis automatically assigns a default name in the format `input_0`, `input_1`, `output_0`, `output_1`, and so forth in numerical order.

## Additional resources

- [Supported functional methods](xref:sentis-supported-functional-methods)
- [Edit a model](xref:sentis-edit-a-model)
