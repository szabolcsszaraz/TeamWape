using System.Collections.Generic;

namespace Unity.GraphAuthoring.Editor.ProviderSystem.Hints
{
    internal static class Common
    {
        internal const string kDisplayName = "sg:DisplayName";
        internal const string kTooltip = "sg:Tooltip";
    }

    [StrongHint]
    internal class DisplayName : IStrongHint<IDefinition>
    {
        public string Key { get; }
        public bool AlwaysProcess => true;
        public IReadOnlyCollection<string> Synonyms { get; } = new string[] { "Label", "Title" };

        public object Value { get; private set; }
        public string Message { get; private set; }
        public bool IsResolved { get; private set; }

        readonly string m_fallback;

        // Default constructor — used by TypeCache auto-discovery.
        internal DisplayName() { Key = Common.kDisplayName; }

        internal DisplayName(string name, string fallback = null)
        {
            Key = name;
            m_fallback = fallback;
        }

        // Direct-value constructor — used by ExpressionProvider.
        internal DisplayName(string name, string resolvedValue, bool resolved)
        {
            Key = name;
            Value = resolvedValue;
            IsResolved = true;
        }

        // Standard-key direct-value constructor — key defaults to Common.kDisplayName.
        internal DisplayName(string resolvedValue, bool resolved)
        {
            Key = Common.kDisplayName;
            Value = resolvedValue;
            IsResolved = true;
        }

        public void Initialize(bool found, string rawValue, IDefinition obj, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints)
        {
            Value = found ? rawValue : m_fallback ?? obj.Name;
            IsResolved = true;
        }
    }

    [StrongHint]
    internal class Tooltip : IStrongHint<IDefinition>
    {
        public string Key { get; }
        public IReadOnlyCollection<string> Synonyms { get; } = new[] { "summary" };

        public object Value { get; private set; }
        public string Message { get; private set; }
        public bool IsResolved { get; private set; }

        // Default constructor — used by TypeCache auto-discovery.
        internal Tooltip() { Key = Common.kTooltip; }

        internal Tooltip(string name) { Key = name; }

        // Direct-value constructor — used by ExpressionProvider.
        internal Tooltip(string name, string resolvedValue, bool resolved)
        {
            Key = name;
            Value = resolvedValue;
            IsResolved = resolvedValue != null;
        }

        public void Initialize(bool found, string rawValue, IDefinition obj, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints)
        {
            Value = found ? rawValue : null;
            IsResolved = found && rawValue != null;
        }
    }

    internal abstract class Flag : IStrongHint<IDefinition>
    {
        public string Key { get; }
        public IReadOnlyCollection<string> Conflicts { get; }
        public IReadOnlyCollection<string> Synonyms { get; }

        public object Value { get; private set; }
        public string Message { get; private set; }
        public bool IsResolved { get; private set; }

        // Base constructors — used by named subclasses.
        internal Flag(string name) { Key = name; }
        internal Flag(string name, string conflict) { Key = name; Conflicts = conflict != null ? new[] { conflict } : null; }
        internal Flag(string name, string[] conflicts) { Key = name; Conflicts = conflicts; }
        internal Flag(string name, string[] conflicts, string[] synonyms) { Key = name; Conflicts = conflicts; Synonyms = synonyms; }

        // Resolved-value constructor — for subclasses that need to set IsResolved at construction time.
        internal Flag(string name, string[] conflicts, string[] synonyms, bool resolved)
        {
            Key = name; Conflicts = conflicts; Synonyms = synonyms; IsResolved = resolved;
        }

        public void Initialize(bool found, string rawValue, IDefinition obj, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints)
        {
            Value = rawValue;
            IsResolved = found;
            if (found && !string.IsNullOrWhiteSpace(rawValue))
                Message = "Expects an empty argument string.";
        }
    }
}
