using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor.ShaderApiReflection;
using UnityEditor;
using GUID = UnityEngine.GUID;
using Unity.GraphAuthoring.Editor.ProviderSystem.Hints;

namespace Unity.GraphAuthoring.Editor.ProviderSystem
{
    // TODO (SVFXG-914): Hints processing and representation improvements planned.
    internal static class ShaderReflectionUtils
    {
        // All registered strong hints and derived lookup maps — built once via TypeCache, reused across all calls.
        static IStrongHint[]                                           s_strongHints;
        static Dictionary<string, IStrongHint>                         s_hintsByKey;
        static Dictionary<string, List<(string StrongHintKey, int Priority)>> s_synonymMap;

        static IStrongHint[] GetStrongHints()
        {
            if (s_strongHints != null)
                return s_strongHints;

            var discovered = TypeCache.GetTypesWithAttribute<StrongHintAttribute>();
            var strongHints = new List<IStrongHint>();
            foreach (var type in discovered)
            {
                if (!type.IsAbstract && !type.IsInterface && typeof(IStrongHint).IsAssignableFrom(type))
                    strongHints.Add((IStrongHint)Activator.CreateInstance(type, nonPublic: true));
            }
            s_strongHints = strongHints.ToArray();

            // Build synonym map from hint metadata — cached alongside s_strongHints.
            s_hintsByKey = new Dictionary<string, IStrongHint>();
            s_synonymMap = new Dictionary<string, List<(string StrongHintKey, int Priority)>>();

            foreach (var hint in s_strongHints)
            {
                s_hintsByKey[hint.Key] = hint;

                int priority = 0;
                if (!s_synonymMap.TryAdd(hint.Key, new() { (hint.Key, priority) }))
                    s_synonymMap[hint.Key].Add((hint.Key, priority));

                if (!hint.AllowDisqualifiedSynonyms)
                    continue;

                var alternates = new List<string>(DisqualifyKey(hint.Key));
                if (hint.Synonyms != null)
                    foreach (var synonym in hint.Synonyms)
                        alternates.AddRange(DisqualifyKey(synonym, true));

                foreach (var alternate in alternates)
                {
                    priority++;
                    if (!s_synonymMap.TryAdd(alternate, new() { (hint.Key, priority) }))
                        s_synonymMap[alternate].Add((hint.Key, priority));
                }
            }

            return s_strongHints;
        }

        readonly struct FieldHintContext : IProvider<IShaderFunction>
        {
            readonly IProvider m_real;
            public string ProviderKey => m_real?.ProviderKey ?? "";
            public GUID AssetID => m_real?.AssetID ?? default;
            public IShaderFunction Definition { get; }
            public IProvider Clone() => this;

            internal FieldHintContext(IProvider real, IShaderFunction contextFunc)
            {
                m_real = real;
                Definition = contextFunc;
            }
        }

        internal static bool TryResolve(string providerKey, GUID assetId, out ReflectedFunction function, string version = null)
        {
            var source = AssetDatabase.LoadAssetByGUID<ShaderInclude>(assetId)?.Reflection;

            function = default;
            if (source == null)
                return false;

            ReflectedFunction firstMatch = default;
            bool hasFirstMatch = false;

            foreach (var func in source.ReflectedFunctions)
            {
                string key = ShaderObjectUtils.EvaluateProviderKey(func.Hints, BuildUnresolvedFunction(func));
                if (providerKey != key)
                    continue;

                // When no version disambiguation is requested, first match wins.
                if (string.IsNullOrEmpty(version))
                {
                    function = func;
                    return true;
                }

                // Version disambiguation: prefer the entry whose sg:Version matches exactly.
                func.Hints.TryGetValue(Func.kVersion, out var funcVersion);
                if (funcVersion == version)
                {
                    function = func;
                    return true;
                }

                if (!hasFirstMatch)
                {
                    firstMatch = func;
                    hasFirstMatch = true;
                }
            }

            if (hasFirstMatch)
            {
                function = firstMatch;
                return true;
            }

            return false;
        }

        // Pass 1: build a function with unresolved (hint-free) fields, used as context
        // for cross-parameter hint validation and for provider key lookup.
        static IShaderFunction BuildUnresolvedFunction(ReflectedFunction refFunc)
        {
            var returnType = ToShaderType(refFunc.ReturnTypeName);
            List<IShaderField> fields = new();
            foreach (var param in refFunc.Parameters)
            {
                var type = ToShaderType(param.TypeName);
                bool isInput = param.DirectionFlags == ReflectedParameter.Direction.In || param.DirectionFlags == ReflectedParameter.Direction.InOut;
                bool isOutput = param.DirectionFlags == ReflectedParameter.Direction.Out || param.DirectionFlags == ReflectedParameter.Direction.InOut;
                fields.Add(new ShaderField(param.Name, isInput, isOutput, type, null));
            }
            return new ShaderFunction(refFunc.Name, refFunc.EnclosingNamespace, fields, returnType, refFunc.BodyText, null);
        }

        internal static IShaderType ToShaderType(string typeName)
            => new ShaderType(typeName);

