using System.Collections.Generic;
using System.Linq;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Utils
{
    static class StringUtils
    {
        public static bool StartsWithAnyLinq(string input, IEnumerable<string> prefixes)
        {
            // Using LINQ
            return prefixes.Any(prefix => prefix.StartsWith(input));
        }

        /// <summary>
        /// Prepares a menu item label for the current editor platform so that ampersands render literally.
        /// Windows native menus treat '&' as a mnemonic prefix and collapse "&&" down to a single '&',
        /// while macOS menus have no such escaping and would render "&&" verbatim. Author labels with a
        /// single '&' and pass them through here.
        /// </summary>
        public static string EscapeMenuAmpersands(string label)
        {
#if UNITY_EDITOR_WIN
            return label?.Replace("&", "&&");
#else
            return label;
#endif
        }
    }
}
