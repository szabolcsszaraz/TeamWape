using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Assistant.Editor.Acp;
using Unity.AI.Assistant.Editor.Backend.Socket.Tools;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Tools.Editor;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Data.MessageBlocks;
using Unity.AI.Assistant.Utils;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Utils
{
    /// <summary>
    /// Extracts completed action outcomes from message blocks for display in the completed actions section.
    /// Handles both ACP and legacy function call blocks.
    /// </summary>
    static class CompletedActionsExtractor
    {
        static readonly HashSet<string> s_AcpCodeEditTools = new(StringComparer.OrdinalIgnoreCase)
        {
            "Edit", "Write", "Unity_CodeEdit"
        };

        public static List<CompletedActionData> Extract(List<IMessageBlockModel> blocks)
        {
            if (blocks == null)
            {
                return null;
            }

            var actions = new List<CompletedActionData>();
            var fileChanges = new Dictionary<string, CompletedActionData>(StringComparer.OrdinalIgnoreCase);

            foreach (var block in blocks)
            {
                switch (block)
                {
                    case AcpToolCallBlockModel acpBlock:
                    {
                        ExtractAcpOutcomes(acpBlock, fileChanges, actions);
                        break;
                    }

                    case FunctionCallBlockModel funcBlock:
                    {
                        ExtractFunctionCallOutcomes(funcBlock, fileChanges, actions);
                        break;
                    }
                }
            }

            foreach (var entry in fileChanges.Values)
            {
                actions.Add(entry);
            }

            return actions.Count > 0 ? actions : null;
        }

        static void ExtractAcpOutcomes(AcpToolCallBlockModel block, Dictionary<string, CompletedActionData> fileChanges, List<CompletedActionData> actions)
        {
            var status = block.LatestUpdate?.Status ?? block.CallInfo?.Status ?? AcpToolCallStatus.Pending;
            if (status == AcpToolCallStatus.Pending)
            {
                return;
            }

            bool isSuccess = status == AcpToolCallStatus.Completed;

            if (block.CallInfo?.ToolName != null && s_AcpCodeEditTools.Contains(block.CallInfo.ToolName))
            {
                var rawInput = block.RawInput ?? block.CallInfo?.RawInput;
                if (rawInput != null)
                {
                    var oldStr = rawInput["old_string"]?.ToString() ?? rawInput["oldString"]?.ToString();
                    var newStr = rawInput["new_string"]?.ToString() ?? rawInput["newString"]?.ToString() ?? rawInput["content"]?.ToString();
                    var filePath = rawInput["file_path"]?.ToString() ?? rawInput["filePath"]?.ToString();

                    if (!string.IsNullOrEmpty(filePath))
                    {
                        AggregateFileChange(fileChanges, filePath, oldStr, newStr, isSuccess);
                        return;
                    }
                }
            }

            if (!string.IsNullOrEmpty(block.LatestUpdate?.Content))
            {
                ExtractReferencesFromText(block.LatestUpdate.Content, isSuccess, actions);
            }
        }

        static void ExtractFunctionCallOutcomes(FunctionCallBlockModel block, Dictionary<string, CompletedActionData> fileChanges, List<CompletedActionData> actions)
        {
            if (!block.Call.Result.IsDone) return;

            bool isSuccess = block.Call.Result.HasFunctionCallSucceeded;
            var functionId = block.Call.FunctionId;

            block.Call.GetCodeEditParameters(out string filePath, out string newCode, out string oldCode);
            if (!string.IsNullOrEmpty(filePath) && (newCode != null || oldCode != null))
            {
                AggregateFileChange(fileChanges, filePath, oldCode, newCode, isSuccess);
                return;
            }

            try
            {
                switch (functionId)
                {
                    case CreateGameObjectTool.k_FunctionId:
                    {
                        var typed = block.Call.Result.GetTypedResult<CreateGameObjectTool.CreateGameObjectOutput>();
                        AddObjectRefEntry(actions, typed.Message, typed.GameObjectId, isSuccess);
                        return;
                    }
                    
                    case ModifyGameObjectTool.k_FunctionId:
                    {
                        var typed = block.Call.Result.GetTypedResult<ModifyGameObjectTool.ModifyGameObjectOutput>();
                        AddObjectRefEntry(actions, typed.Message, typed.GameObjectId, isSuccess);
                        return;
                    }
                    
                    case RemoveGameObjectTool.k_FunctionId:
                    {
                        actions.Add(new CompletedActionData
                        {
                            Title = block.Call.GetDefaultTitle(),
                            IsSuccess = isSuccess
                        });
                        return;
                    }
                    
                    case AddComponentTool.k_FunctionId:
                    {
                        var typed = block.Call.Result.GetTypedResult<AddComponentTool.AddComponentOutput>();
                        AddObjectRefEntry(actions, typed.Message, typed.ComponentInstanceId, isSuccess);
                        return;
                    }
                    
                    case RunCommandTool.k_FunctionId:
                    {
                        ExtractRunCommandOutcome(block, isSuccess, actions, fileChanges);
                        return;
                    }
                    
                    case DeleteFileTool.k_FunctionId:
                    {
                        var deletedPath = block.Call.Parameters?["filePath"]?.ToString();
                        actions.Add(new CompletedActionData
                        {
                            Title = "Deleted " + (!string.IsNullOrEmpty(deletedPath) ? Path.GetFileName(deletedPath) : "file"),
                            IsSuccess = isSuccess
                        });
                        
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                InternalLog.LogWarning($"[CompletedActionsExtractor] Failed to extract outcome for '{functionId}': {e.Message}");
                actions.Add(new CompletedActionData
                {
                    Title = block.Call.GetDefaultTitle(),
                    IsSuccess = isSuccess
                });
            }
        }

        static void ExtractRunCommandOutcome(FunctionCallBlockModel block, bool isSuccess, List<CompletedActionData> actions, Dictionary<string, CompletedActionData> fileChanges)
        {
            var executionSucceeded = isSuccess;
            string executionLogs = null;

            try
            {
                var typed = block.Call.Result.GetTypedResult<RunCommandTool.ExecutionOutput>();
                executionSucceeded = isSuccess && typed.IsExecutionSuccessful;
                executionLogs = typed.ExecutionLogs;
            }
            catch (Exception)
            {
                // Fall through with defaults if deserialization fails
            }

            if (string.IsNullOrEmpty(executionLogs))
                return;

            var lines = executionLogs.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var (displayText, _) = ExecutionLogUtils.ParseLogLine(line);
                var hasReference = false;

                var cleanTitle = ExecutionLogUtils.StripBracketedReferences(displayText);

                foreach (var reference in ExecutionLogUtils.ExtractReferences(displayText))
                {
                    if (reference.IsObjectReference)
                    {
                        hasReference = true;
                        actions.Add(new CompletedActionData
                        {
                            Title = cleanTitle,
                            IsSuccess = executionSucceeded,
                            ClickableRef = reference.DisplayText + "|InstanceID:" + reference.InstanceId
                        });
                    }
                    else if (reference.IsAssetPath)
                    {
                        hasReference = true;
                        AggregateFileChange(fileChanges, reference.AssetPath, null, null, executionSucceeded);
                    }
                }

                if (!hasReference && !string.IsNullOrWhiteSpace(displayText))
                {
                    actions.Add(new CompletedActionData
                    {
                        Title = cleanTitle,
                        IsSuccess = executionSucceeded,
                        ClickableRef = null
                    });
                }
            }
        }

        static void AggregateFileChange(Dictionary<string, CompletedActionData> fileChanges, string filePath, string oldStr, string newStr, bool isSuccess)
        {
            var (added, removed) = ComputeLineCounts(oldStr, newStr);
            bool isCreate = string.IsNullOrEmpty(oldStr) && !string.IsNullOrEmpty(newStr);

            if (fileChanges.TryGetValue(filePath, out var existing))
            {
                existing.LinesAdded += added;
                existing.LinesRemoved += removed;
                existing.IsSuccess = existing.IsSuccess && isSuccess;
                fileChanges[filePath] = existing;
            }
            else
            {
                fileChanges[filePath] = new CompletedActionData
                {
                    Title = (isCreate ? "Created " : "Modified ") + Path.GetFileName(filePath),
                    IsSuccess = isSuccess,
                    LinesAdded = added,
                    LinesRemoved = removed,
                    ClickableRef = filePath
                };
            }
        }

        static void AddObjectRefEntry(List<CompletedActionData> actions, string message, long instanceId, bool isSuccess)
        {
            string objectName = GetObjectName(instanceId);
            actions.Add(new CompletedActionData
            {
                Title = !string.IsNullOrEmpty(message) ? message : objectName ?? "Object",
                IsSuccess = isSuccess,
                ClickableRef = objectName != null ? objectName + "|InstanceID:" + instanceId : null
            });
        }

        static void ExtractReferencesFromText(string text, bool isSuccess, List<CompletedActionData> actions)
        {
            foreach (var reference in ExecutionLogUtils.ExtractReferences(text))
            {
                if (!reference.IsObjectReference) continue;

                actions.Add(new CompletedActionData
                {
                    Title = reference.DisplayText,
                    IsSuccess = isSuccess,
                    ClickableRef = reference.DisplayText + "|InstanceID:" + reference.InstanceId
                });
            }
        }

        static string GetObjectName(long instanceId)
        {
#if UNITY_6000_5_OR_NEWER
            var obj = EditorUtility.EntityIdToObject(EntityId.FromULong((ulong)instanceId));
#elif UNITY_6000_3_OR_NEWER
            var obj = EditorUtility.EntityIdToObject((int)instanceId);
#else
            var obj = EditorUtility.InstanceIDToObject((int)instanceId);
#endif
            return obj != null ? obj.name : null;
        }

        const int k_LcsLineThreshold = 1000;

        static (int added, int removed) ComputeLineCounts(string oldStr, string newStr)
        {
            var oldLines = string.IsNullOrEmpty(oldStr) ? Array.Empty<string>() : oldStr.Split('\n');
            var newLines = string.IsNullOrEmpty(newStr) ? Array.Empty<string>() : newStr.Split('\n');

            int oldCount = oldLines.Length;
            int newCount = newLines.Length;

            if (oldCount > k_LcsLineThreshold || newCount > k_LcsLineThreshold)
                return (Math.Max(0, newCount - oldCount), Math.Max(0, oldCount - newCount));

            int lcs = ComputeLcsLength(oldLines, newLines);
            return (newCount - lcs, oldCount - lcs);
        }

        static int ComputeLcsLength(string[] a, string[] b)
        {
            int m = a.Length;
            int n = b.Length;

            // Use two rows instead of full matrix to save memory
            var prev = new int[n + 1];
            var curr = new int[n + 1];

            for (int i = 1; i <= m; i++)
            {
                for (int j = 1; j <= n; j++)
                {
                    if (string.Equals(a[i - 1], b[j - 1], StringComparison.Ordinal))
                        curr[j] = prev[j - 1] + 1;
                    else
                        curr[j] = Math.Max(prev[j], curr[j - 1]);
                }

                // Swap rows
                var temp = prev;
                prev = curr;
                curr = temp;
                Array.Clear(curr, 0, curr.Length);
            }

            return prev[n];
        }
    }
}
