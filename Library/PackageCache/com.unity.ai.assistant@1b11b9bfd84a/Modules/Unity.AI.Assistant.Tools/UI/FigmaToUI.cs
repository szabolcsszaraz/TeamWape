using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.FunctionCalling;
using UnityEditor;
using Unity.Relay.Editor;
using UnityEngine;

namespace Unity.AI.Assistant.Tools.Editor.UI
{
    class FigmaToUI
    {
        const string k_BaseUrl = "https://api.figma.com/v1";
        
        internal static bool HasToken { get; private set; }
        
        [InitializeOnLoadMethod]
        static void InitFigmaTokenTracking()
        {
            RelayService.Instance.StateChanged += OnRelayStateChanged;
        }

        static async void OnRelayStateChanged()
        {
            if (!RelayService.Instance.IsConnected)
                return;
            await RefreshTokenState();
        }

        internal static async Task<bool> VerifyFigmaAuthToken(string token)
        {
            token = token?.Trim();
            if (string.IsNullOrEmpty(token))
                return false;

            using var client = await FigmaToUIUtils.CreateAuthenticatedClient(token);

            // Use a non-existent file key to test file_content:read scope.
            // A 404 means the token is valid and has read access (file just doesn't exist).
            // Any other status (401, 403, etc.) means the token is invalid or lacks required scope.
            var response = await client.GetAsync($"{k_BaseUrl}/files/__token_check__?depth=1");
            var status = (int)response.StatusCode;
            if (status != 404)
                return false;

            var result = await CredentialClient.Instance.SetAsync("figma", "FIGMA_API_TOKEN", token);
            HasToken = result.Success;
            return result.Success;
        }

        internal static async Task RefreshTokenState()
        {
            try
            {
                var response = await CredentialClient.Instance.RevealAsync("figma", "FIGMA_API_TOKEN");
                HasToken = response.Success && !string.IsNullOrEmpty(response.Value);
            }
            catch
            {
                HasToken = false;
            }
        }

        internal static async Task RemoveToken()
        {
            await CredentialClient.Instance.DeleteAsync("figma", "FIGMA_API_TOKEN");
            HasToken = false;
            FigmaToUITools.InvalidateClient();
        }
        
        internal static async Task<string> GetToken()
        {
            var response = await CredentialClient.Instance.RevealAsync("figma", "FIGMA_API_TOKEN");
            return response.Success ? response.Value : string.Empty;
        }
    }

    static class FigmaToUITools
    {
        const string k_BaseUrl = "https://api.figma.com/v1";
        static HttpClient m_Client;

        [Serializable]
        internal struct ScreenInfo
        {
            [JsonProperty("id")]
            internal string Id;

            [JsonProperty("name")]
            internal string Name;

            [JsonProperty("width")]
            internal float Width;

            [JsonProperty("height")]
            internal float Height;
        }

        [Serializable]
        internal struct ListScreensOutput
        {
            [JsonProperty("fileName")]
            internal string FileName;

            [JsonProperty("screens")]
            internal List<ScreenInfo> Screens;

            [JsonProperty("requestedNodeId", NullValueHandling = NullValueHandling.Ignore)]
            internal string RequestedNodeId;

            [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
            internal string Message;
        }

        [Serializable]
        internal struct ImportScreenOutput
        {
            [JsonProperty("referencePath")]
            internal string ReferencePath;

            [JsonProperty("referenceInstanceId")]
            internal long ReferenceInstanceId;

            [JsonProperty("summary")]
            internal string Summary;

            [JsonProperty("message")]
            internal string Message;
        }

