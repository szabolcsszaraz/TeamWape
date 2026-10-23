using System;
using Unity.AppUI.UI;
using UnityEditor;
using UnityEngine;

namespace Unity.InferenceEngine.Editor.Visualizer.Editor
{
    [FilePath("ProjectSettings/InferenceEngineGraphSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    class ModelVisualizerSettings : ScriptableSingleton<ModelVisualizerSettings>
    {
        [SerializeField]
        CanvasControlScheme m_CanvasControlScheme = CanvasControlScheme.Modern;

        public CanvasControlScheme CanvasControlScheme
        {
            get => m_CanvasControlScheme;
            set
            {
                if (m_CanvasControlScheme != value)
                {
                    m_CanvasControlScheme = value;
                    Save();
                }
            }
        }

        public event Action OnPropertiesModified;

        // Call this method to save changes to disk
        public void Save()
        {
            Save(true);
        }

        public void PropertiesModified()
        {
            OnPropertiesModified?.Invoke();
        }

        // Default values method - useful for reset functionality
        public void ResetToDefaults()
        {
            m_CanvasControlScheme = CanvasControlScheme.Modern;
            Save();
        }
    }
}
