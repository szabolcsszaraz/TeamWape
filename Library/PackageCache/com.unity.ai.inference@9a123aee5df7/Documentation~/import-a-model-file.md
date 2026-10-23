---
uid: sentis-import-a-model-file
---
# Import a model file

To import an ONNX, LiteRT, or PyTorch model file into your Unity project, drag the file from your computer into the `Assets` folder of the **Project** window.

If your model has external weights files, put them in the same directory as the model file so that Sentis imports them automatically.

For more information on supported model formats, refer to [Supported models](xref:sentis-supported-models).

> [!NOTE]
> If your project uses the [Addressables](https://docs.unity3d.com/Packages/com.unity.addressables@latest) package, you can mark your imported model asset as Addressable. At runtime, load it asynchronously via Addressables, then use the `ModelLoader` to create a runtime `Model` as outlined in the following section.

## Create a runtime model

To use an imported model in your scene, use [`ModelLoader.Load`](xref:Unity.InferenceEngine.ModelLoader.Load*) to create a runtime [`Model`](xref:Unity.InferenceEngine.Model) object.

```
using UnityEngine;
using Unity.InferenceEngine;

public class CreateRuntimeModel : MonoBehaviour
{
    public ModelAsset modelAsset;
    Model runtimeModel;

    void Start()
    {
        runtimeModel = ModelLoader.Load(modelAsset);
    }
}
```

After the model is loaded, you can [create an engine to run a model](xref:sentis-create-an-engine).

## Additional resources

- [How Sentis optimizes a model](xref:sentis-models-concept#how-sentis-optimizes-a-model)
- [Export an ONNX file from a machine learning framework](xref:sentis-export-convert-onnx)
- [Export a LiteRT file from a machine learning framework](xref:sentis-export-convert-litert)
- [Export a PyTorch file from a machine learning framework](xref:sentis-export-convert-torch)
- [Model Asset Inspector](xref:sentis-model-asset-inspector)
- [Supported models](xref:sentis-supported-models)
