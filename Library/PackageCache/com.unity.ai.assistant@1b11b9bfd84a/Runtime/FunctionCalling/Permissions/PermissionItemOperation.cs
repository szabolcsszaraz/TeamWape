namespace Unity.AI.Assistant.FunctionCalling
{
    /// <summary>
    /// The type of operation to be performed
    /// </summary>
    public enum PermissionItemOperation
    {
        /// <summary> Only reads data </summary>
        Read,

        /// <summary> Create a new item </summary>
        Create,

        /// <summary> Delete an existing item </summary>
        Delete,

        /// <summary> Modify an existing item </summary>
        Modify
    }
}
