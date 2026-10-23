using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Utils
{
    static class SettingsProviderSearchHelper
    {
        internal static Func<string, bool> CreateSearchHandler(
            Action<string, VisualElement> activateHandler, params string[] baselineKeywords)
        {
            HashSet<string> cachedKeywords = null;

            return searchContext =>
            {
                cachedKeywords ??= GetKeywordsFromUI(activateHandler, baselineKeywords);

                foreach (var keyword in cachedKeywords)
                {
                    if (keyword.IndexOf(searchContext, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }

                return false;
            };
        }

        static HashSet<string> GetKeywordsFromUI(
            Action<string, VisualElement> activateHandler, string[] baselineKeywords)
        {
            var keywords = new HashSet<string>(baselineKeywords);

            var root = new VisualElement();
            activateHandler(string.Empty, root);

            root.Query<TextElement>().ForEach(element =>
            {
                var text = element.text;
                if (string.IsNullOrEmpty(text))
                    return;

                text = Regex.Replace(text, "<.*?>", "");
                keywords.Add(text);
            });

            return keywords;
        }
    }
}
