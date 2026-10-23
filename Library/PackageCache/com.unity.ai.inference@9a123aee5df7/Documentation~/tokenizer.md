---
uid: sentis-tokenizer
---
# Tokenize text for input

Use the built-in [`tokenizer`](xref:Unity.InferenceEngine.Tokenization.ITokenizer) to convert text into numerical tokens to use as input for models that process text.

> [!NOTE]
> The tokenizer is optional for Sentis. You can provide inputs from other sources if you prefer.

## Compatibility

The tokenizer is compatible with the Hugging Face `tokenizers` Python library. To configure it, use the `tokenizer.json` file available in most Hugging Face model repositories.
It is an open framework as you can provide your own implementations.

## Tokenization workflow

A tokenizer processes text through several steps. Not all steps are required for every model:

### Normalization

Transforms the input string, such as replacing characters or applying Unicode normalization. This step outputs a new `string`. For more information, visit [`normalizers`](xref:Unity.InferenceEngine.Tokenization.Normalizers.INormalizer).

### Pre-tokenization

Splits the normalized `string` into smaller parts for token conversion. For more information, refer to [`pre-tokenizers`](xref:Unity.InferenceEngine.Tokenization.PreTokenizers.IPreTokenizer).

### Models (token-to-ID conversion)

Maps each substring to a unique `integer` ID. For more information, visit [`models`](xref:Unity.InferenceEngine.Tokenization.PreTokenizers.IPreTokenizer).

### Truncation

Enforces maximum input length by splitting or trimming token sequences. For more information, view [`truncation`](xref:Unity.InferenceEngine.Tokenization.Truncators.ITruncator).

### Padding

Adds tokens to ensure sequences have a fixed length when required by the model. For more information, refer to [`padding`](xref:Unity.InferenceEngine.Tokenization.Padding.IPadding).

### Post processors

Adds special tokens, such as separators or markers, to prepare the sequence for the model. For more information, visit [`post processors`](xref:Unity.InferenceEngine.Tokenization.PostProcessors.IPostProcessor).

### Decoders

Converts token IDs back into text after inference. Decoding is separate from the encoding steps and is only used when interpreting model outputs. For more information, refer to [`decoders`](xref:Unity.InferenceEngine.Tokenization.Decoders.IDecoder).

## Create a tokenizer

At minimum, tokenization requires token-to-ID conversion. Most text-based models also require additional steps such as, normalization, pre-tokenization, or padding.

Sentis provides the following sample implementation through the **Package Manager**.

### Encode input

After initialization, the tokenizer converts text inputs into sequences of IDs that you can pass to Sentis.

### Decode output

For text-based models, use the same tokenizer to decode the generated IDs back into readable text.

### Manual initialization sample code

The following sample code demonstrates manual initialization:

```cs
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.InferenceEngine;
using Unity.InferenceEngine.Tokenization;
using Unity.InferenceEngine.Tokenization.Decoders;
using Unity.InferenceEngine.Tokenization.Mappers;
using Unity.InferenceEngine.Tokenization.Normalizers;
using Unity.InferenceEngine.Tokenization.Padding;
using Unity.InferenceEngine.Tokenization.PostProcessors;
using Unity.InferenceEngine.Tokenization.PostProcessors.Templating;
using Unity.InferenceEngine.Tokenization.PreTokenizers;
using Unity.InferenceEngine.Tokenization.Truncators;
using UnityEngine;

class TokenizerSample : MonoBehaviour
{
    static Tensor<int> Encode(ITokenizer tokenizer, string input)
    {
        // Generates the sequence
        var encoding = tokenizer.Encode(input);

        // Then you can use the encoding to generate your tensors.

        // Gets this ids
        // Other masks or available, like:
        // - attention
        // - type ids
        // - special mask.
        int[] ids = encoding.GetIds().ToArray();

        // Create a 3D tensor shape
        TensorShape shape = new TensorShape(1, 1, ids.Length);

        // Create a new tensor from the array
        return new Tensor<int>(shape, ids);
    }

    static string Decode(ITokenizer tokenizer, Tensor<int> tensor)
    {
        var ids = tensor.DownloadToArray();
        return tokenizer.Decode(ids);
    }

    static Dictionary<string, int> BuildVocabulary()
    {
        // This stub method returns a legitimate string to id mapping for the tokenizer.
        // It is usually built from a large configuration JSON file.
        return new Dictionary<string, int>();
    }

    static TokenConfiguration[] GetAddedTokens()
    {
        // This stub method returns a legitimate collection of token configuration.
        // Token configuration is the Hugging Face equivalent of added token.
        return Array.Empty<TokenConfiguration>();
    }

    /// This sample initializes a tokenizer based on All MiniLM L6 v2.
    public ITokenizer CreateTokenizer()
    {
        var vocabulary = BuildVocabulary();
        var addedTokens = GetAddedTokens();

        // Central step of the tokenizer
        var mapper = new WordPieceMapper(vocabulary, "[UNK]", "##", 100);


        // Preliminary steps of the tokenization:
        // - normalization (transforms the input string)
        // - pre-tokenization (splits the input string)

        var normalizer = new BertNormalizer(
            cleanText: true,
            handleCjkChars: true,
            stripAccents: null,
            lowerCase: true);

        var preTokenizer = new BertPreTokenizer();


        // Final steps of tokenization:
        // - truncation (splits the token sequences)
        // - post-processing (decorates the token sequences)
        // - padding (adds tokens to match a sequence size).

        var truncator = new LongestFirstTruncator(new RightDirectionRangeGenerator(), 128, 0);

        var clsId = addedTokens.Where(tc => tc.Value == "[CLS]").Select(tc => tc.Id).FirstOrDefault();
        var sepId = addedTokens.Where(tc => tc.Value == "[SEP]").Select(tc => tc.Id).FirstOrDefault();
        var padId = addedTokens.Where(tc => tc.Value == "[PAD]").Select(tc => tc.Id).FirstOrDefault();

        var postProcessor = new TemplatePostProcessor(
          new(Template.Parse("[CLS]:0 $A:0 [SEP]:0")),
          new(Template.Parse("[CLS]:0 $A:0 [SEP]:0 $B:1 [SEP]:1")),
          new (string, int)[] { ("[CLS]", clsId), ("[SEP]", sepId) });

        var padding = new RightPadding(
          new FixedPaddingSizeProvider(128),
          new Token(padId, "[PAD]"));


        // Decoding.

        var decoder = new WordPieceDecoder("##", true);


        // Creates the tokenizer from all the components
        // initialized above.

        return new Tokenizer(
            mapper,
            normalizer: normalizer,
            preTokenizer: preTokenizer,
            truncator: truncator,
            postProcessor: postProcessor,
            paddingProcessor: padding,
            decoder: decoder,
            addedVocabulary: addedTokens);
    }
}
```

