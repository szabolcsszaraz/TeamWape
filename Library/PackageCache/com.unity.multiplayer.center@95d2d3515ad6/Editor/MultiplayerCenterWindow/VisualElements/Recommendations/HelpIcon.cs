using Unity.Properties;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Multiplayer.Center.Editor
{
    [UxmlElement]
    partial class HelpIcon : VisualElement
    {
        string m_PackageName;

        [UxmlAttribute, CreateProperty]
        public string DocumentationUrl { get; set; }

        public string PackageName
        {
            get => m_PackageName;
            set{
                m_PackageName = value;
                tooltip = L10n.Tr($"Navigates to the {m_PackageName} associated online documentation.");
            }
        }

        public HelpIcon()
        {
            this.AddManipulator(new Clickable(OnClick));
        }

        void OnClick()
        {
            Application.OpenURL(DocumentationUrl);
        }
    }
}
