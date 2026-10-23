using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.FunctionCalling;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    /// <summary>
    /// UI component for configuring default permissions for the Assistant.
    /// </summary>
    [UxmlElement]
    partial class ConfigureDefaultPermissionsView : ManagedTemplate
    {
        // Centralized mapping between permission policy enum values and their string representations
        static readonly Dictionary<IPermissionsPolicyProvider.PermissionPolicy, string> k_PolicyToStringMap = new()
        {
            { IPermissionsPolicyProvider.PermissionPolicy.Allow, "Allow" },
            { IPermissionsPolicyProvider.PermissionPolicy.Ask, "Ask Permission" },
            { IPermissionsPolicyProvider.PermissionPolicy.Deny, "Deny" }
        };

        // List of choices for dropdown, derived from the centralized mapping
        static readonly List<string> k_PermissionChoices = k_PolicyToStringMap.Values.ToList();

        DropdownField m_ReadExternalFilesDropdown;
        DropdownField m_ReadProjectDropdown;
        DropdownField m_ModifyProjectDropdown;
        DropdownField m_PlayModeDropdown;
        DropdownField m_ScreenCaptureDropdown;
        DropdownField m_CodeExecutionDropdown;
        DropdownField m_AssetGenerationDropdown;
        DropdownField m_ThirdPartyToolsDropdown;

        public ConfigureDefaultPermissionsView() : base(AssistantUIConstants.UIModulePath)
        {
            RegisterAttachEvents(OnAttachedToPanel, OnDetachedFromPanel);
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_ReadExternalFilesDropdown = InitializeDropdown(view, "readExternalFiles", AssistantEditorPreferences.Permissions.ReadExternalFilesPolicy, p => AssistantEditorPreferences.Permissions.ReadExternalFilesPolicy = p);
            m_ReadProjectDropdown = InitializeDropdown(view, "readProject", AssistantEditorPreferences.Permissions.ReadProjectPolicy, p => AssistantEditorPreferences.Permissions.ReadProjectPolicy = p);
            m_ModifyProjectDropdown = InitializeDropdown(view, "modifyProject", AssistantEditorPreferences.Permissions.ModifyProjectPolicy, p => AssistantEditorPreferences.Permissions.ModifyProjectPolicy = p);
            m_PlayModeDropdown = InitializeDropdown(view, "playMode", AssistantEditorPreferences.Permissions.PlayModePolicy, p => AssistantEditorPreferences.Permissions.PlayModePolicy = p);
            m_ScreenCaptureDropdown = InitializeDropdown(view, "screenCapture", AssistantEditorPreferences.Permissions.ScreenCapturePolicy, p => AssistantEditorPreferences.Permissions.ScreenCapturePolicy = p);
            m_CodeExecutionDropdown = InitializeDropdown(view, "codeExecution", AssistantEditorPreferences.Permissions.CodeExecutionPolicy, p => AssistantEditorPreferences.Permissions.CodeExecutionPolicy = p);
            m_AssetGenerationDropdown = InitializeDropdown(view, "assetGeneration", AssistantEditorPreferences.Permissions.AssetGenerationPolicy, p => AssistantEditorPreferences.Permissions.AssetGenerationPolicy = p);
            m_ThirdPartyToolsDropdown = InitializeDropdown(view, "thirdPartyTools", AssistantEditorPreferences.Permissions.ThirdPartyToolPolicy, p => AssistantEditorPreferences.Permissions.ThirdPartyToolPolicy = p);
        }

        void OnAttachedToPanel(AttachToPanelEvent evt)
        {
            AssistantEditorPreferences.Permissions.ReadExternalFilesPolicyChanged += OnReadExternalFilesPolicyChangedExternally;
            AssistantEditorPreferences.Permissions.ReadProjectPolicyChanged += OnReadProjectPolicyChangedExternally;
            AssistantEditorPreferences.Permissions.ModifyProjectPolicyChanged += OnModifyProjectPolicyChangedExternally;
            AssistantEditorPreferences.Permissions.PlayModePolicyChanged += OnPlayModePolicyChangedExternally;
            AssistantEditorPreferences.Permissions.ScreenCapturePolicyChanged += OnScreenCapturePolicyChangedExternally;
            AssistantEditorPreferences.Permissions.CodeExecutionPolicyChanged += OnCodeExecutionPolicyChangedExternally;
            AssistantEditorPreferences.Permissions.AssetGenerationPolicyChanged += OnAssetGenerationPolicyChangedExternally;
            AssistantEditorPreferences.Permissions.ThirdPartyToolPolicyChanged += OnThirdPartyToolsPolicyChangedExternally;
        }

        void OnDetachedFromPanel(DetachFromPanelEvent evt)
        {
            AssistantEditorPreferences.Permissions.ReadExternalFilesPolicyChanged -= OnReadExternalFilesPolicyChangedExternally;
            AssistantEditorPreferences.Permissions.ReadProjectPolicyChanged -= OnReadProjectPolicyChangedExternally;
            AssistantEditorPreferences.Permissions.ModifyProjectPolicyChanged -= OnModifyProjectPolicyChangedExternally;
            AssistantEditorPreferences.Permissions.PlayModePolicyChanged -= OnPlayModePolicyChangedExternally;
            AssistantEditorPreferences.Permissions.ScreenCapturePolicyChanged -= OnScreenCapturePolicyChangedExternally;
            AssistantEditorPreferences.Permissions.CodeExecutionPolicyChanged -= OnCodeExecutionPolicyChangedExternally;
            AssistantEditorPreferences.Permissions.AssetGenerationPolicyChanged -= OnAssetGenerationPolicyChangedExternally;
            AssistantEditorPreferences.Permissions.ThirdPartyToolPolicyChanged -= OnThirdPartyToolsPolicyChangedExternally;
        }

        DropdownField InitializeDropdown(TemplateContainer view, string settingName, IPermissionsPolicyProvider.PermissionPolicy policy, Action<IPermissionsPolicyProvider.PermissionPolicy> setter)
        {
            var dropdown = view.Q<DropdownField>($"{settingName}Dropdown");
            dropdown.choices = k_PermissionChoices;
            dropdown.value = PolicyToString(policy);
            dropdown.RegisterValueChangedCallback(evt => OnPolicyChanged(evt, settingName, setter));
            return dropdown;
        }

        static void OnPolicyChanged(ChangeEvent<string> evt, string settingName, Action<IPermissionsPolicyProvider.PermissionPolicy> setter)
        {
            var policy = StringToPolicy(evt.newValue);
            setter(policy);
            AIAssistantAnalytics.ReportUITriggerLocalPermissionSettingChangedEvent(settingName, policy);
        }

        void OnReadExternalFilesPolicyChangedExternally(IPermissionsPolicyProvider.PermissionPolicy newPolicy) =>
            m_ReadExternalFilesDropdown.SetValueWithoutNotify(PolicyToString(newPolicy));

        void OnReadProjectPolicyChangedExternally(IPermissionsPolicyProvider.PermissionPolicy newPolicy) =>
            m_ReadProjectDropdown.SetValueWithoutNotify(PolicyToString(newPolicy));

        void OnModifyProjectPolicyChangedExternally(IPermissionsPolicyProvider.PermissionPolicy newPolicy) =>
            m_ModifyProjectDropdown.SetValueWithoutNotify(PolicyToString(newPolicy));

        void OnPlayModePolicyChangedExternally(IPermissionsPolicyProvider.PermissionPolicy newPolicy) =>
            m_PlayModeDropdown.SetValueWithoutNotify(PolicyToString(newPolicy));

        void OnScreenCapturePolicyChangedExternally(IPermissionsPolicyProvider.PermissionPolicy newPolicy) =>
            m_ScreenCaptureDropdown.SetValueWithoutNotify(PolicyToString(newPolicy));

        void OnCodeExecutionPolicyChangedExternally(IPermissionsPolicyProvider.PermissionPolicy newPolicy) =>
            m_CodeExecutionDropdown.SetValueWithoutNotify(PolicyToString(newPolicy));

        void OnAssetGenerationPolicyChangedExternally(IPermissionsPolicyProvider.PermissionPolicy newPolicy) =>
            m_AssetGenerationDropdown.SetValueWithoutNotify(PolicyToString(newPolicy));

        void OnThirdPartyToolsPolicyChangedExternally(IPermissionsPolicyProvider.PermissionPolicy newPolicy) =>
            m_ThirdPartyToolsDropdown.SetValueWithoutNotify(PolicyToString(newPolicy));

        static string PolicyToString(IPermissionsPolicyProvider.PermissionPolicy policy) => k_PolicyToStringMap[policy];

        static IPermissionsPolicyProvider.PermissionPolicy StringToPolicy(string value)
        {
            var kvp = k_PolicyToStringMap.FirstOrDefault(kvp => kvp.Value == value);
            if (kvp.Value == null)
                throw new ArgumentException($"Unknown permission policy string: {value}");
            return kvp.Key;
        }
    }
}
