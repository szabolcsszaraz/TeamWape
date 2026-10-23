namespace Unity.AI.Toolkit.Accounts.Services
{
    [System.Flags]
    enum ConnectionLimitSource
    {
        None = 0,
        Backend = 1,
        Licensing = 2,
        ProFallback = 4
    }
}
