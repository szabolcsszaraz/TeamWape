using System.Collections.Generic;

namespace Unity.AI.Assistant.Skills
{
    static class SkillRegistryTags
    {
        public const string Project  = "Skills.User.Filesystem.Project";
        public const string User     = "Skills.User.Filesystem.AppData";
        public const string Package  = "Skills.User.Filesystem.Package";
        public const string Internal = "Skills.Filesystem";
        
        // Skills registered via other internal code (not SkillsScanner). Always pass the opt-in filter; never cleared by file scanners.
        public const string BuiltIn  = "Skills.BuiltIn";

        public static readonly HashSet<string> All = new() { Project, User, Package, Internal, BuiltIn };

        public static bool IsInternalOrBuiltIn(string tag)
            => tag == Internal || tag == BuiltIn;

        public static bool IsInternalOrBuiltIn(List<string> tags)
        {
            if (tags == null) return false;
            foreach (var tag in tags)
                if (tag == Internal || tag == BuiltIn) return true;
            return false;
        }
    }
}
