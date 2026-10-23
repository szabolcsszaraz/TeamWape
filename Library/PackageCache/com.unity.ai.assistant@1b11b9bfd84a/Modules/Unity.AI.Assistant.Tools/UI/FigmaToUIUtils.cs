using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Unity.AI.Assistant.Tools.Editor.UI
{
    struct AssetNodeInfo
    {
        internal string NodeId;
        internal string Name;
        internal string FileName;
    }

    static class FigmaToUIUtils
    {
        const string k_BaseUrl = "https://api.figma.com/v1";

        internal static string ParseFigmaUrl(string url)
        {
            var uri = new Uri(url);
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            for (var i = 0; i < segments.Length; i++)
            {
                if (segments[i] is "design" or "file" && i + 1 < segments.Length)
                    return segments[i + 1];
            }

            throw new ArgumentException($"Could not extract file key from URL: {url}");
        }

        internal static string ParseNodeIdFromUrl(string url)
        {
            var uri = new Uri(url);
            var query = uri.Query;
            if (string.IsNullOrEmpty(query))
                return null;

            // Parse query string manually to avoid System.Web dependency
            var pairs = query.TrimStart('?').Split('&');
            foreach (var pair in pairs)
            {
                var kv = pair.Split('=', 2);
                if (kv.Length == 2 && kv[0] == "node-id")
                {
                    var rawValue = Uri.UnescapeDataString(kv[1]);
                    var nodeId = rawValue.Replace('-', ':');

                    // 0:1 is the root canvas page, not a specific frame
                    if (nodeId == "0:1")
                        return null;

                    return nodeId;
                }
            }

            return null;
        }

        internal static async Task<HttpClient> CreateAuthenticatedClient(string token = "")                                                                                                                                               
        {
            if (string.IsNullOrEmpty(token))
            {
                token = await FigmaToUI.GetToken();
                if (string.IsNullOrEmpty(token))
                    throw new InvalidOperationException(
                        "Figma token not configured. Please connect to Figma via the AI Assistant first.");
            }                                                                                                         
                                                                                                                                                                                                                 
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("X-Figma-Token", token);                                                                                                                                                  
            return client;         
        }

        internal static async Task EnsureSuccessOrThrowDiagnostic(HttpResponseMessage response, string fileKey)
        {
            if (response.IsSuccessStatusCode)
                return;

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                using var testClient = await CreateAuthenticatedClient();
                var meResponse = await testClient.GetAsync($"{k_BaseUrl}/me");

                var hint = meResponse.IsSuccessStatusCode
                    ? $"Your Figma token is valid but cannot access file '{fileKey}'. "
                      + "Ensure your token has the 'File content: Read-only' scope. "
                      + "You may need to create a new token with that scope in Figma → Settings → Security."
                    : "Your Figma token appears to be invalid or expired. "
                      + "Please generate a new Personal Access Token in Figma → Settings → Security "
                      + "and re-enter it in the AI Assistant.";

                throw new HttpRequestException($"403 Forbidden: {hint}");
            }

            response.EnsureSuccessStatusCode();
        }

        internal static List<AssetNodeInfo> CollectAssetNodes(JObject rootNode)
        {
            var assets = new List<AssetNodeInfo>();
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectAssetNodesRecursive(rootNode, assets, usedNames);
            return assets;
        }

        static void CollectAssetNodesRecursive(JObject node, List<AssetNodeInfo> assets, HashSet<string> usedNames)
        {
            if (node == null) return;

            if (IsAssetNode(node))
            {
                var id = node["id"]?.ToString() ?? "";
                var name = node["name"]?.ToString() ?? "asset";
                var fileName = SanitizeFileName(name);

                if (!usedNames.Add(fileName))
                {
                    fileName = $"{fileName}_{id.Replace(":", "_")}";
                    usedNames.Add(fileName);
                }

                assets.Add(new AssetNodeInfo { NodeId = id, Name = name, FileName = $"{fileName}.png" });
                return;
            }

            var children = node["children"] as JArray;
            if (children == null) return;

            foreach (var child in children)
                CollectAssetNodesRecursive(child as JObject, assets, usedNames);
        }

        static bool IsAssetNode(JObject node)
        {
            var type = node["type"]?.ToString();

            if (type is "VECTOR" or "BOOLEAN_OPERATION" or "LINE" or "REGULAR_POLYGON" or "STAR")
                return true;

            if (type is "RECTANGLE" or "ELLIPSE")
                return HasImageFill(node);

            if (type is "GROUP" or "FRAME" or "COMPONENT" or "INSTANCE")
            {
                var children = node["children"] as JArray;
                if (children == null || children.Count == 0)
                    return HasImageFill(node);

                return !ContainsTextDescendant(node) && ContainsVisualContent(node);
            }

            return false;
        }

        static bool HasImageFill(JObject node)
        {
            var fills = node["fills"] as JArray;
            if (fills == null) return false;

            foreach (var fill in fills)
            {
                if (fill["type"]?.ToString() == "IMAGE")
                    return true;
            }

            return false;
        }

        static bool ContainsTextDescendant(JObject node)
        {
            var children = node["children"] as JArray;
            if (children == null) return false;

            foreach (var child in children)
            {
                var childObj = child as JObject;
                if (childObj == null) continue;

                if (childObj["type"]?.ToString() == "TEXT")
                    return true;

                if (ContainsTextDescendant(childObj))
                    return true;
            }

            return false;
        }

        static bool ContainsVisualContent(JObject node)
        {
            var type = node["type"]?.ToString();
            if (type is "VECTOR" or "BOOLEAN_OPERATION" or "LINE" or "REGULAR_POLYGON" or "STAR" or "ELLIPSE")
                return true;

            if (type is "RECTANGLE" or "FRAME" or "COMPONENT" or "INSTANCE")
            {
                if (HasImageFill(node))
                    return true;
            }

            var children = node["children"] as JArray;
            if (children == null) return false;

            foreach (var child in children)
            {
                if (ContainsVisualContent(child as JObject))
                    return true;
            }

            return false;
        }

        internal static string BuildSummary(JObject node, Dictionary<string, string> assetFileNames, int depth = 0, int maxDepth = 12)
        {
            if (node == null || depth > maxDepth) return "";

            var sb = new StringBuilder();
            var indent = new string(' ', depth * 2);

            var type = node["type"]?.ToString() ?? "UNKNOWN";
            var id = node["id"]?.ToString();
            var name = node["name"]?.ToString();

            sb.Append($"{indent}<{type} id=\"{id}\" name=\"{name}\"");

            AppendBoundingBox(sb, node);

            if (assetFileNames != null && id != null && assetFileNames.TryGetValue(id, out var assetFile))
            {
                sb.AppendLine($" asset=\"{assetFile}\" />");
                return sb.ToString();
            }

            AppendNodeProperties(sb, node, type);

            var children = node["children"] as JArray;
            if (children != null && children.Count > 0)
            {
                sb.AppendLine(">");

                var ordered = SortChildrenByPosition(node, children);
                foreach (var child in ordered)
                {
                    var childObj = child as JObject;
                    if (IsFullyOutsideParent(node, childObj))
                        continue;
                    sb.Append(BuildSummary(childObj, assetFileNames, depth + 1, maxDepth));
                }
                sb.AppendLine($"{indent}</{type}>");
            }
            else
            {
                sb.AppendLine(" />");
            }

            return sb.ToString();
        }

        static void AppendBoundingBox(StringBuilder sb, JObject node)
        {
            var bbox = node["absoluteBoundingBox"];
            if (bbox == null) return;

            var x = (int)Math.Round(bbox["x"]?.Value<double>() ?? 0);
            var y = (int)Math.Round(bbox["y"]?.Value<double>() ?? 0);
            var w = (int)Math.Round(bbox["width"]?.Value<double>() ?? 0);
            var h = (int)Math.Round(bbox["height"]?.Value<double>() ?? 0);
            sb.Append($" x=\"{x}\" y=\"{y}\" w=\"{w}\" h=\"{h}\"");
        }

        // When the parent uses auto-layout, Figma's child order already matches
        // the visual flow. For free-positioned containers, sort children by their
        // x-coordinate so the XML order matches left-to-right visual order.
        static IEnumerable<JToken> SortChildrenByPosition(JObject parent, JArray children)
        {
            var hasAutoLayout = !string.IsNullOrEmpty(parent["layoutMode"]?.ToString());
            if (hasAutoLayout)
                return children;

            var list = new List<JToken>(children);
            list.Sort((a, b) =>
            {
                var ax = a?["absoluteBoundingBox"]?["x"]?.Value<double>() ?? 0;
                var bx = b?["absoluteBoundingBox"]?["x"]?.Value<double>() ?? 0;
                return ax.CompareTo(bx);
            });
            return list;
        }

        static bool IsFullyOutsideParent(JObject parent, JObject child)
        {
            if (child == null) return false;

            var parentBBox = parent["absoluteBoundingBox"];
            var childBBox = child["absoluteBoundingBox"];
            if (parentBBox == null || childBBox == null) return false;

            var px = parentBBox["x"]?.Value<double>() ?? 0;
            var py = parentBBox["y"]?.Value<double>() ?? 0;
            var pw = parentBBox["width"]?.Value<double>() ?? 0;
            var ph = parentBBox["height"]?.Value<double>() ?? 0;

            var cx = childBBox["x"]?.Value<double>() ?? 0;
            var cy = childBBox["y"]?.Value<double>() ?? 0;
            var cw = childBBox["width"]?.Value<double>() ?? 0;
            var ch = childBBox["height"]?.Value<double>() ?? 0;

            return cx >= px + pw || cx + cw <= px || cy >= py + ph || cy + ch <= py;
        }

        static void AppendNodeProperties(StringBuilder sb, JObject node, string type)
        {
            if (type == "TEXT")
            {
                var characters = node["characters"]?.ToString();
                if (!string.IsNullOrEmpty(characters))
                    sb.Append($" text=\"{EscapeXmlAttribute(characters)}\"");

                var style = node["style"];
                if (style != null)
                {
                    var fontFamily = style["fontFamily"]?.ToString();
                    var fontSize = style["fontSize"]?.Value<double?>();
                    var fontWeight = style["fontWeight"]?.ToString();
                    if (fontFamily != null) sb.Append($" font=\"{fontFamily}\"");
                    if (fontSize != null) sb.Append($" fontSize=\"{(int)Math.Round(fontSize.Value)}\"");
                    if (fontWeight != null) sb.Append($" fontWeight=\"{fontWeight}\"");
                }

                AppendFillColor(sb, node);
            }

            if (type is "RECTANGLE" or "ELLIPSE" or "FRAME" or "COMPONENT" or "INSTANCE")
            {
                AppendFillColor(sb, node);

                var cornerRadius = node["cornerRadius"]?.Value<double?>();
                if (cornerRadius != null)
                    sb.Append($" cornerRadius=\"{(int)Math.Round(cornerRadius.Value)}\"");
            }

            if (type is "FRAME" or "COMPONENT" or "INSTANCE")
            {
                var layoutMode = node["layoutMode"]?.ToString();
                if (!string.IsNullOrEmpty(layoutMode))
                {
                    sb.Append($" layout=\"{layoutMode}\"");
                    var gap = node["itemSpacing"]?.Value<double?>();
                    if (gap != null) sb.Append($" gap=\"{(int)Math.Round(gap.Value)}\"");
                    var padTop = node["paddingTop"]?.Value<double?>();
                    var padRight = node["paddingRight"]?.Value<double?>();
                    var padBottom = node["paddingBottom"]?.Value<double?>();
                    var padLeft = node["paddingLeft"]?.Value<double?>();
                    if (padTop != null)
                    {
                        var pt = (int)Math.Round(padTop.Value);
                        var pr = (int)Math.Round(padRight ?? 0);
                        var pb = (int)Math.Round(padBottom ?? 0);
                        var pl = (int)Math.Round(padLeft ?? 0);
                        sb.Append($" padding=\"{pt},{pr},{pb},{pl}\"");
                    }
                }
            }
        }

        static void AppendFillColor(StringBuilder sb, JObject node)
        {
            var fills = node["fills"] as JArray;
            if (fills == null || fills.Count == 0) return;

            float baseR = -1, baseG = -1, baseB = -1, baseA = 1;
            var hasSolid = false;

            foreach (var fill in fills)
            {
                var fillType = fill["type"]?.ToString();

                if (fillType == "IMAGE")
                {
                    var imageRef = fill["imageRef"]?.ToString();
                    if (imageRef != null)
                        sb.Append($" imageRef=\"{imageRef}\"");
                    return;
                }

                if (fillType != "SOLID") continue;
                var color = fill["color"];
                if (color == null) continue;

                var r = color["r"]?.Value<float>() ?? 0;
                var g = color["g"]?.Value<float>() ?? 0;
                var b = color["b"]?.Value<float>() ?? 0;
                var opacity = fill["opacity"]?.Value<float>() ?? 1f;
                var blendMode = fill["blendMode"]?.ToString() ?? "NORMAL";

                if (!hasSolid)
                {
                    baseR = r;
                    baseG = g;
                    baseB = b;
                    baseA = opacity;
                    hasSolid = true;
                    continue;
                }

                var blended = ApplyBlendMode(baseR, baseG, baseB, r, g, b, blendMode);
                baseR = Lerp(baseR, blended.r, opacity);
                baseG = Lerp(baseG, blended.g, opacity);
                baseB = Lerp(baseB, blended.b, opacity);
            }

            if (hasSolid)
            {
                var ri = Clamp255(baseR);
                var gi = Clamp255(baseG);
                var bi = Clamp255(baseB);
                sb.Append($" fill=\"rgba({ri},{gi},{bi},{baseA:F2})\"");
            }
        }

        static (float r, float g, float b) ApplyBlendMode(
            float baseR, float baseG, float baseB,
            float overR, float overG, float overB,
            string blendMode)
        {
            switch (blendMode)
            {
                case "MULTIPLY":
                    return (baseR * overR, baseG * overG, baseB * overB);
                case "SCREEN":
                    return (
                        baseR + overR - baseR * overR,
                        baseG + overG - baseG * overG,
                        baseB + overB - baseB * overB);
                default:
                    return (overR, overG, overB);
            }
        }

        static float Lerp(float a, float b, float t) => a + t * (b - a);

        static int Clamp255(float v) => Math.Max(0, Math.Min(255, (int)(v * 255)));


        internal static string SanitizeFileName(string name)
        {
            var sb = new StringBuilder(name.Length);
            foreach (var c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                    sb.Append(c);
                else if (c == ' ' || c == '/')
                    sb.Append('_');
            }

            var result = sb.ToString().Trim('_');
            return string.IsNullOrEmpty(result) ? "asset" : result;
        }

        static string EscapeXmlAttribute(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("\"", "&quot;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }
    }
}
