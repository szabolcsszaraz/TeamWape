namespace Unity.AI.Assistant.Editor.Checkpoint
{
    readonly struct CheckpointFileChange
    {
        public string Path { get; }
        public CheckpointFileChangeType Type { get; }

        public CheckpointFileChange(string path, CheckpointFileChangeType type) { Path = path; Type = type; }
    }
}
