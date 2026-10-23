using System.Collections.Generic;
using System.Text;
using UnityEditor;

namespace Unity.GraphAuthoring.Editor.ProviderSystem.Hints
{
    internal static class Func
    {
        internal const string kProviderKey = "sg:ProviderKey";

        internal const string kReturnDisplayName = "sg:ReturnDisplayName";

        internal const string kSearchTerms = "sg:SearchTerms";
        internal const string kSearchName = "sg:SearchName";
        internal const string kSearchCategory = "sg:SearchCategory";

        internal const string kPrecision = "sg:DynamicPrecision";

        internal const string kGroupKey = "sg:GroupKey";
        internal const string kReturnTooltip = "sg:ReturnTooltip";
        internal const string kDocumentationLink = "sg:HelpURL";

        internal const string kUnityManualBaseUrl  = "https://docs.unity3d.com/Manual/";
        internal const string kPackageDocsBaseUrl  = "https://docs.unity3d.com/Packages/";

        // Lifecycle and versioning.
        internal const string kVersion    = "sg:Version";
        internal const string kDeprecated = "sg:Deprecated";
        internal const string kObsolete   = "sg:Obsolete";

        // Conflict class shared by Deprecated and Obsolete — a function can be one or the other, not both.
        internal const string kLifecycleConflictClass = "sg:Lifecycle";
    }

    [StrongHint] sealed class ReturnDisplayName : DisplayName
    {
        internal ReturnDisplayName() : base(Func.kReturnDisplayName, "Out") { }
    }

    [StrongHint] sealed class ReturnTooltip : Tooltip
    {
        internal ReturnTooltip() : base(Func.kReturnTooltip) { }
    }

    [StrongHint] sealed class Precision : Flag
    {
        // Default constructor — used by TypeCache auto-discovery and HLSL hint processing.
        internal Precision() : base(Func.kPrecision, (string[])null, new string[] { "Precision" }) { }

        // Direct-value constructor — for use in code when precision support is known at construction time.
        internal Precision(bool resolved) : base(Func.kPrecision, null, new string[] { "Precision" }, resolved) { }
    }

    [StrongHint]
    class ProviderKey : IStrongHint<IShaderFunction>
    {
        public string Key => Func.kProviderKey;
        public bool AlwaysProcess => true;
        public bool AllowDisqualifiedSynonyms => false;

        public object Value { get; private set; }
        public string Message { get; private set; }
        public bool IsResolved { get; private set; }

        // Default constructor — used by TypeCache auto-discovery.
        internal ProviderKey() { }

        // Direct-value constructor.
        internal ProviderKey(string resolvedValue)
        {
            Value = resolvedValue;
            IsResolved = true;
        }

        public void Initialize(bool found, string rawValue, IShaderFunction func, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints)
        {
            if (found && !string.IsNullOrWhiteSpace(rawValue))
            {
                Value = rawValue;
            }
            else
            {
                Value = provider?.ProviderKey ?? ShaderObjectUtils.QualifySignature(func);
                if (!found)
                    Message = $"Expected; but none found for '{func.Name}'.";
            }
            IsResolved = true;
        }
    }

    [StrongHint]
    class SearchName : IStrongHint<IShaderFunction>
    {
        public string Key => Func.kSearchName;
        public bool AlwaysProcess => true;

        public object Value { get; private set; }
        public string Message { get; private set; }
        public bool IsResolved { get; private set; }

        // Default constructor — used by TypeCache auto-discovery.
        internal SearchName() { }

        // Direct-value constructor.
        internal SearchName(string resolvedValue)
        {
            Value = resolvedValue;
            IsResolved = true;
        }

        public void Initialize(bool found, string rawValue, IShaderFunction func, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints)
        {
            Value = found ? rawValue : ShaderObjectUtils.QualifySignature(func, false, true);
            IsResolved = true;
        }
    }

    [StrongHint]
    class SearchTerms : IStrongHint<IShaderFunction>
    {
        public string Key => Func.kSearchTerms;
        public bool AlwaysProcess => true;

        public object Value { get; private set; }
        public string Message { get; private set; }
        public bool IsResolved { get; private set; }

        // Default constructor — used by TypeCache auto-discovery.
        internal SearchTerms() { }

        // Direct-value constructor (pre-split array).
        internal SearchTerms(string[] resolvedValue)
        {
            Value = resolvedValue;
            IsResolved = true;
        }

        public void Initialize(bool found, string rawValue, IShaderFunction func, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints)
        {
            Value = found ? HintUtils.LazyTokenString(rawValue) : new string[] { func.Name };
            IsResolved = true;
        }
    }

    [StrongHint]
    class SearchCategory : IStrongHint<IShaderFunction>
    {
        public string Key => Func.kSearchCategory;
        public bool AlwaysProcess => true;
        public IReadOnlyCollection<string> Synonyms { get; } = new[] { "Category" };

        public object Value { get; private set; }
        public string Message { get; private set; }
        public bool IsResolved { get; private set; }

        // Default constructor — used by TypeCache auto-discovery.
        internal SearchCategory() { }

