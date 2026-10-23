using System;

namespace UnityEditor.U2D.Sprites.ProjectSettings
{
    [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    internal class SpriteEditorWindowProjectSettingsAttribute : Attribute
    {
        public string name { get; }
        public string[] searchKeyWords { get; }
        public SpriteEditorWindowProjectSettingsAttribute(string name, string[] searchKeyWords = null)
        {
            this.name = name ?? "";
            this.searchKeyWords = searchKeyWords ?? Array.Empty<string>();
        }
    }
}
