using Unity.AI.Assistant.Utils.Perf;
using UnityEditor;

namespace Unity.AI.Assistant.Editor.Diagnostics
{
    // Temporary diagnostic instrumentation for UUM-146077.
    [InitializeOnLoad]
    static class AssistantPerfMenu
    {
        const string k_TogglePath = "AI Assistant/Internals/Performance Instrumentation";
        const string k_RevealPath = "AI Assistant/Internals/Reveal Performance Log";

        static AssistantPerfMenu()
        {
            AssistantPerf.Initialize();
        }

        [MenuItem(k_TogglePath, false, 2000)]
        static void Toggle()
        {
            AssistantPerf.SetEnabled(!AssistantPerf.Enabled);
            Menu.SetChecked(k_TogglePath, AssistantPerf.Enabled);
        }

        [MenuItem(k_TogglePath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(k_TogglePath, AssistantPerf.Enabled);
            return true;
        }

        [MenuItem(k_RevealPath, false, 2001)]
        static void Reveal()
        {
            AssistantPerfRecorder.Flush();
            EditorUtility.RevealInFinder(AssistantPerfRecorder.FilePath);
        }
    }
}
