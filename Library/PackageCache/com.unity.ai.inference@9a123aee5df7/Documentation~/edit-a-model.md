---
uid: sentis-edit-a-model
---
# Edit a model

Use the Sentis [`Functional`](xref:Unity.InferenceEngine.Functional) API to edit a model after you create or load it.

## Preprocess inputs or postprocess outputs

If your model expects inputs or returns outputs in a format that doesn't match your tensor data, use the [`Functional`](xref:Unity.InferenceEngine.Functional) API to adjust them. You can perform the following tasks:

* Prepend operations to model inputs
* Append operations to model outputs
* Add or remove inputs and outputs

For example, the following code modifies the [mnist-8](https://github.com/onnx/models/blob/main/validated/vision/classification/mnist/model/mnist-8.onnx) model to apply a softmax operation to its output:

```
using UnityEngine;
using Unity.InferenceEngine;

public class AddOutput : MonoBehaviour
{
    ModelAsset modelAsset;

    void Start()
    {
        // Load the source model from the model asset
        Model model = ModelLoader.Load(modelAsset);

        // Define the functional graph of the model.
        var graph = new FunctionalGraph();

        // Set the inputs of the graph from the original model inputs and return an array of functional tensors.
        // The input names for the graph are taken from the model input names.
        var inputs = graph.AddInputs(model);

        // Apply the model forward function to the inputs to get the source model functional outputs.
        // Sentis will destructively change the loaded source model. To avoid this at the expense of
        // higher memory usage and compile time, use the Functional.ForwardWithCopy method.
        FunctionalTensor[] outputs = Functional.Forward(model, inputs);

        // Calculate the softmax of the first output with the functional API.
        FunctionalTensor softmaxOutput = Functional.Softmax(outputs[0]);

        // Add the output to the graph with a name.
        graph.AddOutput(softmaxOutput, "softmax");

        // Build the model from the graph using the `Compile` method.
        var modelWithSoftmax = graph.Compile();
    }
}

```

Sentis runs [model optimization](xref:sentis-models-concept#how-sentis-optimizes-a-model) on models you create with the [`Functional`](xref:Unity.InferenceEngine.Functional) API. Consequently, the operations used during inference might differ from what you expect.

> [!NOTE]
> [`Compile`](xref:Unity.InferenceEngine.FunctionalGraph.Compile*) is a slow operation that requires significant memory. It's recommended to run this offline and serialize the computed model. For more information, refer to [Serialize A Model](xref:sentis-serialize-a-model).

## Additional resources

- [Supported functional methods](xref:sentis-supported-functional-methods)
- [Encrypt a model](xref:sentis-encrypt-a-model)
