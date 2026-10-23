---
uid: sentis-encrypt-a-model
---
# Encrypt a model

Encrypt a model so that only a user with the correct key can read the model description and weights from disk. You can encrypt a Sentis model to disk with the [`ModelWriter`](xref:Unity.InferenceEngine.ModelWriter) and [`ModelLoader`](xref:Unity.InferenceEngine.ModelLoader) APIs.

## Encrypt a model and save to disk

Use the following steps to encrypt and save a model to disk, typically in the Unity Editor before you build and distribute your project:

1. To get a Sentis model, import an ONNX, LiteRT, or PyTorch file or use the Sentis functional API.
2. Create a `Stream` object for the encrypted model with a cryptography API and your encryption key.
3. Call [`ModelWriter.Save`](xref:Unity.InferenceEngine.ModelWriter.Save*) to serialize and encrypt the model to the stream.

> [!NOTE]
> Certain stream types, such as `MemoryStream`, might not support large models over 2 GB.

## Decrypt a model from disk

To decrypt and load a model before you run it, follow these steps:

1. Create a `Stream` object for the encrypted model with a cryptography API and your key.
2. Decrypt and deserialize the model with the [`ModelLoader.Load`](xref:Unity.InferenceEngine.ModelLoader.Load*) method.

## Example: encrypt and decrypt a model with AES

The following code samples demonstrate how to serialize a runtime model to disk, encrypt it with the Advanced Encryption Standard (AES), and then decrypt the encrypted model for inference.

This code sample uses [AES encryption in C#](https://learn.microsoft.com/en-us/dotnet/standard/security/encrypting-data) to encrypt a model.

```
using System.IO;
using System.Security.Cryptography;
using Unity.InferenceEngine;

void SaveModelAesEncrypted(Model model, string path, byte[] key)
{
    // Create a `FileStream` with the path of the encrypted asset
    using FileStream fileStream = new FileStream(path, FileMode.OpenOrCreate);
    using Aes aes = Aes.Create();
    aes.Key = key;

    byte[] iv = aes.IV;

    // Write the initialization vector to the file
    fileStream.Write(iv, 0, iv.Length);

    // Create a `CryptoStream` that writes to the `FileStream`
    using CryptoStream cryptoStream = new CryptoStream(fileStream, aes.CreateEncryptor(), CryptoStreamMode.Write);

    // Serialize the model to the `CryptoStream`
    ModelWriter.Save(cryptoStream, model);
}
```

This code sample uses [AES decryption in C#](https://learn.microsoft.com/en-us/dotnet/standard/security/decrypting-data) to decrypt a model.

```
using System.IO;
using System.Security.Cryptography;
using Unity.InferenceEngine;

Model LoadModelAesEncrypted(string path, byte[] key)
{
    // Create a `FileStream` with the path of the encrypted asset
    using var fileStream = new FileStream(path, FileMode.Open);
    using Aes aes = Aes.Create();

    // Read the initialization vector from the file
    byte[] iv = new byte[aes.IV.Length];
    int numBytesToRead = aes.IV.Length;
    int numBytesRead = 0;
    while (numBytesToRead > 0)
    {
        int n = fileStream.Read(iv, numBytesRead, numBytesToRead);
        if (n == 0) break;

        numBytesRead += n;
        numBytesToRead -= n;
    }

    // Create a `CryptoStream` that reads from the `FileStream`
    using CryptoStream cryptoStream = new CryptoStream(fileStream, aes.CreateDecryptor(key, iv), CryptoStreamMode.Read);

    // Deserialize the model from the `CryptoStream`
    return ModelLoader.Load(cryptoStream);
}
```

For an example, refer to the `Encrypt a model` example in the [sample scripts](xref:sentis-package-samples) for an example.

## Additional resources

* [Supported functional methods](xref:sentis-supported-functional-methods)
* [Supported ONNX operators](xref:sentis-supported-operators)
