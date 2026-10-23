namespace Unity.AI.Assistant.FunctionCalling
{
    /// <summary>
    /// The play-mode transition being requested by a tool, used with
    /// <see cref="IToolPermissions.CheckPlayMode"/>.
    /// </summary>
    public enum PermissionPlayModeOperation
    {
        /// <summary> Enter play mode </summary>
        Enter,

        /// <summary> Exit play mode </summary>
        Exit
    }
}
