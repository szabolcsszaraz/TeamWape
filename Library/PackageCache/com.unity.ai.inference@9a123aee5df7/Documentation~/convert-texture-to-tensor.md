---
uid: sentis-convert-texture-to-tensor
---
# Convert a texture to a tensor

Use [`TextureConverter.ToTensor`](xref:Unity.InferenceEngine.TextureConverter.ToTensor*) to convert a [`Texture2D`](xref:UnityEngine.Texture2D) or a [`RenderTexture`](xref:UnityEngine.RenderTexture) to a tensor.

```
using UnityEngine;
using Unity.InferenceEngine;

public class ConvertTextureToTensor : MonoBehaviour
{
    Texture2D inputTexture;

    void Start()
    {
        Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 4, inputTexture.height, inputTexture.width));
        TextureConverter.ToTensor(inputTexture, inputTensor);
    }
}
```

By default, the tensor must have the following properties:

- It has a data type of float.
- It follows the tensor layout of batch, channels, height, width (NCHW). For example, `1 × 3 × 24 × 32` represents a single RGB texture of height of 24 and a width of 32.

Ensure the texture format matches the requirements of your model. To change the format of the texture, such as adjusting the number of channels, use the settings in [Texture Import Settings window](https://docs.unity3d.com/Documentation/Manual/class-TextureImporter.html).

Depending on the input tensor your model needs, you might also need to scale the values in the tensor before you run the model. For example, if your model needs values from `0 - 255` instead of from `0 - 1`. You can edit the model using the functional API to scale a tensor input. For more information, refer to [Edit a model](xref:sentis-edit-a-model).

For an example, refer to the `Convert textures to tensors` example in the [sample scripts](xref:sentis-package-samples).

### Override texture layout

You can use a [`TextureTransform`](xref:Unity.InferenceEngine.TextureTransform) object to override the properties of a texture. For example, the following code changes or swizzles the order of the texture channels to blue, green, red, alpha:

```
// Create a TextureTransform that swizzles the order of the channels of the texture
TextureTransform swizzleChannels = new TextureTransform().SetChannelSwizzle(ChannelSwizzle.BGRA);

// Convert the texture to a tensor using the TextureTransform object
Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 4, inputTexture.height, inputTexture.width));
TextureConverter.ToTensor(inputTexture, inputTensor, swizzleChannels);
```

You can also chain operations together.

```
// Create a TextureTransform that swizzles the order of the channels of the texture and changes the coordinate origin
TextureTransform swizzleChannelsAndChangeOrigin = new TextureTransform().SetChannelSwizzle(ChannelSwizzle.BGRA).SetCoordOrigin(CoordOrigin.BottomLeft);

// Convert the texture to a tensor using the TextureTransform object
Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 4, inputTexture.height, inputTexture.width));
TextureConverter.ToTensor(inputTexture, inputTensor, swizzleChannelsAndChangeOrigin);
```

If the width and height of the texture doesn't match the width and height of the tensor, Sentis applies linear resampling to upsample or downsample the texture.

For more information, refer to the [`TextureTransform`](xref:Unity.InferenceEngine.TextureTransform) API reference.

### Set a tensor to the correct format

When you convert a texture to a tensor, Sentis defaults to the NCHW layout.

If your model needs a different layout, use [`SetTensorLayout`](xref:Unity.InferenceEngine.TextureTransform.SetTensorLayout*) to set the layout of the converted tensor.

For more information about tensor formats, refer to [Tensor fundamentals in Sentis](xref:sentis-tensor-fundamentals).

### Avoid tensor and texture creation

Allocating memory affects performance. If possible, allocate all necessary memory on startup. Use [`TextureConverter`](xref:Unity.InferenceEngine.TextureConverter) methods to directly operate on pre-allocated tensor and textures.

For example, to read data from a webcam every frame, copy the webcam texture content into the input tensor. Don't create a new tensor every frame.

```
Tensor<float> inputTensor;
Texture webcamTexture;

// Allocate resources on startup
void Start()
{
    inputTensor = new Tensor<float>(new TensorShape(1, 3, webcamTexture.height, webcamTexture.width));
}
void Update()
{
    // Copy webcamTexture into inputTensor : no memory allocations!
    TextureConverter.ToTensor(webcamTexture, inputTensor, new TextureTransform());

    // Run inference
}
```

## Additional resources

- [Tensor fundamentals in Sentis](xref:sentis-tensor-fundamentals)
- [Edit a model](xref:sentis-edit-a-model)
- [Use output data](xref:sentis-use-model-output)
