using System.Collections.Generic;
using System.Text;

namespace Unity.GraphAuthoring.Editor.ProviderSystem
{
    internal static class ShaderObjectUtils
    {
        internal static string QualifyName(IDefinition obj, string separator = "::", bool includeName = true)
        {
            if (obj == null || !obj.IsValid)
                return null;

            StringBuilder builder = new();

            if (obj.Namespace != null)
                foreach (var name in obj.Namespace)
                    builder.Append($"{name}{separator}");

            builder.Append(obj.Name);
            return builder.ToString();
        }

        internal static string QualifySignature(IShaderFunction obj, bool includeNamespace = true, bool forDisplay = false)
        {
            if (obj == null || !obj.IsValid)
                return null;

            StringBuilder builder = new();
            builder.Append(includeNamespace ? QualifyName(obj) : obj.Name);
            builder.Append(forDisplay ? " (" : "(");

            bool first = true;
            if (obj.Parameters != null)
            {
                foreach (var param in obj.Parameters)
                {
                    if (!first) builder.Append(forDisplay ? ", " : ",");
                    first = false;
                    builder.Append(QualifyName(param.ShaderType));
                }
            }
            builder.Append(")");

            return builder.ToString();
        }

        // Searches the resolved IStrongHint collection for the provider key hint.
        internal static string EvaluateProviderKey(IShaderFunction obj, string fromHint = Hints.Func.kProviderKey)
        {
            if (obj?.IsValid != true)
                return null;

            if (obj.Hints != null)
                foreach (var h in obj.Hints)
                    if (h.Key == fromHint && h.IsResolved)
                        return h.Value as string ?? QualifySignature(obj);
            return QualifySignature(obj);
        }

        // Fast provider-key lookup from a raw string dict (used at construction time before hints are resolved).
        internal static string EvaluateProviderKey(IReadOnlyDictionary<string, string> rawHints, IShaderFunction contextObj, string fromHint = Hints.Func.kProviderKey)
        {
            if (rawHints != null && rawHints.TryGetValue(fromHint, out var result) && !string.IsNullOrWhiteSpace(result))
                return result;
            return QualifySignature(contextObj);
        }

        private static string DeclareField(IShaderField a)
        {
            (string typeName, string arraySpec) SplitTypeName(string typeName)
            {
                int i = typeName.IndexOf('[');
                return i == -1 ? (typeName, "") : (typeName.Substring(0, i - 1), typeName.Substring(i));
            }

            var t = SplitTypeName(a.ShaderType.Name);
            string access = a.IsInput && a.IsOutput ? "inout " : a.IsOutput ? "out " : "";
            return $"{access}{t.typeName} {a.Name}{t.arraySpec}";
        }

        private static string HintValueToString(object value)
        {
            return value switch
            {
                string s => s,
                string[] arr => string.Join(", ", arr),
                float[] arr => string.Join(", ", arr),
                _ => value?.ToString() ?? ""
            };
        }

        private static string GenerateHints(string closure, IReadOnlyCollection<IStrongHint> hints, string name = null)
        {
            StringBuilder sb = new();
            bool any = false;
            foreach (var hint in hints)
            {
                if (!hint.IsResolved) continue;
                if (!any)
                {
                    sb.AppendLine(name != null ? $"/// <{closure} name = \"{name}\">" : $"/// <{closure}>");
                    any = true;
                }
                sb.AppendLine($"///\t <{hint.Key}>{HintValueToString(hint.Value)}</{hint.Key}>");
            }
            if (any) sb.AppendLine($"/// </{closure}>");
            return sb.ToString();
        }

        internal static string GenerateCall(IShaderFunction func, string argList)
        {
            StringBuilder call = new();
            string funcName = func.Name;

            foreach (var name in func.Namespace)
                call.Append($"{name}::");

            call.Append(funcName);
            call.Append("(");
            call.Append(argList);
            call.Append(");");

            return call.ToString();
        }

        internal static string GenerateCode(IShaderFunction func, bool generateHints = true, bool export = true, bool generateNamespace = true)
        {
            StringBuilder sb = new();
            int indent = 0;
            void Ln(string text = "") { for (int i = 0; i < indent; i++) sb.Append("    "); sb.AppendLine(text); }

            if (generateNamespace)
                foreach (var name in func.Namespace)
                    sb.Append($"namespace {name} {{");

            sb.AppendLine();
            indent++;

            if (generateHints)
            {
                if (func.Hints.Count > 0)
                    sb.Append(GenerateHints("funchints", func.Hints));

                foreach (var param in func.Parameters)
                    if (param.Hints.Count > 0)
                        sb.Append(GenerateHints("paramhints", param.Hints, param.Name));
            }

            string funcName = func.Name;
            string typeName = func.ReturnType.Name;

            sb.Append($"{(export ? "UNITY_EXPORT_REFLECTION" : "")} {typeName} {funcName}(");

            bool first = true;
            foreach (var param in func.Parameters)
            {
                if (!first) sb.Append(", ");
                first = false;
                sb.Append(DeclareField(param));
            }
            Ln(")");
            Ln("{");
            indent++;
            Ln(func.FunctionBody);
            indent--;
            Ln("}");
            indent--;

            if (generateNamespace)
                foreach (var name in func.Namespace)
                    sb.Append($"}} /*{name}*/");

            return sb.ToString();
        }
    }
}
