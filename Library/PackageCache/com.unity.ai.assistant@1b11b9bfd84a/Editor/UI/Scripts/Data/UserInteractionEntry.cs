using System;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.UserInteraction;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Data
{
    class UserInteractionEntry
    {
        public UserInteractionId Id { get; internal set; }
        public string Title { get; set; }
        public string TitleIcon { get; set; }
        public string TitleOverride { get; set; }
        public bool HideCounter { get; set; }
        public bool HideHeader { get; set; }
        public bool Persistent { get; set; }
        public string Detail { get; set; }

        public InteractionContentView ContentView { get; set; }
        public VisualElement CustomContent { get; set; }

        public bool HideMainInput { get; set; }

        public Action OnCancel { get; set; }

        /// <summary>
        /// If set, called by the queue after the preceding entry completes.
        /// Returns true if this entry was auto-resolved (bypassing UI display),
        /// false if it still needs user interaction.
        /// </summary>
        public Func<bool> TryAutoResolve { get; set; }

        public string ExpandedTitle { get; set; }
        public Func<VisualElement> ExpandedContentFactory { get; set; }
    }
}
