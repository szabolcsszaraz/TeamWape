using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Decoders;
using Unity.InferenceEngine.Tokenization.Mappers;
using Unity.InferenceEngine.Tokenization.Normalizers;
using Unity.InferenceEngine.Tokenization.Padding;
using Unity.InferenceEngine.Tokenization.PostProcessors;
using Unity.InferenceEngine.Tokenization.PreTokenizers;
using Unity.InferenceEngine.Tokenization.Truncators;
using UnityEngine;
#if UNITY_6000_5_OR_NEWER
using UnityEngine.Assemblies;
#endif

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace
{
    static class HuggingFaceHelper
    {
        public static readonly IReadOnlyDictionary<string, Lazy<IComponentBuilder<IDecoder>>> Decoders;
        public static readonly IReadOnlyDictionary<string, Lazy<IComponentBuilder<IMapper>>> Mappers;
        public static readonly IReadOnlyDictionary<string, Lazy<IComponentBuilder<INormalizer>>> Normalizers;
        public static readonly IReadOnlyDictionary<string, Lazy<IComponentBuilder<IPadding>>> PaddingProcessor;
        public static readonly IReadOnlyDictionary<string, Lazy<IComponentBuilder<IPostProcessor>>> PostProcessors;
        public static readonly IReadOnlyDictionary<string, Lazy<IComponentBuilder<IPreTokenizer>>> PreTokenizers;
        public static readonly IReadOnlyDictionary<string, Lazy<IComponentBuilder<ITruncator>>> Truncators;

        static HuggingFaceHelper()
        {
            var hfTypes = GetHfTypes();

            Decoders = GetComponentTypes<HfDecoderAttribute, IDecoder>(hfTypes, attr => attr.Type);
            Mappers = GetComponentTypes<HfModelAttribute, IMapper>(hfTypes, attr => attr.Type);
            Normalizers = GetComponentTypes<HfNormalizerAttribute, INormalizer>(hfTypes, attr => attr.Type);
            PaddingProcessor = GetComponentTypes<HfPaddingAttribute, IPadding>(hfTypes, attr => attr.Strategy);
            PostProcessors = GetComponentTypes<HfPostProcessorAttribute, IPostProcessor>(hfTypes, attr => attr.Type);
            PreTokenizers = GetComponentTypes<HfPreTokenizerAttribute, IPreTokenizer>(hfTypes, attr => attr.Type);
            Truncators = GetComponentTypes<HfTruncationAttribute, ITruncator>(hfTypes, attr => attr.Strategy);

            return;

            IReadOnlyDictionary<string, Lazy<IComponentBuilder<TComponent>>> GetComponentTypes<
                TComponentAttr,
                TComponent>(IEnumerable<Type> types,
                Func<TComponentAttr, string> identify)
                where TComponentAttr : HfAttribute
            {
                var filtered = types.Select(type => (type,
                        attr: type.GetCustomAttributes(typeof(TComponentAttr), false)
                            .FirstOrDefault() as TComponentAttr))
                    .Where(t => t.attr != null)
                    .Where(t => typeof(IComponentBuilder<TComponent>).IsAssignableFrom(t.type))
                    .ToArray();

                var map = new Dictionary<string, Lazy<IComponentBuilder<TComponent>>>();
                foreach (var (type, attr) in filtered)
                {
                    var idenfifier = identify(attr);
                    if (map.ContainsKey(idenfifier))
                    {
                        Debug.LogWarning(
                            $"An instance of {nameof(IComponentBuilder<TComponent>)} already exists for {idenfifier}");
                        continue;
                    }

                    var lazy = new Lazy<IComponentBuilder<TComponent>>(() =>
                    {
                        IComponentBuilder<TComponent> instance;
                        try
                        {
                            instance =
                                Activator.CreateInstance(type) as IComponentBuilder<TComponent>;
                        }
                        catch (Exception e)
                        {
                            throw new(
                                $"Cannot instantiate component builder of type {type.Name} for {idenfifier}",
                                e);
                        }
                        return instance;
                    });

                    map[idenfifier] = lazy;
                }

                return new ReadOnlyDictionary<string, Lazy<IComponentBuilder<TComponent>>>(map);
            }
        }

        static IReadOnlyCollection<Type> GetHfTypes()
        {
            var mainAssembly = typeof(ITokenizer).Assembly;

            var assemblies = new[]
                {
                    mainAssembly
                }
#if UNITY_6000_5_OR_NEWER
                .Concat(CurrentAssemblies.GetLoadedAssemblies()
#else
                .Concat(AppDomain.CurrentDomain.GetAssemblies()
#endif
                    .Where(assembly => assembly != mainAssembly));

            var hfTypes = new List<Type>();
            foreach (var assembly in assemblies)
            {
                try
                {
                    var assemblyTypes = assembly.GetTypes().Where(type =>
                        type.GetCustomAttributes(typeof(HfAttribute), true).Any());

                    hfTypes.AddRange(assemblyTypes);
                }
                catch (ReflectionTypeLoadException e)
                {
                    Debug.LogWarning(nameof(ReflectionTypeLoadException));

                    var loadedTypes = e.Types
                        .Where(type => type != null)
                        .Where(type => type.GetCustomAttributes(typeof(HfAttribute), true).Any());

                    hfTypes.AddRange(loadedTypes);
                }
            }

            return hfTypes;
        }

        static IComponentBuilder<TComponent> GetInstance<TComponent>(
            IReadOnlyDictionary<string, Lazy<IComponentBuilder<TComponent>>> source, string name)
        {
            return source.TryGetValue(name, out var lazy)
                ? lazy.Value
                : throw new KeyNotFoundException(
                    $"No builder of {typeof(TComponent).Name} with name {name}");
        }

        public static Dictionary<string, int> BuildVocabulary(JObject vocab)
        {
            var output = new Dictionary<string, int>();

            foreach (var (value, id) in vocab)
                output[value] = id?.Value<int>()
                    ?? throw new DataException($"No id for value {value}");

            return output;
        }

        public static MergePair[] BuildMerges(JArray mergesJson)
        {
            if (mergesJson is null ||  mergesJson.Count == 0)
                return Array.Empty<MergePair>();

            var test = mergesJson[0];

            if (test.Type == JTokenType.String)
            {
                return mergesJson
                    .Select(t => t.Value<string>())
                    .Select(s => s.Split(" "))
                    .Select(sa => new MergePair(sa[0], sa[1]))
                    .ToArray();
            }

            if (test.Type == JTokenType.Array)
            {
                return mergesJson
                    .Select(t => t as JArray)
                    .Select(pair => new MergePair(pair[0].Value<string>(), pair[1].Value<string>()))
                    .ToArray();
            }

            throw new DataException($"Unexpected type {test.Type}");
        }
    }
}