        // Direct-value constructor.
        internal SearchCategory(string resolvedValue)
        {
            Value = resolvedValue;
            IsResolved = true;
        }

        public void Initialize(bool found, string rawValue, IShaderFunction func, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints)
        {
            if (found)
            {
                Value = rawValue;
            }
            else
            {
                StringBuilder catsb = new();
                foreach (var name in func.Namespace)
                    catsb.Append($"/{name}");
                var category = catsb.ToString();
                if (!string.IsNullOrWhiteSpace(category))
                    Value = $"Reflected by Namespace{category}";
                else if (provider != null && provider.AssetID != default)
                    Value = $"Reflected by Path/{AssetDatabase.GUIDToAssetPath(provider.AssetID)}";
                else
                    Value = "Uncategorized";
            }
            IsResolved = true;
        }
    }

    // Resolves to a fully-constructed URL string. Three raw value forms are accepted:
    //
    //   "https://example.com/custom"          — raw URL, passed through unchanged
    //   "com.unity.shadergraph, MyPage"        — package + page, builds package docs URL
    //   "MyPage"                               — Unity Manual page name, builds manual URL
    //
    // Value is always the final URL string, ready for use as-is.
    [StrongHint]
    class HelpURL : IStrongHint<IShaderFunction>
    {
        public string Key => Func.kDocumentationLink;

        public object Value { get; private set; }
        public string Message { get; private set; }
        public bool IsResolved { get; private set; }

        internal HelpURL() { }

        internal HelpURL(string resolvedUrl)
        {
            Value = resolvedUrl;
            IsResolved = true;
        }

        public void Initialize(bool found, string rawValue, IShaderFunction func, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints)
        {
            // TODO(SVFXG-919): Handle manual pages and, for unity owned definitions, fallback based on Group/Provider key.
            if (!found) return;
            if (string.IsNullOrWhiteSpace(rawValue)) { Message = "Expected a URL, a page name, or 'packageName, pageName'."; return; }

            // Raw URL — contains a scheme separator.
            if (rawValue.Contains("://"))
            {
                Value = rawValue.Trim();
                IsResolved = true;
                return;
            }

            var tokens = HintUtils.LazyTokenString(rawValue);
            if (tokens.Length > 0)
            {
                string page = tokens[0];
                UnityEditor.PackageManager.PackageInfo packageInfo = null;

                if (tokens.Length == 2) // package name is explicitly provided.
                {
                    var packageName = tokens[1];
                    packageInfo = UnityEditor.PackageManager.PackageInfo.FindForPackageName(packageName);
                    packageInfo ??= UnityEditor.PackageManager.PackageInfo.FindForPackageName($"com.unity.{packageName}");

                    if (packageInfo == null)
                    {
                        Message = $"Package named '{packageName}' could not be found.";
                        return;
                    }
                }
                else if (provider.AssetID == default) // provider is scripted, so assume the package based on the assembly.
                {
                    packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(provider.GetType().Assembly);
                    if (packageInfo == null)
                    {
                        Message = $"Could not find package for type '{provider.GetType()}'.";
                        return;
                    }
                }
                else // has an assetID, so assume the package of the asset.
                {
                    var path = AssetDatabase.GUIDToAssetPath(provider.AssetID);
                    if (path == null)
                    {
                        Message = $"Failed to resolve valid path from asset '{provider.AssetID}'.";
                        return;
                    }
                    packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
                    if (packageInfo == null)
                    {
                        Message = $"Could not find package for asset at {path}.";
                        return;
                    }
                }

                var name = packageInfo.name;                
                var version = packageInfo.version.Substring(0, packageInfo.version.LastIndexOf('.')); // pkg versions are x.y.z; doc pages are x.y.

                Value = $"{Func.kPackageDocsBaseUrl}{name}@{version}/manual/{page}.html";
                IsResolved = true;
                return;
            }
            Message = $"Expected a URL, a page name, or 'packageName, pageName', but found {tokens.Length} comma-separated tokens.";
        }
    }

    // Version string for a function provider. The raw string is stored in Value; if it parses
    // as a System.Version (e.g. "1.2.3"), ParsedVersion is also populated for ordered comparison.
    [StrongHint]
    class Version : IStrongHint<IShaderFunction>
    {
        // Reserved version string for providers that don't declare an sg:Version hint.
        // IProvider.Version defaults to this value, and Initialize rejects it so a user
        // can't declare it explicitly.
        internal const string kUnversioned = "<unversioned>";

        public string Key => Func.kVersion;

        public object Value { get; private set; }
        public string Message { get; private set; }
        public bool IsResolved { get; private set; }

        // Non-null when Value parses as a dotted numeric version (e.g. "1.2.3").
        internal System.Version ParsedVersion { get; private set; }

        internal Version() { }

        internal Version(string resolvedValue)
        {
            Value = resolvedValue;
            System.Version.TryParse(resolvedValue, out var parsed);
            ParsedVersion = parsed;
            IsResolved = true;
        }

