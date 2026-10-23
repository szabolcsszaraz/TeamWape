---
uid: sentis-export-convert-litert
---
# Export and convert a file to LiteRT

The following sections describe how to export models in LiteRT format and convert models from other formats to LiteRT.

## Export a LiteRT file from a machine learning framework

You can export a model from most machine learning frameworks in LiteRT format.

To export a PyTorch model to LiteRT, refer to [Convert PyTorch models to LiteRT](https://ai.google.dev/edge/litert/models/convert_pytorch) on the LiteRT website.

For more information about LiteRT compatibility, refer to [Import a model file](xref:sentis-import-a-model-file).

## Export TensorFlow files to LiteRT

Exporting files from TensorFlow involves two key file types: SavedModel and Checkpoints.

### SavedModel files

TensorFlow saves models in SavedModel files, which contain a complete TensorFlow program, including trained parameters and computation. SavedModels have the `.pb` file extension. For more information on SavedModels, refer to [Using the SavedModel format](https://www.tensorflow.org/guide/saved_model) (TensorFlow documentation).
You can convert TensorFlow models to TFLite format using the [TensorFlow Lite Converter (TFLiteConverter)](https://www.tensorflow.org/api_docs/python/tf/lite/TFLiteConverter).

### Checkpoints

[Checkpoints](https://www.tensorflow.org/guide/checkpoint) (TensorFlow documentation) contain only the model parameters.

Checkpoints in TensorFlow can consist up of two file formats:

- A file to store the graph, with the extension `.ckpt.meta`.
- A file to store the weights, with the extension `.ckpt`.

To convert a checkpoint to LiteRT:
1. Restore the model in TensorFlow with both the graph and weights file types.
2. If only the `.ckpt` file is available, locate the Python code that defines the model and loads the weights.
3. Convert the restored model using the [TensorFlow Lite Converter (TFLiteConverter)](https://www.tensorflow.org/api_docs/python/tf/lite/TFLiteConverter).

## Convert PyTorch files to LiteRT

Use this information to understand how to convert PyTorch files to the LiteRT format.

### Model files

PyTorch model files usually have the `.pt2` file extension.

To export a model file to LiteRT, follow these steps:
1. [Load the model](https://pytorch.org/tutorials/beginner/saving_loading_models.html) in Python.
2. [Convert the model](https://ai.google.dev/edge/litert/models/convert_pytorch) as a LiteRT file.

If your `.pt2` file doesn't contain the model graph, you must find the Python code that constructs the model and loads in the weights.

###  Checkpoints

You can create [Checkpoints](https://pytorch.org/docs/stable/checkpoint.html) in PyTorch to save the state of your model at any instance of time. Checkpoint files are usually denoted with the `.tar` or `.pth` extension.

To convert a checkpoint file to LiteRT, you must find the Python code that constructs the model and loads in the weights.

## Additional resources

- [Supported LiteRT operators](xref:sentis-supported-litert-operators)
- [Profile a model](xref:sentis-profile-a-model)
