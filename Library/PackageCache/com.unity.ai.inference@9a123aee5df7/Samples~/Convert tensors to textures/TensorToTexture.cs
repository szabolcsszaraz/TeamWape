using Unity.InferenceEngine;
using UnityEngine;

public class TensorToTexture : MonoBehaviour
{
    // 8x8 rgba render texture
    [SerializeField]
    RenderTexture rgbaRenderTexture;

    // 8x8 r render texture
    [SerializeField]
    RenderTexture rRenderTexture;

    void Start()
    {
        Debug.LogFormat("When running this example, the contents of the RGBA and R tensors will be rendered to the following render textures correspondingly: {0}, {1}",
            rgbaRenderTexture ? rgbaRenderTexture.name : "None", rRenderTexture ? rRenderTexture.name : "None");

        // create single pixel tensors
        using Tensor<float> rTensor = new Tensor<float>(new TensorShape(1, 1, 1, 1));
        using Tensor<float> rgbaTensor = new Tensor<float>(new TensorShape(1, 4, 1, 1));

        // set red channel to 0.5
        rTensor[0, 0, 0, 0] = 0.5f;
        rgbaTensor[0, 0, 0, 0] = 0.5f;

        // set alpha channel to 1
        rgbaTensor[0, 3, 0, 0] = 1f;

        // blit texture to render texture, if dimensions don't match tensor will be linearly sampled
        TextureConverter.RenderToTexture(rTensor, rRenderTexture);
        TextureConverter.RenderToTexture(rgbaTensor, rgbaRenderTexture);

        // if render texture has fewer channels than input tensor the tensor channels will be truncated
        TextureConverter.RenderToTexture(rgbaTensor, rRenderTexture);

        // if render texture has more channels than input tensor the remaining texture channels will default to rgb = 0, alpha = 1
        // here the final texture will have color (0.5, 0, 0, 1) everywhere
        TextureConverter.RenderToTexture(rTensor, rgbaRenderTexture);

        // for advanced conversions use a TextureTransform
        // with BroadcastChannels = true the tensor will broadcast to remaining tensor channels
        // here the final texture will have color (0.5, 0.5, 0.5, 0.5) everywhere
        TextureConverter.RenderToTexture(rTensor, rgbaRenderTexture, new TextureTransform().SetBroadcastChannels(true));

        // SetChannelColorMask overrides the color in one or multiple color channels
        // here the final texture will have color (0.5, 0.8, 0, 1) everywhere
        TextureConverter.RenderToTexture(rTensor, rgbaRenderTexture, new TextureTransform().SetChannelColorMask(Channel.B, true, 0.8f));

        // it's possible to chain together operations on the transform, here we combine BroadcastChannel and ChannelColorMask
        // here we output the original single channel tensor as the alpha channel of white texture
        TextureConverter.RenderToTexture(rTensor, rgbaRenderTexture, new TextureTransform().SetBroadcastChannels(true).SetChannelColorMask(true, true, true, false, Color.white));

        // swizzle defines the layout of the color channels in the tensor
        // here the final texture will have color (0, 0, 0.5, 1) everywhere
        TextureConverter.RenderToTexture(rgbaTensor, rgbaRenderTexture, new TextureTransform().SetChannelSwizzle(ChannelSwizzle.BGRA));

        // if the tensor has a different layout use SetTensorLayout to sample from the tensor correctly
        using Tensor<float> rgbaNHWCTensor = new Tensor<float>(new TensorShape(1, 8, 8, 4));
        TextureConverter.RenderToTexture(rgbaNHWCTensor, rgbaRenderTexture, new TextureTransform().SetTensorLayout(TensorLayout.NHWC));

        // if 0, 0 in the tensor should correspond to the bottom left of the texture rather than the top left use CoordOrigin.BottomLeft
        TextureConverter.RenderToTexture(rgbaTensor, rgbaRenderTexture, new TextureTransform().SetCoordOrigin(CoordOrigin.BottomLeft));
    }
}
