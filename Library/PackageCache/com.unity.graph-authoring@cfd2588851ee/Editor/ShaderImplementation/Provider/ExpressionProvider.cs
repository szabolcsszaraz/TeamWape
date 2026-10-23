using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using Unity.GraphAuthoring.Editor.ProviderSystem.Hints;

namespace Unity.GraphAuthoring.Editor.ProviderSystem
{
    [Serializable]
    [ScriptedProvider]
    [MovedFrom(false, "UnityEditor.ShaderGraph.ProviderSystem", "Unity.ShaderGraph.Editor")]
    internal class ExpressionProvider : IProvider<IShaderFunction>
    {
        // Expression provider is domain agnostic, but we need some sort of keyword filtering that is domain specific.
        internal interface IKeywordFilter
        {
            bool IsReserved(string name);
        }

        internal const string kExpressionProviderKey = "unity:shadergraph:scripted:Expression";
        public string ProviderKey => kExpressionProviderKey;
        public GUID AssetID => default;

        private static readonly string[] kNamespace = { "unity_sg_expression" };

        internal string Expression => m_expression;
        internal string ShaderType => m_type;

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
        string m_name;

        [SerializeField]
        string m_expression;

        [SerializeField]
        string m_type;

        [SerializeReference]
        IKeywordFilter m_keywordFilter;

        [NonSerialized]
        IShaderFunction m_definition;

        internal ExpressionProvider() : this("Expression", "A", "float") { }

        internal ExpressionProvider(string name, string expression, string type, IKeywordFilter filter = null)
        {
            UpdateExpression(name, expression, type, filter);
        }

        public void Reload()
        {
            m_definition = ExpressionToShaderFunction(m_name, m_expression, m_type, out _, m_keywordFilter);
        }

        static IReadOnlyCollection<IStrongHint> BuildFuncHints() =>
            new IStrongHint[]
            {
                new DisplayName("Expression", true),
                new ProviderKey(kExpressionProviderKey),
                new SearchName("Expression"),
                new SearchCategory("Utility"),
                new SearchTerms(new[] { "equation", "calculation", "inline", "code" }),
                new Precision(true),
            };

        static IReadOnlyCollection<IStrongHint> BuildParamHints(string displayName) =>
            new IStrongHint[]
            {
                new DisplayName(displayName, true)
            };

        internal void UpdateExpression(string name, string expression, string type, IKeywordFilter filter = null)
        {
            m_name = name;
            m_type = type;
            m_expression = expression;
            m_keywordFilter = filter;
            Reload();
        }

        public IProvider Clone()
            => new ExpressionProvider(m_name, m_expression, m_type, m_keywordFilter);

        internal static IShaderFunction ExpressionToShaderFunction(string name, string expression, string type, out string finalExpression, IKeywordFilter keywordFilter = null)
        {
            // don't create fields from comments.
            const string kCommentRegex = @"(\/\*.*?\*\/)|(\/\/.*)";

            // pattern for getting valid identifiers that aren't functions or members,
            // includes expanded european character set.
            const string kIdentifierRegex = @"(?<!\.)\b[a-zA-ZŽžÀ-ÿ_][a-zA-ZŽžÀ-ÿ0-9_]*\b(?!\s*[\(])";

            List<string> orderedNames = new();
            HashSet<string> usedNames = new();

            // gather the list of identifiers, deduplicate them, and ignore if they are reserved keywords.
            string HandleName(string name)
            {
                if (!usedNames.Contains(name) && keywordFilter?.IsReserved(name) != true)
                {
                    usedNames.Add(name);
                    orderedNames.Add(name);
                }
                return name;
            }

            // clean out comments.
            expression = Regex.Replace(expression, kCommentRegex, "");

            // process identifiers.
            expression = Regex.Replace(expression, kIdentifierRegex, e => HandleName(e.Value));

            finalExpression = expression;

            // default initialize if there is no expression left.
            if (string.IsNullOrWhiteSpace(expression))
                expression = $"({type})0";

            IShaderType shaderType = new ShaderType(type);

            List<IShaderField> parameters = new();

            foreach (var paramName in orderedNames)
                parameters.Add(new ShaderField(paramName, true, false, shaderType, BuildParamHints(paramName)));

            // trust that the name and type is valid.
            return new ShaderFunction(name, kNamespace, parameters, shaderType, $"return {expression};", BuildFuncHints());
        }

    }
}
