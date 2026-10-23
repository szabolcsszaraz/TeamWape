namespace Unity.AI.Assistant.FunctionCalling
{
    /// <summary>
    /// The answer the user provided to an <see cref="IApprovalInteraction"/>,
    /// combining the allow/deny decision with the scope of that decision.
    /// </summary>
    public enum PermissionUserAnswer
    {
        /// <summary> Allow this single request; future requests will prompt again. </summary>
        AllowOnce,

        /// <summary> Allow this request and remember the choice for future identical requests. </summary>
        AllowAlways,

        /// <summary> Deny this single request; future requests will prompt again. </summary>
        DenyOnce,

        /// <summary> Deny this request and remember the choice for future identical requests. </summary>
        DenyAlways
    }
}
