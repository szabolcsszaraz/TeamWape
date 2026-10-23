---
uid: sentis-upgrade-guide
---
# Upgrade to Sentis 2.5 or 2.6

You do not need to take any actions to upgrade your project when upgrading from Sentis 2.4. If you are upgrading from an earlier version please follow the instructions below.

# Upgrade to Sentis 2.4

ONNX model input and output names have been fixed to match the spec exactly. If you use hardcoded input and output names in your code these may also need updating to match the corrected versions.

In very rare cases where a model has been serialized as a .sentis file in versions of Sentis before 2.3, and then loaded and reserialized as a .sentis file in version 2.3, and the model contains the ConvTranspose operator, the model may be corrupted. In this case you will need to use the original .sentis file and reimport.

# Upgrade to Inference Engine 2.3

You do not need to take any actions to upgrade your project when upgrading from Inference Engine 2.2. If you are upgrading from Sentis 2.1 please follow the instructions below.

Inference Engine 2.3 makes extensive use of [source generators](https://learn.microsoft.com/en-us/shows/on-dotnet/c-source-generators). If you're using an older IDE such as Visual Studio 2019, you may need to upgrade to a newer version to continue debugging your project successfully.

# Upgrade to Inference Engine 2.2

To upgrade from Sentis 2.1 to Inference Engine 2.2, follow these steps:

1. Open the **Package Manager** and remove the **Sentis** package from your project.
2. In the **Package Manager**, select **+** > **Add package by name**.
3. Enter `com.unity.ai.inference` and select **Add**.
4. When prompted, use Unity’s automatic API updater or manually replace all instances of `Unity.Sentis` with `Unity.InferenceEngine`.

## Additional resources

* [Get started](xref:sentis-get-started)
* [Create a model](xref:sentis-create-a-model)
* [Run an imported model](xref:sentis-run-an-imported-model)
* [Use Tensors](xref:sentis-use-tensors)
