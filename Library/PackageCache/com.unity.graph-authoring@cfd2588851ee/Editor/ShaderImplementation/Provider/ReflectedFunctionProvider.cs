using System;
using UnityEditor;
using UnityEditor.ShaderApiReflection;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Unity.GraphAuthoring.Editor.ProviderSystem
{
    [Serializable]
    [MovedFrom(false, "UnityEditor.ShaderGraph.ProviderSystem", "Unity.ShaderGraph.Editor")]
    internal class ReflectedFunctionProvider : IProvider<IShaderFunction>
    {
        public string ProviderKey => m_providerKey;
        public string Version => m_version ?? Hints.Version.kUnversioned;
        public string Group => m_group;
        public int GroupPriority => m_groupPriority;
        public GUID AssetID => m_sourceAssetId;

        public IShaderFunction Definition
        {
            get
            {
                if (m_definition == null || !m_definition.IsValid)
                    Reload();
                return m_definition;
            }
        }

        [SerializeField]
        string m_providerKey;

        [SerializeField]
        string m_version;

        [SerializeField]
        GUID m_sourceAssetId;


        [NonSerialized]
        string m_group;

        [NonSerialized]
        int m_groupPriority;

        [NonSerialized]
        IShaderFunction m_definition;

        internal ReflectedFunctionProvider(GUID assetId, IShaderFunction definition)
        {
            m_sourceAssetId = assetId;
            m_definition = definition;

            if (definition.IsValid)
            {
                m_providerKey = definition.Hints.GetHint<Hints.ProviderKey>()?.Value as string;
                m_version = definition.Hints.GetHint<Hints.Version>()?.Value as string;

                var groupHint = definition.Hints.GetHint<Hints.GroupKey>();
                m_group = groupHint?.Value as string;
                m_groupPriority = groupHint?.Priority ?? 0;
            }
        }

        internal ReflectedFunctionProvider(GUID assetId, ReflectedFunction function)
            : this(assetId, ShaderReflectionUtils.ToShaderFunction(function))
        {
        }

        public void Reload()
        {
            if (ShaderReflectionUtils.TryResolve(m_providerKey, m_sourceAssetId, out var function, m_version))
            {
                m_definition = ShaderReflectionUtils.ToShaderFunction(function, this);

                var groupHint = m_definition.Hints.GetHint<Hints.GroupKey>();
                m_group = groupHint?.Value as string;
                m_groupPriority = groupHint?.Priority ?? 0;

            }
            else
            {
                m_definition = null;
                m_group = null;
                m_groupPriority = 0;
            }
        }

        public IProvider Clone()
            // NOTE: Definition is read only, so it's fine for a clone to reference the same object.
            => new ReflectedFunctionProvider(m_sourceAssetId, m_definition);
    }
}
