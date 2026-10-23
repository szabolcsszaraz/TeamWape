using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.AI.Assistant.Agents;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Api;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;


namespace Unity.AI.Assistant.Integrations.Sample.Editor
{
    static class ApiExample
    {
        const string k_SampleAgentId = "unity_sample_agent";

        [MenuItem("AI Assistant/Internals/API Sample")]
        public static void ShowWindow()
        {
            var window = EditorWindow.GetWindow<SampleWindow>();
            window.Show();
        }
        
        public static async Task<string> RunHeadless()
        {
            try
            {
                // Create agent for one-shot use
                var agent = CreateSampleAgent();
                
                // Create custom attachment to specify city information so that it is not asked in headless mode
                var attachedContext = new AssistantApi.AttachedContext();
                var customAttachment = new VirtualAttachment(
                    payload: "{username: John, hobbies: [hiking, gaming, cooking], city: Montreal}", 
                    type: "WeatherData", 
                    displayName: "Weather Data",
                    metadata: null
                );
                attachedContext.Add(customAttachment);
                
                // Run agent with custom attachment
                return await agent.RunHeadless("What should I do today?", attachedContext: attachedContext);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            return null;
        }
        
        public static async Task RunWithUI()
        {
            try
            {
                // Create custom attachment (no city so that it is asked to the user)
                var attachedContext = new AssistantApi.AttachedContext();
                var customAttachment = new VirtualAttachment(
                    payload: "{username: John, hobbies: [hiking, gaming, cooking]}", 
                    type: "WeatherData", 
                    displayName: "Weather Data",
                    metadata: null
                );
                attachedContext.Add(customAttachment);
                
                // Run Assistant with our agent already registered
                await AssistantApi.Run("What should I do today?", attachedContext: attachedContext);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
        
        public static async Task PromptThenRun(VisualElement parent)
        {
            try
            {
                // Create custom attachment (no city so that it is asked to the user)
                var attachedContext = new AssistantApi.AttachedContext();
                var customAttachment = new VirtualAttachment(
                    payload: "{username: John, hobbies: [hiking, gaming, cooking]}", 
                    type: "WeatherData", 
                    displayName: "Weather Data",
                    metadata: null
                );
                attachedContext.Add(customAttachment);
                
                // Show prompt popup so that the prompt can be changed by the user
                await AssistantApi.PromptThenRun(parent,"What should I do today?", attachedContext: attachedContext);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
        
        static IAgent CreateSampleAgent()
        {
            var agent = new LlmAgent()
                .WithId(k_SampleAgentId)
                .WithName("Personal Assistant")
                .WithDescription("Specialized agent to help plan your daily activities according to the weather.")
                .WithSystemPrompt(@"
                    You are a friendly personal assistant. Use your tools to:
                    1. Suggest activities based on the weather
                    2. ALWAYS save the list of suggestions into personal notes

                    In your final answer, when you mention a specific activity name, always mention it as a url with the following format:
                    [Activity Name](sample://activity_name) where 'Activity Name' is the name of the activity.
                ")
                .WithToolsFrom<SampleTools>();

            return agent;
        }
    }
}