        [AgentTool(
            "List all top-level screens/frames in a Figma project. If the URL contains a node-id, "
            + "the response includes a requestedNodeId — use that node ID directly with ImportScreen. "
            + "If no node-id is in the URL, present the screen list and confirm with user which screen/UI component to create.",
            "Unity.Figma.ListScreens")]
        [AgentToolSettings(toolCallEnvironment: ToolCallEnvironment.EditMode,
            tags: FunctionCallingUtilities.k_UITag)]
        internal static async Task<ListScreensOutput> ListScreensFromFigmaProject(
            ToolExecutionContext context,
            [ToolParameter("The full Figma design URL, e.g. https://www.figma.com/design/ABC123/MyFile?node-id=0-1")]
            string projectUrl)
        {
            var client = await GetClient();
            var fileKey = FigmaToUIUtils.ParseFigmaUrl(projectUrl);
            var requestedNodeId = FigmaToUIUtils.ParseNodeIdFromUrl(projectUrl);

            var response = await client.GetAsync($"{k_BaseUrl}/files/{fileKey}?depth=2");
            await FigmaToUIUtils.EnsureSuccessOrThrowDiagnostic(response, fileKey);

            var json = await response.Content.ReadAsStringAsync();
            var file = JObject.Parse(json);

            var output = new ListScreensOutput
            {
                FileName = file["name"]?.ToString(),
                Screens = new List<ScreenInfo>()
            };

            var pages = file["document"]?["children"];
            if (pages == null) return output;

            foreach (var page in pages)
            {
                var frames = page["children"];
                if (frames == null) continue;

                foreach (var frame in frames)
                {
                    if (frame["type"]?.ToString() != "FRAME") continue;

                    var bbox = frame["absoluteBoundingBox"];
                    output.Screens.Add(new ScreenInfo
                    {
                        Id = frame["id"]?.ToString(),
                        Name = frame["name"]?.ToString(),
                        Width = bbox?["width"]?.Value<float>() ?? 0,
                        Height = bbox?["height"]?.Value<float>() ?? 0
                    });
                }
            }

            if (requestedNodeId != null)
            {
                output.RequestedNodeId = requestedNodeId;

                var matchingScreen = output.Screens.Find(s => s.Id == requestedNodeId);
                output.Message = matchingScreen.Id != null
                    ? $"The URL points to screen '{matchingScreen.Name}' (node {requestedNodeId}). "
                      + "Use this node ID directly with ImportScreen."
                    : $"The URL points to node {requestedNodeId}, which is a nested element or component. "
                      + "Use this node ID directly with ImportScreen.";
            }

            return output;
        }

