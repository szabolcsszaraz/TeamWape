---
uid: sentis-split-inference-over-multiple-frames
---
# Split inference over multiple frames

To run a model, one layer at a time, use the [`ScheduleIterable`](xref:Unity.InferenceEngine.Worker.ScheduleIterable) method of the worker. This method creates an `IEnumerator` object.

For example, if a model takes 50 milliseconds to run, to run it in one frame might cause stuttering or low frame rates in gameplay. Instead, to ensure smoother performance, spread the run over 10 frames to allocate 5 milliseconds per frame.

The following code sample runs the model one layer per frame and runs the rest of the `Update` method only after the model finishes.

```
// Set a larger number for faster GPUs
const int k_LayersPerFrame = 20;

IEnumerator m_Schedule;
bool m_Started = false;

void Update()
{
    if (!m_Started)
    {
        // ExecuteLayerByLayer starts the scheduling of the model
        // It returns an IEnumerator to iterate over the model layers and schedule each layer sequentially
        m_Schedule = m_Worker.ScheduleIterable(m_Input);
        m_Started = true;
    }

    int it = 0;
    while (m_Schedule.MoveNext())
    {
        if (++it % k_LayersPerFrame == 0)
            return;
    }

    var outputTensor = m_Worker.PeekOutput() as Tensor<float>;
    var cpuCopyTensor = outputTensor.ReadbackAndClone();
    // cpuCopyTensor is a CPU copy of the output tensor. You can access it and modify it

    // Set this flag to false to run the network again
    m_Started = false;
    cpuCopyTensor.Dispose();
}
```

For an example, refer to the `Run a model a layer at a time` example in the [sample scripts](xref:sentis-package-samples).

## Additional resources

- [Run a model](xref:sentis-run-a-model)
- [Sentis models](xref:sentis-models-concept)
- [Create an engine to run a model](xref:sentis-create-an-engine)
- [Profile a model](xref:sentis-profile-a-model)
