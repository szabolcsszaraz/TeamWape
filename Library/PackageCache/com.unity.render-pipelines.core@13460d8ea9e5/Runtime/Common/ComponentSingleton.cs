using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering
{
#if UNITY_EDITOR
    static class ComponentSingletonRegistry
    {
        static readonly List<Component> s_Instances = new();

        internal static void Register(Component instance) => s_Instances.Add(instance);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void ResetStaticsOnLoad()
        {
            foreach (var instance in s_Instances)
                if (instance != null)
                    CoreUtils.Destroy(instance.gameObject);
            s_Instances.Clear();
        }
    }
#endif

    // Use this class to get a static instance of a component
    // Mainly used to have a default instance

    /// <summary>
    /// Singleton of a Component class.
    /// </summary>
    /// <typeparam name="TType">Component type.</typeparam>
    public static class ComponentSingleton<TType>
        where TType : Component
    {
        static TType s_Instance = null;

        /// <summary>
        /// Instance of the required component type.
        /// </summary>
        public static TType instance
        {
            get
            {
                if (s_Instance == null)
                {
                    GameObject go = new GameObject("Default " + typeof(TType).Name) { hideFlags = HideFlags.HideAndDontSave };

#if !UNITY_EDITOR
                    GameObject.DontDestroyOnLoad(go);
#endif

                    go.SetActive(false);
                    s_Instance = go.AddComponent<TType>();
#if UNITY_EDITOR
                    ComponentSingletonRegistry.Register(s_Instance);
#endif
                }

                return s_Instance;
            }
        }

        /// <summary>
        /// Release the component singleton.
        /// </summary>
        public static void Release()
        {
            if (s_Instance != null)
            {
                var go = s_Instance.gameObject;
                CoreUtils.Destroy(go);
                s_Instance = null;
            }
        }
    }
}
