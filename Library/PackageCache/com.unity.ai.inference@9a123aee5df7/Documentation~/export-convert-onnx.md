---
uid: sentis-export-convert-onnx
---
# Export and convert a file to ONNX

The following sections describe how to export and convert models to the ONNX format.

## Export an ONNX file from a machine learning framework

Most machine learning frameworks let you to export models in ONNX format.

To export files in ONNX format from common machine learning frameworks, refer to the following documentation:

- [Exporting a model from PyTorch to ONNX](https://docs.pytorch.org/tutorials/beginner/onnx/export_simple_model_to_onnx_tutorial.html) on the PyTorch website.
- [Convert TensorFlow, Keras, Tensorflow.js and Tflite models to ONNX](https://github.com/onnx/tensorflow-onnx) on the ONNX GitHub repository.

> [!NOTE]
> To ensure compatibility with Sentis, set the ONNX opset version to `25` during export. For more information about ONNX compatibility, refer to [import a model file](xref:sentis-import-a-model-file).

## Convert TensorFlow files to ONNX

TensorFlow uses two primary file types for saving models: SavedModel and Checkpoints.

The following sections explain each file type and how to convert them to the ONNX format.

### Model files

TensorFlow saves models in SavedModel files, which contain a complete TensorFlow program, including trained parameters and computation. SavedModels have the `.pb` file extension. For more information on SavedModels, refer to [Using the SavedModel format](https://www.tensorflow.org/guide/saved_model) (TensorFlow documentation).

To generate an ONNX file from a SavedModel, use the [tf2onnx](https://github.com/onnx/tensorflow-onnx) tool. This is a command line tool that works best with full path names.

### Checkpoints

[Checkpoints](https://www.tensorflow.org/guide/checkpoint) (TensorFlow documentation) contain only the parameters of the model.

Checkpoints in TensorFlow can consist up of two file formats:

- A file to store the graph, with the extension `.ckpt.meta`.
- A file to store the weights, with the extension `.ckpt`.

If you have both the graph and weight file types, use the [tf2onnx](https://github.com/onnx/tensorflow-onnx) tool to create an ONNX file.

If you only have the `.ckpt` file, find the Python code that constructs the model and loads in the weights. After that, proceed to export the model to ONNX.

## Convert PyTorch files to ONNX

Use this information to convert PyTorch files to the ONNX format.

### PyTorch Model files

PyTorch model files usually have the `.pt2` file extension.

To export a model file to ONNX, refer to the links in the following instructions:
1. [Load the model](https://pytorch.org/tutorials/beginner/saving_loading_models.html) in Python.
2. [Export the model](https://docs.pytorch.org/tutorials/beginner/onnx/export_simple_model_to_onnx_tutorial.html) as an ONNX file. When you export your model, it's recommended to use Opset `15` or higher.

If your `.pt2` file doesn't contain the model graph, find the Python code that constructs the model and loads in the weights. After that, [export the model to ONNX](https://docs.pytorch.org/tutorials/beginner/onnx/export_simple_model_to_onnx_tutorial.html).

###  Checkpoints

You can create [Checkpoints](https://pytorch.org/docs/stable/checkpoint.html) in PyTorch to save the state of your model at any instance of time. Checkpoint files are usually denoted with the `.tar` or `.pth` extension.

To convert a checkpoint file to ONNX, find the Python code which constructs the model and loads in the weights. After that, proceed to [export the model to ONNX](https://docs.pytorch.org/tutorials/beginner/onnx/export_simple_model_to_onnx_tutorial.html).

## Additional resources

- [Open Neural Network Exchange](https://onnx.ai/)
- [Supported ONNX operators](xref:sentis-supported-operators)
- [Profile a model](xref:sentis-profile-a-model)
- [Convert TensorFlow, Keras, Tensorflow.js and Tflite models to ONNX](https://github.com/onnx/tensorflow-onnx)
