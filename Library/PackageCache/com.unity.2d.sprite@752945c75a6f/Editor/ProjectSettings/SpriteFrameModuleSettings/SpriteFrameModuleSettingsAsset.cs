using System;
using UnityEditorInternal;
using UnityEngine;

namespace UnityEditor.U2D.Sprites.ProjectSettings
{
    class SpriteFrameModuleSettingsAsset : ScriptableObject
    {
        [SerializeField]
        bool m_AllowSpriteFrameEditCapabilityOverride = false;
        private SpriteFrameModuleSettingsAsset() { }
        static SpriteFrameModuleSettingsAsset s_Instance;
        event Action OnSettingsChanged;

        public static void CreateInstance()
        {
            if (s_Instance == null)
            {
                var savedInstanceArray = InternalEditorUtility.LoadSerializedFileAndForget("ProjectSettings/SpriteFrameModuleSettingsAsset.asset");
                if (savedInstanceArray != null && savedInstanceArray.Length > 0)
                {
                    s_Instance = savedInstanceArray[0] as SpriteFrameModuleSettingsAsset;
                }
                else
                    s_Instance = CreateInstance<SpriteFrameModuleSettingsAsset>();
            }
        }

        public static event Action SettingsChanged
        {
            add { instance.OnSettingsChanged += value; }
            remove { instance.OnSettingsChanged -= value; }
        }

        public static bool allowSpriteFrameEditCapabilityOverride
        {
            get { return instance.m_AllowSpriteFrameEditCapabilityOverride; }
            set
            {
                if (instance.m_AllowSpriteFrameEditCapabilityOverride != value)
                {
                    instance.m_AllowSpriteFrameEditCapabilityOverride = value;
                    instance.OnSettingsChanged?.Invoke();
                }
            }
        }

        public static SpriteFrameModuleSettingsAsset instance
        {
            get
            {
                CreateInstance();
                return s_Instance;
            }
        }

        public static void Save()
        {
            if(s_Instance != null)
                InternalEditorUtility.SaveToSerializedFileAndForget(new UnityEngine.Object[] { s_Instance }, "ProjectSettings/SpriteFrameModuleSettingsAsset.asset", true);
        }
    }
}
