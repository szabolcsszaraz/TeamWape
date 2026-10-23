using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.AI.Assistant.Utils;
using UnityEngine;

namespace Unity.AI.Assistant.Editor.CodeAnalyze
{
    internal struct ObsoleteApiUsage
    {
        /// <summary>
        /// Fully qualified display string of the deprecated symbol, e.g. "UnityEngine.Object.GetInstanceID()".
        /// </summary>
        public string Symbol;

        /// <summary>
        /// Message carried by the <see cref="System.ObsoleteAttribute"/>, as authored by the API owner.
        /// </summary>
        public string Message;

        /// <summary>
        /// True when the attribute is declared with error severity, meaning the usage fails compilation
        /// rather than producing a warning.
        /// </summary>
        public bool IsError;

        /// <summary>
        /// One-based line of the usage within the analyzed script.
        /// </summary>
        public int Line;

        /// <summary>
        /// Extra guidance about the suggested replacement, resolved from the editor's own assemblies.
        /// Null when no replacement could be resolved from <see cref="Message"/>.
        /// </summary>
        public string ReplacementDetail;
    }

    /// <summary>
    /// Reports deprecated API usage in an agent-authored script.
    /// </summary>
    /// <remarks>
    /// The deprecation data is read from the <see cref="System.ObsoleteAttribute"/> instances carried by the
    /// assemblies the script compiles against, which are the running editor's own assemblies. There is
    /// deliberately no hardcoded list of APIs or Unity versions here: a method that is fine in one editor and
    /// deprecated in the next is reported correctly in both without any change to this file.
    ///
    /// Two things make this worth reporting on top of the raw compiler diagnostics:
    /// warning-severity deprecations never reach the agent at all, because the run command path forwards
    /// errors only; and a rename alone is often not a sufficient fix, since the replacement may return a
    /// different type. Surfacing the replacement's signature lets the agent converge in a single retry
    /// instead of fixing the rename and then tripping over the return type.
    /// </remarks>
    internal static class ObsoleteApiAnalyzer
    {
        const string k_ObsoleteAttributeName = "ObsoleteAttribute";
        const string k_ObsoleteAttributeNamespace = "System";

        /// <summary>
        /// Caps on what reaches the agent. Both the message and the symbol name originate in whatever
        /// assemblies the project references, third-party packages included, so neither is trusted to be
        /// short or single-line before it is interpolated into a tool result.
        /// </summary>
        const int k_MaxTextLength = 300;

        const int k_MaxReportedUsages = 20;

        /// <summary>
        /// Matches the replacement member name in the conventional "Use X instead." obsolete message, taking
        /// the final segment so that a qualified suggestion such as "Use Object.FindObjectsByType instead"
        /// yields the member name. A candidate found here is only trusted once it resolves to a real member on
        /// the containing type, which is what keeps a prose message from turning into invented advice.
        /// </summary>
        static readonly Regex k_ReplacementPattern = new(
            @"\b[Uu]se\s+(?:the\s+)?[""'`]?(?:[A-Za-z_][A-Za-z0-9_]*\.)*(?<name>[A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled);

        static readonly Regex k_WhitespacePattern = new(@"\s+", RegexOptions.Compiled);

        /// <summary>
        /// Describes the deprecated APIs used by the analyzed script, or an empty string when it uses none.
        /// </summary>
        /// <param name="compilation">The compilation to inspect. May carry errors.</param>
        /// <param name="errorsOnly">
        /// When true, only error-severity deprecations are reported. Callers that write into a
        /// compilation-error channel pass true, so that a warning-severity deprecation is not pulled into it
        /// by an unrelated compile error elsewhere in the script.
        /// </param>
        internal static string Analyze(CSharpCompilation compilation, bool errorsOnly = false)
        {
            // Guidance is advisory. It is produced while the caller is already handling a compile failure, on
            // a semantic model built over an erroneous tree, so a throw here must never be what turns a
            // recoverable script into a failed tool call.
            try
            {
                return BuildReport(compilation, errorsOnly);
            }
            catch (Exception exception)
            {
                InternalLog.LogWarning($"Deprecated API analysis failed and was skipped:\n{exception}");
                return string.Empty;
            }
        }

        static string BuildReport(CSharpCompilation compilation, bool errorsOnly)
        {
            var usages = FindObsoleteUsages(compilation);
            if (errorsOnly)
                usages = usages.Where(usage => usage.IsError).ToList();

            if (usages.Count == 0)
                return string.Empty;

            var report = new StringBuilder();
            report.Append($"DEPRECATED API (Unity {Application.unityVersion}):");

            foreach (var usage in usages.Take(k_MaxReportedUsages))
            {
                // The severity is the agent's cue for whether this must be fixed to run at all.
                var severity = usage.IsError ? "error" : "warning";

                report.AppendLine();
                report.Append($"- '{Sanitize(usage.Symbol)}' at line {usage.Line} is obsolete ({severity})");
                report.Append(string.IsNullOrEmpty(usage.Message) ? "." : $": {Sanitize(usage.Message)}");

                if (!string.IsNullOrEmpty(usage.ReplacementDetail))
                {
                    report.AppendLine();
                    report.Append($"  {Sanitize(usage.ReplacementDetail)}");
                }
            }

            var undisclosed = usages.Count - k_MaxReportedUsages;
            if (undisclosed > 0)
            {
                report.AppendLine();
                report.Append($"- and {undisclosed} more deprecated usage(s) not listed.");
            }

            return report.ToString();
        }

        /// <summary>
        /// Flattens text that came from an assembly's attribute into one bounded line, so that a long or
        /// multi-line message cannot restructure the tool result it is embedded in.
        /// </summary>
        static string Sanitize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var collapsed = k_WhitespacePattern.Replace(text, " ").Trim();

            return collapsed.Length <= k_MaxTextLength
                ? collapsed
                : collapsed.Substring(0, k_MaxTextLength) + "...";
        }

