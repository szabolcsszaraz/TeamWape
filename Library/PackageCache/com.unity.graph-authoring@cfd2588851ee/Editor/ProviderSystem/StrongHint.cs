using System;
using System.Collections.Generic;
using UnityEditor;

namespace Unity.GraphAuthoring.Editor.ProviderSystem
{
    // TODO(SVFXG-914): Some improvements and simplification can now be made here.

    internal interface IStrongHint
    {
        string Key { get; }
        object Value { get; }
        string Message { get; }
        MessageType MessageSeverity => MessageType.Warning;
        bool IsResolved { get; }
        

        bool AlwaysProcess => false;
        bool AllowDisqualifiedSynonyms => true;
        IReadOnlyCollection<string> Synonyms => null;
        IReadOnlyCollection<string> Conflicts => null;

        void Initialize(bool found, string rawValue, IDefinition obj, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints) { }
    }

    internal interface IStrongHint<T> : IStrongHint where T : IDefinition
    {
        void Initialize(bool found, string rawValue, T obj, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints);

        void IStrongHint.Initialize(bool found, string rawValue, IDefinition obj, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints)
        {
            if (obj is T typed) Initialize(found, rawValue, typed, provider, actualHintKey, rawHints);
        }
    }

    
    internal readonly struct MessageHint : IStrongHint
    {
        public string Key { get; }
        public object Value => null;
        public string Message { get; }
        public bool IsResolved => false;
        internal MessageHint(string key, string message) { Key = key; Message = message; }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    internal class StrongHintAttribute : Attribute { }
}
