namespace Unity.AI.Assistant.Skills
{
    readonly struct SkillFileIssue
    {
        internal enum ErrorLevel
        {
            Info,
            Warning,
            Critical
        }
        
        public string Name { get; }
        public string Path { get; }
        public string Error { get; }
        public ErrorLevel Type { get; }
        
        // Parent folder name, pre-computed from Path to speed up sorting in UI
        public string DisplayName { get; }

        public SkillFileIssue(string name, string path, string error, ErrorLevel type)
        {
            Name = name;
            Path = path;
            Error = error;
            Type = type;
            DisplayName = string.IsNullOrEmpty(path)
                ? null
                : System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path));
        }
    }
}
