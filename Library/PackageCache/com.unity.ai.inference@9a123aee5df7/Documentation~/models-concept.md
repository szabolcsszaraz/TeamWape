---
uid: sentis-models-concept
---
# Sentis models

Sentis can import and run trained machine learning model files in Open Neural Network Exchange (ONNX), LiteRT (formerly TensorFlow Lite), and PyTorch formats.

To get a model that's compatible with Sentis, you can do one of the following:

- Train a model with a framework like TensorFlow, PyTorch, or Keras. Subsequently, export it in [ONNX format](xref:sentis-export-convert-onnx), [LiteRT format](xref:sentis-export-convert-litert), or [PyTorch format](xref:sentis-export-convert-torch).
- Download a trained model file and [convert to ONNX format](xref:sentis-export-convert-onnx). For more information, refer to the [ONNXXMLTools](https://github.com/onnx/onnxmltools) Python package.
- Download a trained model file and [convert to LiteRT format](xref:sentis-export-convert-litert).
- Download a trained model file and [convert to PyTorch format](xref:sentis-export-convert-torch).
- Download a trained model that's already in ONNX, LiteRT, or PyTorch format, such as those available in the [ONNX Model Zoo](https://github.com/onnx/models). For more resources, refer to [supported models](xref:sentis-supported-models).

## How Sentis optimizes a model

When you import an ONNX model, each ONNX operator in the model graph becomes a Sentis layer. Similarly, when you import a LiteRT or PyTorch model, each operator is mapped to one or more Sentis layers.

To check the list of layers in the imported model, in the order Sentis runs them, open the **[Model Asset Inspector](xref:sentis-model-asset-inspector)**. For more information, refer to [Supported ONNX operators](xref:sentis-supported-operators).

Sentis optimizes models to make them smaller and more efficient. For example, Sentis might do the following to an imported model:

- Remove a layer or subgraph and turn it into a constant.
- Replace a layer or subgraph with a simpler layer or subgraph that works the same way.
- Set a layer to run on the CPU, if the data must be read at inference time.

The optimization doesn't affect what the model inputs or outputs.

## Model inputs

You can get the shape of your model inputs in one of two ways:
- [Inspect a model](xref:sentis-inspect-a-model) to use the [`inputs`](xref:Unity.InferenceEngine.Model.inputs) property of the runtime model.
- Select your model from the **Project** window to open the [Model Asset Inspector](xref:sentis-model-asset-inspector) and view the **Inputs** section.

The shape of a model input consists of multiple dimensions, defined as a [`DynamicTensorShape`](xref:Unity.InferenceEngine.DynamicTensorShape).

 The dimensions of a model input are either [static](#static-dimensions) or [dynamic](#dynamic-dimensions):
- An `int` denotes a static dimension.
- A named string (for example, `batch_size`, `height`) represents a dynamic dimension. These names come from the ONNX model.

### Static dimensions

The value of the `int` defines the specific shape of the input the model accepts.

For example, if the **Inputs** section displays **(1, 1, 28, 28)**, the model only accepts an input tensor of shape `1 x 1 x 28 x 28`.

### Dynamic dimensions

A dynamic dimension allows flexibility in input shapes. When the shape of a model input contains a named dynamic dimension, for example `batch_size` or `height`, that input dimension can be any size.

For example, if the input has a shape **(batch_size, 1, 28, 28)**, the first dimension of the input shape can be any size.

When you define the input tensor for this input shape, the following tensor shapes are valid:

```
[1, 1, 28, 28]
[2, 1, 28, 28]
[3, 1, 28, 28] ...
```

If you change the size of another dimension, however, the input tensor isn't valid. For example:

```
[1, 3, 28, 28]
```

> [!NOTE]
> If a model uses inputs with dynamic input dimensions, Sentis might not be able to optimize the model as efficiently as a model that uses static input dimensions. This might slow down the model.

For more information on how to set a static value for a dynamic dimension, refer to [Import settings for ONNX models](xref:sentis-onnx-import-settings).

## Additional resources

- [Import a model](xref:sentis-import-a-model-file)
- [Import settings for ONNX models](xref:sentis-onnx-import-settings)
- [Import settings for LiteRT models](xref:sentis-litert-import-settings)
- [Export an ONNX file from a machine learning framework](xref:sentis-export-convert-onnx)
- [Export a LiteRT file from a machine learning framework](xref:sentis-export-convert-litert)
- [Export a PyTorch file from a machine learning framework](xref:sentis-export-convert-torch)
- [Supported ONNX operators](xref:sentis-supported-operators)
- [Supported LiteRT operators](xref:sentis-supported-litert-operators)
- [Supported PyTorch operators](xref:sentis-supported-torch-export-operators)
