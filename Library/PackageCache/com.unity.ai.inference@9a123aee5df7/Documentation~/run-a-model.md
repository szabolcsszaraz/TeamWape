---
uid: sentis-run-a-model
---
# Run a model

After you [create a worker](xref:sentis-create-an-engine), call [`Schedule`](xref:Unity.InferenceEngine.Worker.Schedule*) to run the model.

```
worker.Schedule(inputTensor);
```

The first scheduling of a model within the Unity Editor might be slow as Sentis needs to compile code and shaders, including allocating internal memory. Subsequent runs will be faster due to caching.

It’s a good idea to include a test run when you start the application to help improve the initial load time.

For an example, refer to the `Run a model` sample in the [sample scripts](xref:sentis-package-samples).

## Additional resources

- [Split inference over multiple frames](xref:sentis-split-inference-over-multiple-frames)
- [Sentis models](xref:sentis-models-concept)
- [Create an engine to run a model](xref:sentis-create-an-engine)
- [Profile a model](xref:sentis-profile-a-model)
