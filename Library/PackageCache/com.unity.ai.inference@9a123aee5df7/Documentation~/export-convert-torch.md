---
uid: sentis-export-convert-torch
---
# Export and convert a file to PyTorch

Use this information to export models in PyTorch ExportedProgram format.

## Export a PyTorch file from the PyTorch machine learning framework

You can export a model from the PyTorch machine learning framework to a PyTorch ExportedProgram format file. Refer to the following documentation:
- [Saving and Loading Models](https://docs.pytorch.org/tutorials/beginner/saving_loading_models.html#saving-an-exported-program) on the PyTorch website.
- [torch.export Tutorial](https://docs.pytorch.org/tutorials/intermediate/torch_export_tutorial.html) on the PyTorch website.

### PyTorch Model files

PyTorch model files usually have the `.pt2` file extension.
Sentis supports the [Core ATen IR](https://docs.pytorch.org/docs/stable/torch.compiler_ir.html#core-aten-ir), a collection of about 180 operators. 

To export a PyTorch model file, refer to the links in the following instructions:

1. [Load the model](https://pytorch.org/tutorials/beginner/saving_loading_models.html#saving-an-exported-program) in Python. Alternatively, [create your own model](https://docs.pytorch.org/tutorials/beginner/introyt/modelsyt_tutorial.html) in PyTorch.
2. [Declare dynamic shapes](https://docs.pytorch.org/tutorials/intermediate/torch_export_tutorial.html#basic-concepts-symbols-and-guards) if needed. If the input batch size is variable, mark the batch dimension as dynamic.
3. [Decompose the model](https://docs.pytorch.org/tutorials/intermediate/torch_export_tutorial.html#ir-decompositions) to Core ATen IR operators.
4. Clamp large integer values to `int32` min and max values.
5. [Save the model](https://docs.pytorch.org/tutorials/beginner/saving_loading_models.html#saving-an-exported-program) as a PyTorch ExportedProgram file.

If your `.pt2` file doesn't contain the model graph, you must find the Python code that constructs the model and loads in the weights.

#### Export a Hugging Face model

Many pre-trained models are available on [Hugging Face](https://huggingface.co/). You can export these models to PyTorch ExportedProgram format using a class.

The following example shows how to export the Whisper decoder from Hugging Face. This example was tested with Python 3, PyTorch 2.9.1, and Transformers 4.48.0.

```python
import torch
import torch.nn as nn
from transformers import WhisperForConditionalGeneration

# Create a class for the model
class WhisperDecoderWrapper(nn.Module):
    def __init__(self):
        super().__init__()
        model = WhisperForConditionalGeneration.from_pretrained("openai/whisper-tiny")
        self.decoder = model.model.decoder.eval()

    def forward(self, input_ids, encoder_hidden_states, attention_mask):
        out = self.decoder(
            input_ids=input_ids,
            encoder_hidden_states=encoder_hidden_states,
            attention_mask=attention_mask,
            use_cache=False,
        )
        return out.last_hidden_state

# Create model instance and example inputs
model = WhisperDecoderWrapper().eval()
example_inputs = (
    torch.zeros((1, 5), dtype=torch.long),      # input_ids
    torch.randn((1, 50, 384)),                  # encoder_hidden_states
    torch.ones((1, 5), dtype=torch.long),       # attention_mask
)

# Export the model
exported = torch.export.export(model, example_inputs)

# Decompose to Core ATen IR
decomp_table = torch.export.default_decompositions()
decomposed = exported.run_decompositions(decomp_table)

# Clamp large integer values to int32 range
INT32_MIN = torch.iinfo(torch.int32).min
INT32_MAX = torch.iinfo(torch.int32).max

def clamp(obj):
    return max(INT32_MIN, min(INT32_MAX, obj)) if isinstance(obj, int) else obj

for node in decomposed.graph_module.graph.nodes:
    node.args = tuple(clamp(arg) for arg in getattr(node, 'args', ()))

# Save the exported model
torch.export.save(decomposed, "whisper_decoder.pt2")
```

###  Checkpoints

You can create [Checkpoints](https://pytorch.org/docs/stable/checkpoint.html) in PyTorch to save the state of your model at any instance of time. Checkpoint files usually have `.tar` or `.pth` extension.

To convert a checkpoint file to PyTorch, find the Python code that constructs the model and loads in the weights, then export the model as previously described.

## Additional resources

- [Supported PyTorch operators](xref:sentis-supported-torch-export-operators)
- [Profile a model](xref:sentis-profile-a-model)
