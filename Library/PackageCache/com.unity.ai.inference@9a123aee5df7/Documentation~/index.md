---
uid: sentis-index
---
# Sentis overview

[!include[](snippets/name-change.md)]

Sentis is a neural network inference library for Unity. It lets you import trained neural network models into Unity and run them in real-time with your target device’s compute resources, such as central processing unit (CPU) or graphics processing unit (GPU).

Sentis supports real-time applications across all Unity-supported platforms.

The package is officially released and available to all Unity users through the **Package Manager**.

> [!TIP]
> Prior experience with machine learning frameworks like [TensorFlow](https://www.tensorflow.org/) or [PyTorch](https://pytorch.org/) is helpful, but not required. It can make it easier to understand how to work with models in Sentis.

|Section|Description|
|-|-|
|[Get started](xref:sentis-get-started)|Learn how to install Sentis, explore sample projects, and understand the Sentis workflow.|
|[Create a model](xref:sentis-create-a-model)|Create a runtime model by importing an ONNX model file or using the Sentis model API.|
|[Run a model](xref:sentis-run-an-imported-model)|Create input data for a model, create an engine to run the model, and get output.|
|[Use Tensors](xref:sentis-use-tensors)|Learn how to get, set, and modify input and output data.|
|[Profile a model](xref:sentis-profile-a-model)|Use Unity tools to profile the speed and performance of a model.|

## Supported platforms

Sentis supports [all Unity runtime platforms](https://docs.unity3d.com/Documentation/Manual/PlatformSpecific.html).

Performance might vary based on:
* Model operators and complexity
* Hardware and software platform constraints of your device
* Type of engine used

   For more information, refer to [Models](xref:sentis-models-concept) and [Create an engine](xref:sentis-create-an-engine).

## Supported model types

Sentis supports most models in Open Neural Network Exchange (ONNX) format with an [opset version](https://github.com/onnx/onnx/blob/main/docs/Versioning.md#released-versions) between 7 and 25. For more information, refer to [Supported models](xref:sentis-supported-models) and [Supported ONNX operators](xref:sentis-supported-operators).

Sentis supports most models in [LiteRT (formerly TensorFlow Lite)](https://ai.google.dev/edge/litert) format. For more information, refer to [Supported LiteRT operators](xref:sentis-supported-litert-operators).

Sentis supports most models (exported programs) in [PyTorch](https://docs.pytorch.org/tutorials/beginner/saving_loading_models.html) format if they are decomposed to [Core ATen IR operators](https://docs.pytorch.org/docs/stable/torch.compiler_ir.html). For more information, refer to [Supported PyTorch operators](xref:sentis-supported-torch-export-operators).

## Places to find pre-trained models

[!include[](snippets/model-registry.md)]

## Additional resources

- [Sample scripts](xref:sentis-package-samples)
- [Unity Discussions group](https://discussions.unity.com/tag/Sentis)
- [Understand the Sentis workflow](xref:sentis-understand-sentis-workflow)
- [Sentis models](xref:sentis-models-concept)
- [Tensor fundamentals in Sentis](xref:sentis-tensor-fundamentals)
- [The AI menu](https://docs.unity3d.com/Manual/ai-menu.html) in Unity Editor
- [Unity Dashboard AI settings](https://docs.unity.com/en-us/ai)
- [Tokenize text for input](xref:sentis-tokenizer)
