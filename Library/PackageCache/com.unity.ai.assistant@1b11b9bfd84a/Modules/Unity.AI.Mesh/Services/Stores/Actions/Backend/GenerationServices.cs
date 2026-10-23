using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Mesh.Services.Stores.Actions.Backend
{
    static class GenerationServices
    {
        static readonly Dictionary<string, IGenerationService> k_Services = new();
        static bool s_Initialized;

        public static async void Register(IGenerationService service)
        {
            try
            {
                foreach (var modelSettings in await service.GetModelsAsync())
                {
                    k_Services[modelSettings.id] = service;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to register generation service {service.GetType().Name}: {ex.Message}");
            }
        }

        public static IGenerationService GetServiceForModel(string modelSettingsId)
        {
            EnsureInitialized();
            return k_Services.GetValueOrDefault(modelSettingsId);
        }

        public static IEnumerable<IGenerationService> GetAllServices()
        {
            EnsureInitialized();
            foreach (var service in k_Services.Values)
            {
                yield return service;
            }
        }

        [InitializeOnLoadMethod]
        static void Initialize()
        {
            if (Application.isBatchMode)
                return;

            s_Initialized = true;
            RegisterAllServices();
        }

        static void EnsureInitialized()
        {
            if (s_Initialized)
                return;

            s_Initialized = true;
            RegisterAllServices();
        }

        static void RegisterAllServices()
        {
            // Register all IGenerationService implementations using TypeCache
            var serviceTypes = TypeCache.GetTypesDerivedFrom<IGenerationService>();
            foreach (var serviceType in serviceTypes)
            {
                // Skip abstract classes and interfaces
                if (serviceType.IsAbstract || serviceType.IsInterface)
                    continue;

                try
                {
                    var serviceInstance = (IGenerationService)System.Activator.CreateInstance(serviceType);
                    Register(serviceInstance);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to instantiate generation service {serviceType.Name}: {ex.Message}");
                }
            }
        }
    }
}