        internal static IShaderFunction ToShaderFunction(ReflectedFunction refFunc, IProvider provider = null)
        {
            var returnType = ToShaderType(refFunc.ReturnTypeName);

            // Pass 1: unresolved fields — gives cross-parameter hints (e.g. Linkage)
            // a fully populated sibling list to validate against.
            var unresolvedFunc = BuildUnresolvedFunction(refFunc);
            var fieldContext = new FieldHintContext(provider, unresolvedFunc);

            // Pass 2: resolve field hints with the context function available.
            List<IShaderField> resolvedFields = new();
            foreach (var param in refFunc.Parameters)
            {
                var type = ToShaderType(param.TypeName);
                bool isInput = param.DirectionFlags == ReflectedParameter.Direction.In || param.DirectionFlags == ReflectedParameter.Direction.InOut;
                bool isOutput = param.DirectionFlags == ReflectedParameter.Direction.Out || param.DirectionFlags == ReflectedParameter.Direction.InOut;
                var tempField = new ShaderField(param.Name, isInput, isOutput, type, null);
                var fieldHints = ResolveRawHints(param.Hints, tempField, fieldContext);
                resolvedFields.Add(new ShaderField(param.Name, isInput, isOutput, type, fieldHints));
            }

            // Resolve function hints using the final resolved fields.
            var tempFunc = new ShaderFunction(refFunc.Name, refFunc.EnclosingNamespace, resolvedFields, returnType, refFunc.BodyText, null);
            var funcHints = ResolveRawHints(refFunc.Hints, tempFunc, provider);

            return new ShaderFunction(refFunc.Name, refFunc.EnclosingNamespace, resolvedFields, returnType, refFunc.BodyText, funcHints);
        }

        // Resolves a raw string hint dict against all registered hints, returning
        // a collection of resolved IStrongHint instances. Handles synonym matching,
        // conflict detection, and AlwaysProcess fallbacks.
        static IReadOnlyCollection<IStrongHint> ResolveRawHints(
            IReadOnlyDictionary<string, string> rawHints,
            IDefinition obj,
            IProvider provider)
        {
            var hints = GetStrongHints();
            var hintsByKey = s_hintsByKey;
            var synonymMap = s_synonymMap;
            var result = new List<IStrongHint>();

            // Match raw hint keys to hint keys via synonym map.
            var foundHints = new Dictionary<string, (string Synonym, int Priority)>();
            var conflictCases = new Dictionary<string, HashSet<string>>();
            var conflictedHints = new HashSet<string>();

            foreach (var rawHintKey in rawHints.Keys)
            {
                if (!synonymMap.TryGetValue(rawHintKey, out var matches))
                    continue;

                foreach (var match in matches)
                {
                    if (!foundHints.TryAdd(match.StrongHintKey, (rawHintKey, match.Priority)))
                    {
                        if (match.Priority < foundHints[match.StrongHintKey].Priority)
                            foundHints[match.StrongHintKey] = (rawHintKey, match.Priority);
                    }

                    if (hintsByKey[match.StrongHintKey].Conflicts != null)
                        foreach (var conflictClass in hintsByKey[match.StrongHintKey].Conflicts)
                            if (!conflictCases.TryAdd(conflictClass, new HashSet<string>() { match.StrongHintKey }))
                                conflictCases[conflictClass].Add(match.StrongHintKey);
                }
            }

            // Emit conflict messages and collect conflicted hint keys.
            foreach (var caseKV in conflictCases)
            {
                if (caseKV.Value.Count == 1)
                    continue;

                conflictedHints.UnionWith(caseKV.Value);

                StringBuilder sb = new();
                bool first = true;
                sb.Append($"Conflicting hints of class '{caseKV.Key}' found, ignoring: ");
                foreach (var conflictKey in caseKV.Value)
                {
                    if (!first) sb.Append(", ");
                    sb.Append($"'{conflictKey}'");
                    first = false;
                }
                result.Add(new MessageHint(caseKV.Key, sb.ToString()));
            }

            // Resolve each hint.
            foreach (var hint in hints)
            {
                if (conflictedHints.Contains(hint.Key))
                    continue;

                string synonymUsed = null;
                string rawHintValue = null;
                bool found;
                bool shouldProcess = hint.AlwaysProcess;

                if (found = foundHints.TryGetValue(hint.Key, out var rawHintData))
                {
                    shouldProcess = true;
                    synonymUsed = rawHintData.Synonym;
                    rawHintValue = rawHints[synonymUsed];
                }

                if (!shouldProcess)
                    continue;

                var instance = (IStrongHint)Activator.CreateInstance(hint.GetType(), nonPublic: true);
                instance.Initialize(found, rawHintValue, obj, provider, synonymUsed, rawHints);
                if (instance.IsResolved || instance.Message != null)
                    result.Add(instance);
            }

            return result;
        }

        // eg. "unity:engine:sg:HintKey" => "engine:sg:HintKey", "sg:HintKey", "HintKey"
        static IEnumerable<string> DisqualifyKey(string key, bool inclusive = false)
        {
            if (inclusive)
                yield return key;

            for (int i = 0; i < key.Length - 1; ++i)
            {
                if (key[i] == ':')
                {
                    int j;
                    for (j = i + 1; j < key.Length && key[j] == ':'; ++j);
                    string candidate = key[j..];
                    if (!string.IsNullOrEmpty(candidate))
                        yield return candidate;
                }
            }
        }
    }
}