        public void Initialize(bool found, string rawValue, IShaderFunction func, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints)
        {
            if (!found) return;
            if (string.IsNullOrWhiteSpace(rawValue)) { Message = "Expected a version string."; return; }
            if (rawValue == kUnversioned) { Message = $"'{kUnversioned}' is reserved; pick a different version string."; return; }
            Value = rawValue;
            System.Version.TryParse(rawValue, out var parsed);
            ParsedVersion = parsed;
            IsResolved = true;
        }

        // Compares two Version hints. Null is treated as less than any non-null version.
        // Uses ParsedVersion for dotted numeric strings; falls back to ordinal string compare.
        internal static int Compare(Version a, Version b)
        {
            if (a?.ParsedVersion != null && b?.ParsedVersion != null)
                return a.ParsedVersion.CompareTo(b.ParsedVersion);
            return string.Compare(a?.Value as string, b?.Value as string, System.StringComparison.Ordinal);
        }

    }

    // Marks a function as deprecated — still functional, but users should migrate away.
    // Value is a human-readable message explaining why and what to use instead.
    [StrongHint]
    class Deprecated : IStrongHint<IShaderFunction>
    {
        public string Key => Func.kDeprecated;
        public IReadOnlyCollection<string> Conflicts { get; } = new[] { Func.kLifecycleConflictClass };

        public object Value { get; private set; }
        public string Message { get; private set; }
        public MessageType MessageSeverity => MessageType.Warning;
        public bool IsResolved { get; private set; }

        internal Deprecated() { }

        internal Deprecated(string message)
        {
            Value = message;
            IsResolved = true;
        }

        public void Initialize(bool found, string rawValue, IShaderFunction func, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints)
        {
            if (!found) return;
            Value = rawValue; // may be null/empty — that's valid (bare flag)
            Message = string.IsNullOrWhiteSpace(rawValue)
                ? $"'{func.Name}' is deprecated."
                : $"'{func.Name}' is deprecated: {rawValue}";
            IsResolved = true;
        }
    }

    // Marks a function as obsolete — scheduled for removal. Stronger than Deprecated.
    // Value is a human-readable message explaining when it will be removed and what replaces it.
    [StrongHint]
    class Obsolete : IStrongHint<IShaderFunction>
    {
        public string Key => Func.kObsolete;
        public IReadOnlyCollection<string> Conflicts { get; } = new[] { Func.kLifecycleConflictClass };

        public object Value { get; private set; }
        public string Message { get; private set; }
        public MessageType MessageSeverity => MessageType.Error;
        public bool IsResolved { get; private set; }

        internal Obsolete() { }

        internal Obsolete(string message)
        {
            Value = message;
            IsResolved = true;
        }

        public void Initialize(bool found, string rawValue, IShaderFunction func, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints)
        {
            if (!found) return;
            Value = rawValue;
            Message = string.IsNullOrWhiteSpace(rawValue)
                ? $"'{func.Name}' is obsolete and will be removed."
                : $"'{func.Name}' is obsolete and will be removed: {rawValue}";
            IsResolved = true;
        }
    }

    // Groups multiple providers under a shared identity key so callers can discover
    // all providers belonging to a conceptual set (e.g. several implementations of
    // the same operation).  An optional integer priority lets the calling code
    // determine a preferred default without the library imposing an ordering.
    //
    // Raw value forms:
    //   "MyGroup"       — group key only, priority defaults to 0
    //   "MyGroup, 5"    — group key with explicit priority
    [StrongHint]
    class GroupKey : IStrongHint<IShaderFunction>
    {
        public string Key => Func.kGroupKey;

        public object Value { get; private set; }     // group key string
        internal int Priority { get; private set; }   // 0 if not specified
        internal string DisplayName { get; private set; } // null if not declared
        internal string Label { get; private set; }       // null if not declared
        public string Message { get; private set; }
        public bool IsResolved { get; private set; }

        internal GroupKey() { }

        internal GroupKey(string groupKey, int priority = 0, string displayName = null, string label = null)
        {
            Value = groupKey;
            Priority = priority;
            DisplayName = displayName;
            Label = label;
            IsResolved = true;
        }

        public void Initialize(bool found, string rawValue, IShaderFunction func, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints)
        {
            if (!found) return;
            if (string.IsNullOrWhiteSpace(rawValue)) { Message = "Expected a group key string."; return; }

            var tokens = HintUtils.LazyTokenString(rawValue);

            if (tokens.Length == 1)
            {
                Value = tokens[0];
                DisplayName = tokens[0];
                IsResolved = true;
            }
            else if (tokens.Length >= 2)
            {
                if (!int.TryParse(tokens[1], out var priority))
                {
                    Message = $"Expected an integer priority as the second token, but found '{tokens[1]}'.";
                    return;
                }
                Value = tokens[0];
                DisplayName = tokens[0];
                Priority = priority;
                if (tokens.Length >= 3) DisplayName = tokens[2];
                if (tokens.Length >= 4) Label = tokens[3];
                if (tokens.Length > 4)
                {
                    Message = $"Expected 1-4 comma-separated tokens (key[, priority[, displayName[, label]]]), but found {tokens.Length}.";
                    return;
                }
                IsResolved = true;
            }
        }
    }
}
