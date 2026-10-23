namespace Unity.AI.Assistant.UI.Editor.Scripts.Data
{
    readonly struct AssistantDropdownItemData
    {
        public readonly string Id;
        public readonly string DisplayText;
        public readonly string IconClass;
        public readonly object Tag;
        public readonly bool IsAction;
        public readonly string Tooltip;

        public AssistantDropdownItemData(string id, string displayText, string iconClass = null, object tag = null, bool isAction = false, string tooltip = null)
        {
            Id = id;
            DisplayText = displayText;
            IconClass = iconClass;
            Tag = tag;
            IsAction = isAction;
            Tooltip = tooltip;
        }
    }
}