        internal static List<ObsoleteApiUsage> FindObsoleteUsages(CSharpCompilation compilation)
        {
            var usages = new List<ObsoleteApiUsage>();

            var tree = compilation?.SyntaxTrees.FirstOrDefault();
            if (tree == null)
                return usages;

            var model = compilation.GetSemanticModel(tree);
            var reported = new HashSet<string>();

            foreach (var node in tree.GetCompilationUnitRoot().DescendantNodes())
            {
                var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                // One use of an API must produce one report. A member access, its enclosing invocation and
                // its name all resolve to the same symbol, and their start lines diverge as soon as the call
                // is split across lines, so the simple name is taken as the single canonical occurrence --
                // which is also the location the compiler points its own diagnostic at. Object creation is
                // collected separately, since no simple name resolves to the constructor.
                switch (node)
                {
                    case SimpleNameSyntax name:
                        CollectFromSymbolInfo(model.GetSymbolInfo(name), line, reported, usages);
                        break;

                    case ObjectCreationExpressionSyntax creation:
                        CollectFromSymbolInfo(model.GetSymbolInfo(creation), line, reported, usages);
                        break;
                }

                if (node is not ExpressionSyntax expression)
                    continue;

                // A user-defined conversion can be deprecated independently of the members around it. This is
                // how an implicit cast that the agent never wrote explicitly still breaks the script. The type
                // check first keeps GetConversion off nodes that carry no conversion at all.
                var typeInfo = model.GetTypeInfo(expression);
                if (typeInfo.Type == null
                    || typeInfo.ConvertedType == null
                    || SymbolEqualityComparer.Default.Equals(typeInfo.Type, typeInfo.ConvertedType))
                    continue;

                var conversion = model.GetConversion(expression);
                if (conversion.IsUserDefined)
                    CollectIfObsolete(conversion.MethodSymbol, line, reported, usages);
            }

            return usages;
        }

        /// <summary>
        /// Collects from a resolved symbol, falling back to the binding candidates when there is none.
        /// </summary>
        /// <remarks>
        /// A failed binding -- wrong argument count, an ambiguous overload -- leaves
        /// <see cref="SymbolInfo.Symbol"/> null while the members it could not choose between sit in
        /// <see cref="SymbolInfo.CandidateSymbols"/>. That case matters more than it looks: the validator
        /// runs this analysis precisely when compilation failed, so a script that both misuses an API and
        /// deprecates one would otherwise report nothing at all.
        /// </remarks>
        static void CollectFromSymbolInfo(
            SymbolInfo info,
            int line,
            HashSet<string> reported,
            List<ObsoleteApiUsage> usages)
        {
            if (info.Symbol != null)
            {
                CollectIfObsolete(info.Symbol, line, reported, usages);
                return;
            }

            foreach (var candidate in info.CandidateSymbols)
                CollectIfObsolete(candidate, line, reported, usages);
        }

        static void CollectIfObsolete(
            ISymbol symbol,
            int line,
            HashSet<string> reported,
            List<ObsoleteApiUsage> usages)
        {
            if (symbol == null)
                return;

            var obsoleteAttribute = symbol.GetAttributes().FirstOrDefault(IsSystemObsoleteAttribute);
            if (obsoleteAttribute == null)
                return;

            var display = symbol.ToDisplayString();
            if (!reported.Add($"{display}@{line}"))
                return;

            var arguments = obsoleteAttribute.ConstructorArguments;
            var message = arguments.Length > 0 ? arguments[0].Value as string : null;
            var isError = arguments.Length > 1 && arguments[1].Value is bool asError && asError;

            usages.Add(new ObsoleteApiUsage
            {
                Symbol = display,
                Message = message,
                IsError = isError,
                Line = line,
                ReplacementDetail = DescribeReplacement(symbol, message)
            });
        }

