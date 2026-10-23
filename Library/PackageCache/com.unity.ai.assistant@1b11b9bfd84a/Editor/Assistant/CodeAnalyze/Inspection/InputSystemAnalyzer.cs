using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.Editor.CodeAnalyze
{
    internal enum InputSystemType
    {
        New,
        Legacy,
        Both,
        Unknown
    }

    internal struct InputSystemAnalysisResult
    {
        public InputSystemType ProjectInputSystem;
        public InputSystemType ScriptInputSystem;
        public bool ScriptHasMatchingInputSystem;
        public string Context;
    }
    
    internal class InputSystemAnalyzer
    {
        static readonly string[] m_NewInputSystemNamespaces = { "UnityEngine.InputSystem" };
        static List<MetadataReference> s_References;
        static List<MetadataReference> References => 
            s_References ??= new List<MetadataReference>(DynamicAssemblyBuilder.GetUnityReferences());

        public static string Analyze(string script)
        {
            var inputSystemMatch = HasMatchingInputSystem(script);
            var output = string.Empty;
            if (!inputSystemMatch.ScriptHasMatchingInputSystem)                                                                                                                               
            {                                                                                                                                                                                 
                output = $"WARNING: " +                                                                                                                                                  
                         $"This project is configured to use {inputSystemMatch.ProjectInputSystem}, " +                                                                                            
                         $"while this script uses the {inputSystemMatch.ScriptInputSystem} Input System API: " +
                         $"{inputSystemMatch.Context}. " +
                         $"This mismatch can cause compilation errors. Carefully review the code and adjust the mismatch.";
            }

            return output;
        }

        static InputSystemType GetProjectInputSystemType()
        {
            var playerSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        
            if (playerSettings.Length == 0)
            {
                Debug.LogError("Could not find the PlayerSettings asset.");
                return InputSystemType.Unknown; 
            }

            using SerializedObject so = new SerializedObject(playerSettings[0]);
            var activeInputHandler = so.FindProperty("activeInputHandler");

            if (activeInputHandler == null) return InputSystemType.Unknown;
            return activeInputHandler.intValue switch
            {
                0 => InputSystemType.Legacy,
                1 => InputSystemType.New,
                2 => InputSystemType.Both,
                _ => InputSystemType.Unknown
            };
        }

        static List<string> FindInputModuleUsages(SyntaxTree tree, string moduleName)
        {
            var root = tree.GetCompilationUnitRoot();
            return root.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Where(id => id.Identifier.Text == moduleName)
                .Select(id => $"{moduleName} is used at line {id.GetLocation().GetLineSpan().StartLinePosition.Line + 1}")
                .ToList();
        }

        static InputSystemAnalysisResult HasMatchingInputSystem(string script)
        {
            var inputSystemType = GetProjectInputSystemType();
            if  (inputSystemType == InputSystemType.Unknown)
                throw new InvalidOperationException("Project input system cannot be detected from project setting.");
            
            var tree = SyntaxFactory.ParseSyntaxTree(script);
            switch (inputSystemType)
            {
                case InputSystemType.Legacy:
                {
                    var usesNewInputSystem = tree.ContainsNamespaces(m_NewInputSystemNamespaces);
                    if (usesNewInputSystem.Valid)
                        return new InputSystemAnalysisResult
                        {
                            ProjectInputSystem = inputSystemType,
                            ScriptInputSystem = InputSystemType.New,
                            ScriptHasMatchingInputSystem = false,
                            Context = usesNewInputSystem.Context
                        };

                    var newModuleUsages = FindInputModuleUsages(tree, "InputSystemUIInputModule");
                    if (newModuleUsages.Any())
                        return new InputSystemAnalysisResult
                        {
                            ProjectInputSystem = inputSystemType,
                            ScriptInputSystem = InputSystemType.New,
                            ScriptHasMatchingInputSystem = false,
                            Context = string.Join(", ", newModuleUsages)
                        };
                    break;
                }
                case InputSystemType.New:
                {
                    var root = tree.GetCompilationUnitRoot();
                    var suspectedOldInput = root.DescendantNodes()
                        .OfType<MemberAccessExpressionSyntax>()
                        .Where(m =>
                            // Matches when Input is used directly as a simple name:
                            (m.Expression is IdentifierNameSyntax id && id.Identifier.Text == "Input") || 
                            // Matches when Input is the right side of a dotted access:
                            (m.Expression is MemberAccessExpressionSyntax ma && ma.Name.Identifier.Text == "Input")
                        );

                    var suspectedOldInputList = suspectedOldInput.ToList();
                    if (!suspectedOldInputList.Any()) break;
                    
                    var compilation = CSharpCompilation.Create("InputSystemAnalysis")
                        .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                        .AddReferences(References)
                        .AddSyntaxTrees(tree);

                    var semanticModel = compilation.GetSemanticModel(tree);
                    var occurrences = suspectedOldInputList
                        .Where(access => semanticModel.GetSymbolInfo(access.Expression).Symbol?.ContainingNamespace?.ToDisplayString() == "UnityEngine")
                        .Select(access => $"{access.Expression.ToString()} is used at line {access.GetLocation().GetLineSpan().StartLinePosition.Line + 1}")
                        .ToList();
                    if (occurrences.Any())
                    {
                        return new InputSystemAnalysisResult
                        {
                            ProjectInputSystem = inputSystemType,
                            ScriptInputSystem = InputSystemType.Legacy,
                            ScriptHasMatchingInputSystem = false,
                            Context = string.Join(", ", occurrences)
                        };
                    }

                    var legacyModuleUsages = FindInputModuleUsages(tree, "StandaloneInputModule");
                    if (legacyModuleUsages.Any())
                    {
                        return new InputSystemAnalysisResult
                        {
                            ProjectInputSystem = inputSystemType,
                            ScriptInputSystem = InputSystemType.Legacy,
                            ScriptHasMatchingInputSystem = false,
                            Context = string.Join(", ", legacyModuleUsages)
                        };
                    }

                    break;
                }
            }
            return new InputSystemAnalysisResult
            {
                ProjectInputSystem = inputSystemType,
                ScriptHasMatchingInputSystem = true
            };
        }
    }
}