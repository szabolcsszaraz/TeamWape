using UnityEngine;
using Unity.InferenceEngine;
using UnityEngine.Assertions;

public class TextureToTensor : MonoBehaviour
{
    // 8x8 red texture
    [SerializeField]
    Texture2D texture;

    void Start()
    {
        // tensor dimensions are taken from texture
        using Tensor<float> tensor = new Tensor<float>(new TensorShape(1, 4, texture.height, texture.width));
        TextureConverter.ToTensor(texture, tensor);

        // specifying channel number truncates the channels from the texture
        using Tensor<float> tensorRGB = new Tensor<float>(new TensorShape(1, 3, texture.height, texture.width));
        TextureConverter.ToTensor(texture, tensorRGB);

        // specifying width and/or height resamples the texture linearly
        using Tensor<float> tensor6X12 = new Tensor<float>(new TensorShape(1, 4, 6, 12));
        TextureConverter.ToTensor(texture, tensor6X12);

        // alternative tensor layout
        using Tensor<float> tensorNHWC = new Tensor<float>(new TensorShape(1, texture.height, texture.width, 4));
        TextureConverter.ToTensor(texture, tensorNHWC, new TextureTransform().SetTensorLayout(TensorLayout.NHWC));

        // explicit alternative tensor layout
        using Tensor<float> tensorNHCW = new Tensor<float>(new TensorShape(1, texture.height, 4, texture.width));
        TextureConverter.ToTensor(texture, tensorNHCW, new TextureTransform().SetTensorLayout(0, 2, 1, 3));

        // set tensor 0, 0 from bottom left of texture rather than default top left
        using Tensor<float> tensorBottomLeft = new Tensor<float>(new TensorShape(1, 4, texture.height, texture.width));
        TextureConverter.ToTensor(texture, tensorBottomLeft, new TextureTransform().SetCoordOrigin(CoordOrigin.BottomLeft));

        // swizzle color channels of texture using preset
        using Tensor<float> tensorBGRA = new Tensor<float>(new TensorShape(1, 4, texture.height, texture.width));
        TextureConverter.ToTensor(texture, tensorBGRA, new TextureTransform().SetChannelSwizzle(ChannelSwizzle.BGRA));

        // swizzle color channels of texture explicitly to all sample from Red channel in texture
        using Tensor<float> tensorRRRR = new Tensor<float>(new TensorShape(1, 4, texture.height, texture.width));
        TextureConverter.ToTensor(texture, tensorRRRR, new TextureTransform().SetChannelSwizzle(0, 0, 0, 0));

        // chain transform settings together
        using Tensor<float> tensorChained = new Tensor<float>(new TensorShape(1, 4, texture.height, texture.width));
        TextureConverter.ToTensor(texture, tensorChained, new TextureTransform().SetCoordOrigin(CoordOrigin.BottomLeft).SetChannelSwizzle(ChannelSwizzle.BGRA));
    }
}
