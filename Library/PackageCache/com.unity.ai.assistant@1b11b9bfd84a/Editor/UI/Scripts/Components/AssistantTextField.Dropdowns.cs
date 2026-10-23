using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Acp;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using Unity.AI.Assistant.Utils;
using Unity.Relay.Editor.Acp;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    partial class AssistantTextField
    {
        const string k_ModelTag = "model";
        const string k_EmDash = "\u2014";
        const string k_AddAgentActionId = "__add_agent__";

        Button m_ModeButton;
        Label m_ModeLabel;
        AssistantImage m_ModeIcon;
        AssistantDropdown m_ModeMenu;
        ModeProvider m_ModeProvider;

        Button m_ProviderButton;
        Label m_ProviderLabel;
        AssistantImage m_ProviderIcon;
        AssistantDropdown m_ProviderMenu;
        List<AssistantDropdownItemData> m_ProviderItems = new();

        // USS class names for the provider-selector capacity-fallback highlight (see AssistantTextField.uss).
        const string k_ProviderFlashClass = "mui-provider-flash";
        const string k_ProviderHighlightClass = "mui-provider-highlight";
        IVisualElementScheduledItem m_ProviderFlashPulse;

        Button m_CommandButton;
        AssistantDropdown m_CommandMenu;
        List<AssistantDropdownItemData> m_CommandMenuItems = new();

        void InitializeDropdowns(TemplateContainer view)
        {
            m_ModeButton = view.SetupButton("modeDropdown", _ => ToggleDropdown(m_ModeMenu, m_ModeButton));
            m_ModeLabel = m_ModeButton.Q<Label>("modeDropdownLabel");
            m_ModeIcon = new AssistantImage(m_ModeButton.Q<Image>("modeDropdownIcon"), autoHide: true);
            m_ModeProvider = new ModeProvider(Context.Blackboard);
            m_ModeProvider.ModeChanged += _ => RefreshModeDropdown();
            m_ProviderButton = view.SetupButton("providerSelector", _ => ToggleDropdown(m_ProviderMenu, m_ProviderButton));
            m_ProviderLabel = m_ProviderButton.Q<Label>("providerSelectorLabel");
            m_ProviderIcon = new AssistantImage(m_ProviderButton.Q<Image>("providerSelectorIcon"), autoHide: true);

            AcpProvidersRegistry.EnsureInitialized();

            m_CommandButton = view.SetupButton("commandSelector", _ => ToggleDropdown(m_CommandMenu, m_CommandButton));

            UpdateCommandSelectorVisibility();
            UpdateCommandSelectorEnabled();
        }

        void InitializeDropdownMenus(VisualElement popupRoot)
        {
            m_ModeMenu = CreateDropdownMenu(popupRoot, OnModeItemSelected);
            m_ProviderMenu = CreateDropdownMenu(popupRoot, OnProviderMenuItemSelected);
            m_CommandMenu = CreateDropdownMenu(popupRoot, OnCommandMenuItemSelected);

            RefreshModeDropdown();
            RefreshProviderItems();
        }

        AssistantDropdown CreateDropdownMenu(VisualElement popupRoot, Action<string> onSelected)
        {
            var menu = new AssistantDropdown();
            menu.Initialize(Context, autoShowControl: false);
            popupRoot.Add(menu);
            menu.ItemSelected += onSelected;
            return menu;
        }

        static void ToggleDropdown(AssistantDropdown menu, Button button)
        {
            if (menu == null)
            {
                return;
            }

            if (menu.IsShown)
            {
                menu.HideMenu();
            }
            else
            {
                menu.ShowAt(button, button);
            }
        }

        void RefreshModeDropdown()
        {
            var items = new List<AssistantDropdownItemData>();

            if (m_ModeProvider.AvailableModes.Count == 0)
            {
                m_ModeLabel.text = k_EmDash;
                m_ModeMenu.SetItems(items, null);
                return;
            }

            foreach (var mode in m_ModeProvider.AvailableModes)
            {
                items.Add(new AssistantDropdownItemData(mode.Id, mode.Name));
            }

            m_ModeMenu.SetItems(items, m_ModeProvider.CurrentModeId);

            var currentItem = items.FirstOrDefault(i => i.Id == m_ModeProvider.CurrentModeId);
            m_ModeLabel.text = currentItem.DisplayText ?? items.FirstOrDefault().DisplayText ?? k_EmDash;
            m_ModeIcon.SetIconClassName(currentItem.IconClass);
        }

        async void OnModeItemSelected(string modeId)
        {
            // Update label immediately so the button reflects the selection
            var selected = m_ModeProvider.AvailableModes.FirstOrDefault(m => m.Id == modeId);
            if (selected != null)
            {
                m_ModeLabel.text = selected.Name;
            }

            var conversationId = Context.Blackboard.ActiveConversationId;
            try
            {
                await m_ModeProvider.SetModeAsync(modeId);
                AIAssistantAnalytics.ReportUITriggerLocalModeSwitchedEvent(conversationId, modeId);
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning($"[ModeDropdown] Failed to set mode: {ex.Message}");
            }
        }

        async void OnPlanApproved()
        {
            try
            {
                await m_ModeProvider.SetModeAsync("Agent");
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning($"[ModeDropdown] Failed to switch to Agent mode after plan approval: {ex.Message}");
            }
        }

        async void OnEnterPlanModeRequested()
        {
            try
            {
                await m_ModeProvider.SetModeAsync("Plan");
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning($"[ModeDropdown] Failed to switch to Plan mode after Unity.EnterPlanMode tool call: {ex.Message}");
            }
        }

        void RefreshProviderItems()
        {
            m_ProviderItems.Clear();

            // 1. Always add Unity provider(s)
            var profiles = Context.AvailableUnityModelProfiles;
            if (profiles is { Count: > 0 })
            {
                foreach (var (providerId, profileName, tooltip) in profiles)
                    m_ProviderItems.Add(new AssistantDropdownItemData(providerId, profileName ?? providerId, tooltip: tooltip));
            }
            else
            {
                m_ProviderItems.Add(new AssistantDropdownItemData(AssistantProviderFactory.DefaultProvider.ProfileId, "Unity"));
            }

            // 2. Add ACP providers only if disclaimer accepted AND provider enabled
            var disclaimerAccepted = AssistantEditorPreferences.GetAiGatewayDisclaimerAccepted();
            var hasDisabledProviders = false;

            foreach (var p in AcpProvidersRegistry.Providers)
            {
                if (!disclaimerAccepted || !AssistantEditorPreferences.GetProviderEnabled(p.Id))
                {
                    hasDisabledProviders = true;
                    continue;
                }

                m_ProviderItems.Add(new AssistantDropdownItemData(
                    p.Id,
                    string.IsNullOrEmpty(p.DisplayName) ? p.Id : p.DisplayName));
            }

            // 3. Show "+ Add agent" if any provider is disabled
            if (hasDisabledProviders || (!disclaimerAccepted && AcpProvidersRegistry.Providers.Count > 0))
            {
                m_ProviderItems.Add(new AssistantDropdownItemData(
                    k_AddAgentActionId, "+ Add agent", isAction: true));
            }

            // Default to the first profile (backend order) when nothing valid is selected.
            // The guard guarantees at least one profile item was added above, so First() is safe.
            var previousId = m_SelectedProviderId;
            if (profiles is { Count: > 0 } && m_ProviderItems.All(i => i.Id != m_SelectedProviderId))
                m_SelectedProviderId = m_ProviderItems.First(i => !i.IsAction).Id;

            m_ProviderMenu.SetItems(m_ProviderItems, m_SelectedProviderId);
            UpdateProviderLabel();
            UpdateProviderTooltip();

            if (m_SelectedProviderId != previousId)
                OnProviderChangedHandler(previousId, m_SelectedProviderId);
        }

        void UpdateProviderLabel()
        {
            var item = m_ProviderItems.FirstOrDefault(i => i.Id == m_SelectedProviderId);
            m_ProviderLabel.text = item.DisplayText ?? m_ProviderItems.FirstOrDefault().DisplayText ?? "";
            m_ProviderIcon.SetIconClassName(item.IconClass);
        }

        /// <summary>
        /// Pulse the provider selector blue several times (with a whiter border during the pulse),
        /// then settle on a held blue highlight. Draws the user's eye to the model picker after an
        /// automatic capacity fallback; the highlight persists until
        /// <see cref="ClearProviderSelectorHighlight"/> is called (banner dismissed or model changed).
        /// </summary>
        public void FlashProviderSelector()
        {
            if (m_ProviderButton == null)
                return;

            // Restart cleanly if a previous flash/highlight is still active.
            m_ProviderFlashPulse?.Pause();
            ResetProviderHighlightClasses();

            // Toggle the flash class on a timer to blink (each toggle animates over the 200ms USS
            // transition). After the pulses, settle into the persistent blue highlight.
            const int totalToggles = 4; // 2 blue pulses, then settle on held blue
            const long halfPeriodMs = 240;
            var count = 0;

            m_ProviderFlashPulse = m_ProviderButton.schedule.Execute(() =>
            {
                count++;
                if (count >= totalToggles)
                {
                    // Settle on held blue: drop the white-border flash, keep the blue background.
                    m_ProviderButton.RemoveFromClassList(k_ProviderFlashClass);
                    m_ProviderButton.AddToClassList(k_ProviderHighlightClass);
                    m_ProviderFlashPulse?.Pause();
                    return;
                }

                m_ProviderButton.ToggleInClassList(k_ProviderFlashClass);
            }).Every(halfPeriodMs);
        }

        /// <summary>
        /// Clear the blue highlight/flash applied by <see cref="FlashProviderSelector"/> — called when
        /// the capacity banner is dismissed, the user changes the model, or the field detaches.
        /// </summary>
        public void ClearProviderSelectorHighlight()
        {
            if (m_ProviderButton == null)
                return;

            m_ProviderFlashPulse?.Pause();
            ResetProviderHighlightClasses();
        }

        void ResetProviderHighlightClasses()
        {
            m_ProviderButton.RemoveFromClassList(k_ProviderFlashClass);
            m_ProviderButton.RemoveFromClassList(k_ProviderHighlightClass);
        }

        void UpdateProviderTooltip()
        {
            if (!string.IsNullOrEmpty(m_SelectedProviderId))
            {
                var selected = m_ProviderItems.FirstOrDefault(i => i.Id == m_SelectedProviderId);
                if (selected.Id != null && !string.IsNullOrEmpty(selected.Tooltip))
                {
                    m_ProviderButton.tooltip = selected.Tooltip;
                    return;
                }
            }

            var displayName = GetProviderDisplayName(m_SelectedProviderId);
            var provider = AcpProvidersRegistry.Providers.FirstOrDefault(p => p.Id == m_SelectedProviderId);

            if (provider != null && !string.IsNullOrEmpty(provider.Version))
            {
                var tooltip = $"{displayName} v{provider.Version}";
                if (provider.IsCustom)
                {
                    tooltip += "  [Custom]";
                }
                m_ProviderButton.tooltip = tooltip;
            }
            else
            {
                m_ProviderButton.tooltip = string.IsNullOrEmpty(displayName) ? null : displayName;
            }
        }

        void OnProviderMenuItemSelected(string id)
        {
            if (id == k_AddAgentActionId)
            {
                SettingsService.OpenUserPreferences("Preferences/AI/Gateway");
                return;
            }

            if (id == m_SelectedProviderId)
            {
                return;
            }

            var item = m_ProviderItems.FirstOrDefault(i => i.Id == id);
            if (item.Id == null)
            {
                return;
            }

            // User explicitly chose a model from the dropdown — clear any capacity-fallback highlight.
            ClearProviderSelectorHighlight();

            var oldProvider = m_SelectedProviderId;
            m_SelectedProviderId = id;
            UpdateProviderLabel();
            UpdateProviderTooltip();
            OnProviderChangedHandler(oldProvider, id);
        }

        void UpdateCommandSelectorVisibility()
        {
            var isThirdPartyProvider = !AssistantProviderFactory.IsUnityProvider(m_SelectedProviderId);
            m_CommandButton.SetDisplay(isThirdPartyProvider);
        }

        void UpdateCommandSelectorEnabled()
        {
            m_CommandButton.SetEnabled(m_CommandItems.Count > 0 || m_ModelItems.Count > 0);
        }

        void RefreshCommandSelectorItems()
        {
            m_CommandMenuItems.Clear();

            foreach (var model in m_ModelItems)
            {
                m_CommandMenuItems.Add(new AssistantDropdownItemData(model.id, model.displayName, tag: k_ModelTag));
            }

            foreach (var command in m_CommandItems)
            {
                m_CommandMenuItems.Add(new AssistantDropdownItemData(command.id, command.displayName));
            }

            m_CommandMenu.SetItems(m_CommandMenuItems, m_SelectedModelId);
            UpdateCommandSelectorEnabled();
        }

        void OnCommandMenuItemSelected(string id)
        {
            var item = m_CommandMenuItems.FirstOrDefault(i => i.Id == id);
            if (item.Id == null)
            {
                return;
            }

            if ((string)item.Tag == k_ModelTag)
            {
                m_SelectedModelId = id;
                OnModelSelected?.Invoke(id);
            }
            else
            {
                OnCommandSelected?.Invoke(id);
            }
        }
    }
}
