using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.ApplicationModels;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor.Mcp.Transport;
using Unity.AI.Assistant.Editor.Mcp.Transport.Models;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Utils;

namespace Unity.AI.Assistant.Editor.Mcp
{
    class McpAssistantFunction : ICachedFunction
    {
        /// <summary>
        /// Prefix for all MCP function IDs. Format: "Client.Mcp."
        /// Used to namespace MCP tools in the function registry.
        /// </summary>
        internal const string FunctionIdPrefix = "Client.Mcp.";

        McpServerEntry m_Server;
        McpTool m_Tool;
        IUnityMcpHttpClient m_Client;

        public FunctionDefinition FunctionDefinition { get; }

        public McpAssistantFunction(McpServerEntry server, McpTool tool, IUnityMcpHttpClient client)
        {
            m_Server = server ?? throw new ArgumentNullException(nameof(server));
            m_Tool = tool ?? throw new ArgumentNullException(nameof(tool));
            m_Client = client ?? throw new ArgumentNullException(nameof(client));

            var param = new List<ParameterDefinition>();

            if (tool.InputSchema is { Properties: not null })
            {
                foreach (var prop in tool.InputSchema.Properties)
                {
                    var properties = prop.Value as JObject;

                    if(properties is null)
                        continue;
                    
                    param.Add(new ParameterDefinition(
                        properties["description"]?.Value<string>() ?? "no parameter description provided",
                        prop.Key,
                        properties["type"]?.Value<string>() ?? "string",
                        tool.InputSchema.Properties[prop.Key] as JObject
                    ));
                }
            }

            FunctionDefinition = new FunctionDefinition(tool.Description, tool.Name)
            {
                Namespace = "MCP",
                FunctionId = CreateFunctionId(server.Name, tool.Name),
                Parameters = param,
                AssistantMode = AssistantMode.Any,
                Tags = new List<string>() { "mcp" }
            };
        }

        public async Task<object> InvokeAsync(ToolExecutionContext context)
        {
            InternalLog.Log($"Calling: {context.Call.FunctionId}");

            // Unwrap parameters that may be incorrectly wrapped by the LLM as {"value": actualValue}
            var parameters = UnwrapParameters(context.Call.Parameters);

            var res = await m_Client.CallMcpToolAsync(
                m_Server,
                m_Tool.Name,
                parameters);

            if (!res.IsSuccess)
                throw new Exception(res.ErrorMessage);

            return res.Content;
        }

        JObject UnwrapParameters(JObject parameters)
        {
            if (parameters == null)
                return null;

            var result = new JObject();
            
            foreach (var property in parameters.Properties())
            {
                var schema = FunctionDefinition
                    .Parameters
                    .FirstOrDefault(p => p.Name == property.Name);

                if (schema == null)
                {
                    InternalLog.LogWarning("LLM provided parameter in MCP call that is not present in the schema." +
                                           $"Parameter: {property.Name}");
                    continue;
                }

                if (schema.Type == "object")
                {
                    result[property.Name] = property.Value;
                    continue;
                }

                result[property.Name] = UnwrapValue(schema, property.Value);
            }

            return result;
        }

        static JToken UnwrapValue(ParameterDefinition schema, JToken value)
        {
            // The LLM has an issue described here: https://jira.unity3d.com/browse/UUM-134855 where it provides
            // parameters wrapped up in more object, for some reason. It's a bit weird tbh, but it generally does a good
            // job of providing objects with a single argument that is the correct type. In an unwrap, assume that 
            // there is a single value wrapping the object and extract it.
            if (schema.Type == "array" && value is JArray array)
                return array;
            
            if (value is JObject obj)
            {
                if(obj.Count == 0)
                    return schema.DefaultValue == null ? null : JToken.FromObject(schema.DefaultValue);
                
                if (obj.Count > 1)
                {
                    obj.Remove("type");
                    obj.Remove("TYPE");
                    obj.Remove("Type");
                }

                return obj.First?.First;
            }
            
            // If not, assume the object is the correct JToken format
            return value;
        }

        /// <summary>
        /// Creates a function ID from server and tool names.
        /// The ID uses dots as separators with PascalCase segments.
        /// Format: Client.Mcp.ServerName.ToolName
        /// </summary>
        /// <param name="serverName">The MCP server name</param>
        /// <param name="toolName">The tool name</param>
        /// <returns>A valid function ID in format Client.Mcp.ServerName.ToolName</returns>
        internal static string CreateFunctionId(string serverName, string toolName)
        {
            var formattedServerName = ToPascalCase(serverName ?? string.Empty);
            var formattedToolName = ToPascalCase(toolName ?? string.Empty);
            return $"{FunctionIdPrefix}{formattedServerName}.{formattedToolName}";
        }

        /// <summary>
        /// Converts a string to PascalCase, treating underscores, spaces, and hyphens as word separators.
        /// Examples: "my_server" → "MyServer", "my-tool" → "MyTool", "already_Pascal" → "AlreadyPascal"
        /// </summary>
        static string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            var result = new System.Text.StringBuilder();
            var capitalizeNext = true;

            foreach (var c in input)
            {
                if (c == '_' || c == ' ' || c == '-' || c == '.')
                {
                    // Skip separator, capitalize next letter
                    capitalizeNext = true;
                }
                else if (capitalizeNext)
                {
                    result.Append(char.ToUpperInvariant(c));
                    capitalizeNext = false;
                }
                else
                {
                    result.Append(c);
                }
            }

            return result.ToString();
        }
    }
}
