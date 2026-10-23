namespace Unity.AI.Assistant.Integrations.Profiler.Editor
{
    /// <summary>
    /// A single curated counter: its display category, the exact counter name used to
    /// resolve a marker id, its native unit, and a one-line description.
    /// </summary>
    readonly struct CuratedCounter
    {
        public readonly string Category;
        public readonly string Name;
        public readonly CounterUnit Unit;
        public readonly string Description;

        public CuratedCounter(string category, string name, CounterUnit unit, string description)
        {
            Category = category;
            Name = name;
            Unit = unit;
            Description = description;
        }
    }
}
