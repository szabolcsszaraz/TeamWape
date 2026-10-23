using System;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.UserInteraction
{
    interface INavigableInteractionView
    {
        int NavigationIndex { get; }
        int NavigationCount { get; }
        event Action NavigationChanged;
        void NavigatePrev();
        void NavigateNext();
    }
}