        [AgentTool(
            "Import a Figma node (screen, component, or element): downloads a reference screenshot, auto-detects and downloads all visual assets as PNGs, and returns a compact XML layout summary with asset filenames embedded. "
            + "Works with any node ID — a full screen or an individual element/component. Each node marked with asset=\"filename.png\" has been downloaded to the output folder.",
            "Unity.Figma.ImportScreen")]
        [AgentToolSettings(toolCallEnvironment: ToolCallEnvironment.EditMode,
            tags: FunctionCallingUtilities.k_UITag)]
        internal static async Task<ImportScreenOutput> ImportFigmaScreen(
            ToolExecutionContext context,
            [ToolParameter("The full Figma design URL")]
            string projectUrl,
            [ToolParameter("The node ID to import, e.g. '2:2'. Can be a screen, component, or any element.")]
            string nodeId,
            [ToolParameter("Target folder to save all assets, e.g. 'Assets/UI/FigmaImport'")]
            string outputFolder)
        {
            var client = await GetClient();
            var fileKey = FigmaToUIUtils.ParseFigmaUrl(projectUrl);
            var fullOutputPath = Path.GetFullPath(outputFolder);
            if (!Directory.Exists(fullOutputPath))
            {
                await context.Permissions.CheckFileSystemAccess(PermissionItemOperation.Create, fullOutputPath);
                Directory.CreateDirectory(fullOutputPath);
            }

            // Fetch full node data
            var layoutResponse = await client.GetAsync($"{k_BaseUrl}/files/{fileKey}/nodes?ids={nodeId}");
            await FigmaToUIUtils.EnsureSuccessOrThrowDiagnostic(layoutResponse, fileKey);

            var layoutJson = await layoutResponse.Content.ReadAsStringAsync();
            var layoutResult = JObject.Parse(layoutJson);
            var nodeData = layoutResult["nodes"]?[nodeId]?["document"] as JObject;

            if (nodeData == null)
            {
                return new ImportScreenOutput
                {
                    Message = $"No data found for node {nodeId}."
                };
            }

            // Identify asset nodes
            var assetNodes = FigmaToUIUtils.CollectAssetNodes(nodeData);

            // Download assets as PNGs via Figma render API
            var assetFileNames = new Dictionary<string, string>();
            if (assetNodes.Count > 0)
            {
                var nodeIds = string.Join(",", assetNodes.ConvertAll(a => a.NodeId));
                var renderResponse = await client.GetAsync(
                    $"{k_BaseUrl}/images/{fileKey}?ids={nodeIds}&format=png&scale=2");
                await FigmaToUIUtils.EnsureSuccessOrThrowDiagnostic(renderResponse, fileKey);

                var renderJson = await renderResponse.Content.ReadAsStringAsync();
                var renderResult = JObject.Parse(renderJson);
                var renderedImages = renderResult["images"] as JObject;

                if (renderedImages != null)
                {
                    foreach (var asset in assetNodes)
                    {
                        var imageUrl = renderedImages[asset.NodeId]?.ToString();
                        if (string.IsNullOrEmpty(imageUrl)) continue;

                        var filePath = Path.Combine(fullOutputPath, asset.FileName);
                        var imageBytes = await client.GetByteArrayAsync(imageUrl);
                        await context.Permissions.CheckFileSystemAccess(PermissionItemOperation.Create, filePath);
                        await File.WriteAllBytesAsync(filePath, imageBytes);

                        assetFileNames[asset.NodeId] = asset.FileName;
                    }
                }
            }

            // Download reference screenshot
            var refResponse = await client.GetAsync(
                $"{k_BaseUrl}/images/{fileKey}?ids={nodeId}&format=png&scale=2");
            string referencePath = null;

            if (refResponse.IsSuccessStatusCode)
            {
                var refJson = await refResponse.Content.ReadAsStringAsync();
                var refResult = JObject.Parse(refJson);
                var refUrl = (refResult["images"] as JObject)?[nodeId]?.ToString();

                if (!string.IsNullOrEmpty(refUrl))
                {
                    var screenName = FigmaToUIUtils.SanitizeFileName(
                        nodeData["name"]?.ToString() ?? nodeId);
                    referencePath = Path.Combine(fullOutputPath, $"{screenName}_reference.png");
                    var refBytes = await client.GetByteArrayAsync(refUrl);
                    await context.Permissions.CheckFileSystemAccess(PermissionItemOperation.Create, referencePath);
                    await File.WriteAllBytesAsync(referencePath, refBytes);
                }
            }

            AssetDatabase.Refresh();

            // Resolve reference image instance ID so the agent can view it
            long referenceInstanceId = 0;
            if (referencePath != null)
            {
                var assetPath = referencePath;
                var projectRoot = Path.GetFullPath(".");
                if (assetPath.StartsWith(projectRoot))
                    assetPath = assetPath.Substring(projectRoot.Length + 1).Replace('\\', '/');

                var refAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (refAsset != null)
                {
#if UNITY_6000_5_OR_NEWER
                    referenceInstanceId = (long)EntityId.ToULong(refAsset.GetEntityId());
#else
                    referenceInstanceId = refAsset.GetInstanceID();
#endif
                }
            }

            // Build compact summary with asset annotations
            var summary = FigmaToUIUtils.BuildSummary(nodeData, assetFileNames);

            return new ImportScreenOutput
            {
                ReferencePath = referencePath,
                ReferenceInstanceId = referenceInstanceId,
                Summary = summary,
                Message = $"Imported '{nodeData["name"]}'. "
                          + $"Downloaded {assetFileNames.Count} assets to {outputFolder}. "
                          + $"IMPORTANT: Call Unity.GetImageAssetContent with instanceID={referenceInstanceId} "
                          + "to view the reference screenshot before generating UI code. "
                          + "Use the summary XML to generate UI. Nodes with asset=\"...\" are pre-downloaded PNGs."
            };
        }

        internal static void InvalidateClient()
        {
            m_Client?.Dispose();
            m_Client = null;
        }

        static async Task<HttpClient> GetClient()
        {
            return m_Client ??= await FigmaToUIUtils.CreateAuthenticatedClient();
        }
    }
}
