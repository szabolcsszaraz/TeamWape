---
uid: sentis-serialize-a-model
---
# Serialize a Model

For larger models, it's advisable to use a serialized asset, which typically comes with the file extension `.sentis`.

## Create a serialized asset

After you import your ONNX, LiteRT, or PyTorch file into the Unity project, follow these steps:

1. Select the model file in the **Project** window.
1. In the **Inspector** window, select **Serialize to StreamingAssets**.

Unity creates a serialized version of the model and saves it as a `.sentis` file in the **StreamingAssets** folder.

## Load a serialized asset

To load a serialized model in your project at runtime, use the following code:

```
Model model = ModelLoader.Load(Application.streamingAssetsPath + "/mymodel.sentis");
```

Replace `mymodel.sentis` with the name of your serialized model file.

## Advantages of using serialized models

Some advantages of using a serialized model are as follows:

* Saves disk space in your project
* Faster loading times
* Validated to work in Unity
* Easier to share

## Serialization layout

Sentis serializes a `.sentis` file with `FlatBuffers`.

```
             ┌───────────────────────────────────┐
             │Flatbuffer-serialized              |
             | model desription                  │
             │                                   │
          ┌─ ├───────────────────────────────────┤
          │  │ Weight chunk data                 |
          │  │                                   │
          │  │                                   │
Weights  ─┤  ├───────────────────────────────────┤
          │  │ Weight chunk data                 │
          │  │                                   │
          │  │                                   │
          │  ├───────────────────────────────────┤
          │  │...                                │
          └─ └───────────────────────────────────┘
```

For more information, refer to `com.unity.ai.inference/Runtime/Core/Serialization/program.fbs`.

## Additional resources

- [Quantize a model](xref:sentis-quantize-a-model)
- [Inspect a model](xref:sentis-inspect-a-model)
