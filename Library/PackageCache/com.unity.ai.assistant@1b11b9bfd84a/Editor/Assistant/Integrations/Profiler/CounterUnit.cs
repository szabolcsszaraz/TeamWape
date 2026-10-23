namespace Unity.AI.Assistant.Integrations.Profiler.Editor
{
    /// <summary>
    /// The native unit of a profiler counter's raw value, as returned by the per-frame
    /// counter accessors. Used to label values reported by the counter tools.
    /// </summary>
    enum CounterUnit
    {
        TimeNanoseconds,
        Bytes,
        Count
    }
}
