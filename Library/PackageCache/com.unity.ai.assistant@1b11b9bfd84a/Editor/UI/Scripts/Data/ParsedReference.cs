namespace Unity.AI.Assistant.UI.Editor.Scripts.Data
{
    /// <summary>
    /// A structured reference extracted from a bracketed segment in execution log text.
    /// Either an object reference (has <see cref="InstanceId"/>) or an asset path (has <see cref="AssetPath"/>).
    /// </summary>
    struct ParsedReference
    {
        public string DisplayText;
        public long InstanceId;
        public string AssetPath;

        public bool IsObjectReference => InstanceId != 0;
        public bool IsAssetPath => AssetPath != null;
    }
}