## Create custom component implementations

You can use you own implementations as you pass you own instances when initializing it.
All you have to do is implementation the components interfaces:

- [`model`](xref:Unity.InferenceEngine.Tokenization.Mappers.IMapper)
- [`normalizer`](xref:Unity.InferenceEngine.Tokenization.Normalizers.INormalizer)
- [`pre-tokenizer`](xref:Unity.InferenceEngine.Tokenization.PreTokenizers.IPreTokenizer)
- [`post-processor`](xref:Unity.InferenceEngine.Tokenization.PostProcessors.IPostProcessor)
- [`padding processor`](xref:Unity.InferenceEngine.Tokenization.Padding.IPadding)
- [`truncation`](xref:Unity.InferenceEngine.Tokenization.Truncators.ITruncator)
- [`decoder`](xref:Unity.InferenceEngine.Tokenization.Decoders.IDecoder)

As an example, the following implementation normalizes an input by reversing the characters and adding a prefix.

```cs
using System.Text;
using Unity.InferenceEngine.Tokenization;
using Unity.InferenceEngine.Tokenization.Normalizers;

class ReversePrefixNormalizer : INormalizer
{
    readonly string m_Prefix;

    public ReversePrefixNormalizer(string prefix = null)
    {
        m_Prefix = prefix ?? string.Empty;
    }

    public SubString Normalize(SubString input)
    {
        var sb = new StringBuilder()
            .Append(m_Prefix);

        for(var i = input.Length - 1; i >= 0; --i)
            sb.Append(input[i]);

        return sb.ToString();
    }
}
```

## Hugging Face JSON configuration parsing

The tokenizer comes with a compatibility with Hugging Face JSON parser.
You can parse the `string` configuration to build a `tokenizer` instance.

```cs
using Unity.InferenceEngine.Tokenization;
using Unity.InferenceEngine.Tokenization.Parsers.HuggingFace;
using UnityEngine;

class TokenizerParsingSample : MonoBehaviour
{
    /// <summary>
    ///     References to a JSON file asset.
    /// </summary>
    [SerializeField]
    TextAsset m_JsonConfig;

    /// <summary>
    /// Generated tokenizer.
    /// </summary>
    ITokenizer m_Tokenizer;
    
    /// <summary>
    /// Creates a tokenizer as it reads the referenced <see cref="m_JsonConfig"/>.
    /// </summary>
    /// <returns>
    /// The generated tokenizer.
    /// </returns>
    ITokenizer CreateTokenizer()
    {
        var jsonContent = m_JsonConfig.text;

        // Gets the default parser.
        var parser = HuggingFaceParser.GetDefault();

        return parser.Parse(jsonContent);
    }

    void Start()
    {
        m_Tokenizer = CreateTokenizer();
    }
}
```

### Implement a component builder

The current framework does not support all the components available in the latest version of the Hugging Face python modules `tokenizers`.

When the configuration uses a component that isn't supported by the built-in implementations, you can build it yourself (refer to [Create custom component implementations](#create-custom-component-implementations)).

Then you have to implement a component builder.
This builder must implement [`IComponentBuilder`](xref:Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.IComponentBuilder)

```cs
using System.Data;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Parsers.HuggingFace;

// A builder is decorated with a HfAttribute.
// As we are implementing a builder for a normalizer, HfNormalizer
// is used here.
// The attribute indicates the type of the normalizer.
// This is the value stored in the "normalizer->type" field.
//
// The config object would look like the following:
// {
//   "type": "ReversePrefix",
//   "prefix": "_"
// }

[HfNormalizer("ReversePrefix")]
class ReverseNormalizerBuilder : IComponentBuilder<INormalizer>
{
    // The field in the JSON object.
    const string k_PrefixField = "prefix";

    // Gets the prefix from JSON configuration.
    static string GetPrefix(JToken parameters)
    {
        if(parameters.Type != JTokenType.Object)
            throw new DataException("Parameter must be an object");

        var parameterObj = (JObject)parameters;

        var found = parameterObj.TryGetValue("prefix", out var prefixToken);
        if (!found)
            return null;

        if (prefixToken.Type != JTokenType.String)
            throw new DataException("Prefix must be a string");

        return prefixToken.Value<string>();
    }

    public INormalizer Build(JToken parameters, HuggingFaceParser parser)
    {
        var prefix = GetPrefix(parameters);
        return new CustomNormalizer(prefix);
    }
}
```
## Additional resources

* [Samples](xref:sentis-package-samples)