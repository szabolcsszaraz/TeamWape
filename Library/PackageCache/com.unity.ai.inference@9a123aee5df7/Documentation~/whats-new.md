---
uid: sentis-whats-new
---
# What's new in Sentis 2.6

This is a summary of the changes from Sentis 2.5 to Sentis 2.6.

## Added

- Official support for ONNX opset versions up to version `25`.
- Functional methods for the `Swish` and `RMSNorm` operators, including support for the `alpha` argument on the `Swish` operator.
- Support for `Buffer` in PyTorch model import.
- Generic truncation in the Tokenizer, with support for `longestfirst`, `onlyfirst`, and `onlysecond` strategies.
- Compatibility with Fast Enter Play Mode for CoreCLR.
- Improved analytics and error reporting when importing models with unsupported operators.

## Updated

- The random number generator now uses Unity's `Mathematics.Random` instead of `System.Random`.
- Improved documentation for the `Tensor` and `Functional` APIs.
- Updated documentation for Cubic interpolation mode support limitations.

## Fixed

- Corrected behavior of `ReduceL1`, `ReduceL2`, `ReduceSumSquare`, and `ReduceLogSum` operators when `noop_with_empty_axes` is `true` and `axes` are empty.
- Resolved an issue with the `Interpolate` operator when using the `scaleFactor` argument.
- Fixed a case where GPU allocations (`ComputeTensorData`) were used for tensors without a corresponding backend.
- Prevented crashes when closing the editor while in Play mode.
- Fixed a memory leak in PyTorch model import.
- Resolved a GPU crash for convolution with padding on GPU compute.
- Fixed an issue with the `Split` operator when importing `.sentis` files.

# What's new in Sentis 2.5

This is a summary of the changes from Sentis 2.4 to Sentis 2.5.

## Added

- `PyTorch` model import to directly import PyTorch files (.pt2) to Sentis without using ONNX.
- `LRN (LocalResponseNormalization)` operator is now implemented on all backends.
- `3D MaxPool` and `AveragePool` operators are now implemented on all backends.
- Sentis Importer now allows users to specify dynamic dimensions as static on Sentis model import, same as we do for ONNX.
- Tokenizer now parses Hugging Face models.
- Wider coverage of all the components of the Tokenizer.

## Updated

- Model Visualizer now supports background loading of models.
- Resize operator on CPU no longer uses main (mono) thread path.
- All model converters use switch-case instead of if-else cascade for improved performance
- Mono APIs are migrated to CoreCLR-compatible APIs

## Fixed

- Editor crash when quitting in Play Mode.
- Memory leak in FuseConstantPass was fixed.
- `Clip` operator no longer need CPU fallback for min/max parameters.
- `Mod` operator fix on some platform with float operands.
- Faulty optimization pass was corrected.
- Fix in existing burst code for 2D pooling vectorization calculations.
- `TopK` issue on `GPUCompute` when dimension is specified.
- Many fixes to the Tokenizer.

# What's new in Sentis 2.4

Sentis is the new name for this package.

This is a summary of the changes from Inference Engine 2.3 to Sentis 2.4.

## Added

- Tokenizer API for tokenization and detokenization of strings with language models.
- LiteRT model import to directly import .tflite files to Sentis without using ONNX.
- Spectral operators to enable audio models.
- Many new operators corresponding to LiteRT and torch operators with functional API and optimization passes.

## Updated

- Import of ONNX models has been greatly sped up and optimized to match the ONNX specification.

## Fixed

- Many small import, inference and documentation issues.

# What's new in Inference Engine 2.3

This is a summary of the changes from Inference Engine 2.2 to Inference Engine 2.3.

## Added

- Model Visualizer for inspecting models as node-based graphs inside the Unity Editor.
- `GatherND` and `Pow` operators now support `Tensor<int>` inputs more widely.
- `ConvTranspose` and `Constant` operators now support more input arguments.


# What's new in Inference Engine 2.2

Inference Engine is the new name for the [Sentis package](https://docs.unity3d.com/Packages/com.unity.sentis@2.1/manual/index.html).

This is a summary of the changes from Sentis 2.1 to Inference Engine 2.2.

For information on how to upgrade, refer to the [Upgrade Guide](xref:sentis-upgrade-guide).

## Added

- Dynamic input shape dimensions support at import time for better model optimization.
- Custom input and output names for models created with the functional API.
- The model stores the shapes and data types of intermediate and output tensors and displays them in the **Model Asset Inspector**.
- New `Mish` operator.
- Improved shape inference for model optimization.

## Updated

- `ScatterElements` and `ScatterND` operators now support `min` and `max` reduction modes.
- `DepthToSpace` and `SpaceToDepth` now support integer tensors.
- `TopK` supports integer tensors.
- `Functional.OneHot` now allows negative indices.
- `RoiAlign` now supports the `coordinate_transformation_mode` parameter.
- Reduction operators return correct results when reducing a tensor along an axis of length 0.
- `Reshape` operator can now infer unknown dimensions even when reshaping a length 0 tensor like in PyTorch.
- Improved documentation for **Model Asset Inspector**.

## Removed

- Obsolete Unity Editor menu items.
- Slow CPU support for 4-dimensional and higher `Convolution` layers.

## Fixed

- Out-of-bounds errors for certain operators on `GPUCompute` backend.
- The `TextureConverter` methods now correctly performs sRGB to RGB conversions.
- Incorrect graph optimizations for certain models.
- Issues with negative padding values in pooling and convolutions.
- Accurate handling of large and small integer values in the `GPUPixel` backend.
- Proper destruction of allocated render textures in the `GPUPixel` backend.
- `LeakyRelu` now supports `alpha` greater than 1 on all platforms.
- Fixed Async behaviour for CPU tensor data.
