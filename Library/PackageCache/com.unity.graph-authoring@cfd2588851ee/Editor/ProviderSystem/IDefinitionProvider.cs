using System.Collections.Generic;
using System.Collections.Immutable;
using UnityEngine;

namespace Unity.GraphAuthoring.Editor.ProviderSystem
{

    // A functional data object used by the provider system (ie. See IShaderObject).
    // Definitions should be read-only and immutable. They are used as a template for
    // for populating mutable State, which can then be used for UX and the like.
    internal interface IDefinition
    {
        bool IsValid { get; }
        string Name { get; }
        IEnumerable<string> Namespace => ImmutableArray<string>.Empty;
        IReadOnlyCollection<IStrongHint> Hints => ImmutableArray<IStrongHint>.Empty;
    }


    // Abstracts how or where a Definition comes from, allowing them to be used
    // in a context free manner. A provider can be mutable and is stored alongside state,
    // and prevents the need from serializing a Definition directly.
    // Within tools that utilize the provider system, they should expect a Definition to change
    // when modifications to the Provider are made.
    internal interface IProvider
    {
        string ProviderKey { get; }
        bool IsValid { get; }
        GUID AssetID => default;
        string Version => Hints.Version.kUnversioned;
        string Group => null;
        int GroupPriority => 0;
        void Reload() { }
        IProvider Clone();
    }

    internal interface IProvider<T> : IProvider where T : IDefinition
    {
        T Definition { get; }
        bool IProvider.IsValid => Definition?.IsValid ?? false;
    }
}