        /// <summary>
        /// Resolves the replacement named in an obsolete message and describes it, so the agent learns the
        /// replacement's shape at the same time as the deprecation. Returns null when the message names no
        /// resolvable member, or when the replacement is a drop-in whose signature adds nothing.
        /// </summary>
        static string DescribeReplacement(ISymbol obsoleteSymbol, string message)
        {
            if (string.IsNullOrEmpty(message))
                return null;

            if (obsoleteSymbol is not IMethodSymbol obsoleteMethod)
                return null;

            var containingType = obsoleteSymbol.ContainingType;
            if (containingType == null)
                return null;

            var match = k_ReplacementPattern.Match(message);
            if (!match.Success)
                return null;

            var candidateName = match.Groups["name"].Value;
            if (candidateName == obsoleteSymbol.Name)
                return null;

            var replacement = ResolveReplacementMethod(containingType, candidateName, obsoleteMethod);
            if (replacement == null)
                return null;

            var oldReturnType = obsoleteMethod.ReturnType;
            var newReturnType = replacement.ReturnType;
            if (SymbolEqualityComparer.Default.Equals(oldReturnType, newReturnType))
                return null;

            var detail = new StringBuilder();
            detail.Append($"Replacement '{replacement.ToDisplayString()}' returns ");
            detail.Append($"{newReturnType.ToDisplayString()}, not {oldReturnType.ToDisplayString()}. ");
            detail.Append("Declare the receiving variable accordingly (for example with 'var')");

            if (HasObsoleteConversion(newReturnType, oldReturnType))
            {
                detail.Append($" -- converting it back to {oldReturnType.ToDisplayString()} is deprecated as well, ");
                detail.Append("so assigning or casting it to the old type fails for a second time");
            }

            detail.Append('.');
            return detail.ToString();
        }

        /// <summary>
        /// True when converting <paramref name="fromType"/> to <paramref name="toType"/> goes through a
        /// user-defined conversion operator that is itself deprecated. This is what turns a simple rename
        /// into two consecutive failures for code that assigned the old return type to a local.
        /// </summary>
        static bool HasObsoleteConversion(ITypeSymbol fromType, ITypeSymbol toType)
        {
            if (fromType == null || toType == null)
                return false;

            return fromType.GetMembers()
                .OfType<IMethodSymbol>()
                .Where(method => method.MethodKind is MethodKind.Conversion)
                .Where(method => SymbolEqualityComparer.Default.Equals(method.ReturnType, toType))
                .Any(method => method.GetAttributes().Any(IsSystemObsoleteAttribute));
        }

        /// <summary>
        /// Picks the overload of <paramref name="name"/> that the deprecated member was actually replaced by,
        /// or null when that cannot be determined.
        /// </summary>
        /// <remarks>
        /// Taking the first member of the group describes whichever overload happens to be declared first,
        /// which is how a deprecated generic call gets told its replacement changes return type when the
        /// matching generic overload preserves it exactly. Generic candidates are therefore constructed with
        /// the same type arguments as the deprecated call, and an overload group that stays ambiguous after
        /// that yields no advice at all -- describing the wrong overload is worse for the agent than
        /// describing none.
        /// </remarks>
        static IMethodSymbol ResolveReplacementMethod(
            INamedTypeSymbol containingType,
            string name,
            IMethodSymbol obsoleteMethod)
        {
            var candidates = containingType.GetMembers(name)
                .OfType<IMethodSymbol>()
                .Where(candidate => candidate.TypeParameters.Length == obsoleteMethod.TypeArguments.Length)
                .Select(candidate => candidate.TypeParameters.Length > 0
                    ? candidate.Construct(obsoleteMethod.TypeArguments.ToArray())
                    : candidate)
                .ToList();

            if (candidates.Count <= 1)
                return candidates.FirstOrDefault();

            var matching = candidates.Where(candidate => HasSameParameterTypes(candidate, obsoleteMethod)).ToList();

            return matching.Count == 1 ? matching[0] : null;
        }

        static bool HasSameParameterTypes(IMethodSymbol first, IMethodSymbol second)
        {
            if (first.Parameters.Length != second.Parameters.Length)
                return false;

            return first.Parameters
                .Select(parameter => parameter.Type)
                .SequenceEqual(second.Parameters.Select(parameter => parameter.Type), SymbolEqualityComparer.Default);
        }

        /// <summary>
        /// True only for <see cref="System.ObsoleteAttribute"/> itself. The short name alone is not enough:
        /// any assembly may declare its own ObsoleteAttribute in another namespace, and Roslyn issues no
        /// deprecation diagnostic for those, so matching on the name would turn an unrelated annotation into
        /// false guidance for the agent.
        /// </summary>
        static bool IsSystemObsoleteAttribute(AttributeData attribute)
        {
            var attributeClass = attribute.AttributeClass;

            return attributeClass?.Name == k_ObsoleteAttributeName
                && attributeClass.ContainingNamespace?.ToDisplayString() == k_ObsoleteAttributeNamespace;
        }
    }
}
