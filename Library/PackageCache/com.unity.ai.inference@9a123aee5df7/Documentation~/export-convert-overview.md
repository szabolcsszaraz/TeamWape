---
uid: sentis-export-convert-overview
---
# Export a model from a machine learning framework

Sentis currently supports model files in the following formats:
- ONNX (Open Neural Network Exchange)
- LiteRT (formerly TensorFlow Lite)
- PyTorch
- .sentis (Sentis serialized format)

If your model is not in one of these formats, you must convert it.

Use the following table to determine the appropriate export workflow based on your machine learning framework.

|Machine learning framework|Export workflow|
|-|-|
|PyTorch|[Export and convert to PyTorch](xref:sentis-export-convert-torch)<br>[Export and convert to ONNX](xref:sentis-export-convert-onnx)|
|TensorFlow, Keras, Tensorflow.js|[Export and convert to LiteRT](xref:sentis-export-convert-litert)<br>[Export and convert to ONNX](xref:sentis-export-convert-onnx)|

## Additional resources

- [Profile a model](xref:sentis-profile-a-model)
- [Export an ONNX file from a machine learning framework](xref:sentis-export-convert-onnx)
- [Export a LiteRT file from a machine learning framework](xref:sentis-export-convert-litert)
- [Export a PyTorch file from a machine learning framework](xref:sentis-export-convert-torch)
