---
uid: sentis-quantize-a-model
---
# Quantize a Model

Sentis imports model constants and weights as 32-bit values. To reduce the model's storage size on disk and memory, use model quantization.

Quantization represents the weight values in a lower-precision format. At runtime, Sentis converts these values back to a higher-precision format before processing the operations.

## Quantization types

Sentis currently supports the following quantization types.

| Quantization type | Bits per value | Description |
| ----------------- |--------------- | ----------- |
| None              | 32-bit         | Stores the value in full precision. |
| Float16           | 16-bit         | Converts the values to a 16-bit floating point. Often preserves accuracy close to the original model. |
| Uint8             | 8-bit          | Linearly quantizes values between the highest and lowest range. Might significantly impact accuracy depending on the model. |

A lower bit count per value decreases your model’s disk and memory usage without significantly affecting inference speed.

> [!NOTE]
> Sentis only quantizes float weights used as inputs to specific operations, such as Dense, MatMul, or Conv. Integer constants remain unchanged.

The impact of quantization on model accuracy varies depending on the model type. The best way to evaluate model quantization is to test it and compare performance and accuracy.

## Quantizing a loaded model

To quantize a model in code, follow these steps:

1. Use the [`ModelQuantizer`](xref:Unity.InferenceEngine.ModelQuantizer) API to apply quantization to the model.
1. Use the [`ModelWriter`](xref:Unity.InferenceEngine.ModelWriter) API to serialize and save the quantized model to disk.

```
using Unity.InferenceEngine;

void QuantizeAndSerializeModel(Model model, string path)
{
    // Sentis destructively edits the source model in memory when quantizing.
    ModelQuantizer.QuantizeWeights(QuantizationType.Float16, ref model);

    // Serialize the quantized model to a file.
    ModelWriter.Save(path, model);
}
```

## Additional resources

- [Inspect a model](xref:sentis-inspect-a-model)
- [Create a new model](xref:sentis-create-a-new-model)
