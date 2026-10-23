using System;
using Unity.AI.Toolkit.Accounts.Components;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.Editor.SessionBanner
{
    class UpdateAvailableBanner : BasicBannerContent
    {
        public UpdateAvailableBanner(string currentVersion, string latestVersion, Action onUpdate)
            : base(
                $"A new version of Assistant is available ({currentVersion} -> {latestVersion}).",
                null,
                true)
        {
            var state = PackageUpdateState.instance;

            if (state.isUpdating)
                BuildUpdatingView(latestVersion);
            else if (!string.IsNullOrEmpty(state.updateError))
                BuildErrorView(state.updateError, onUpdate);
            else
                Add(new UpdateBannerButtons(onUpdate));
        }

        void BuildUpdatingView(string latestVersion)
        {
            content.Clear();
            content.Add(new DropdownLoading($"Updating to {latestVersion}..."));
        }

        void BuildErrorView(string error, Action onRetry)
        {
            content.Clear();
            content.Add(CreateWarningIcon());
            content.Add(new RichLabel($"Update failed: {error}"));
            var row = new VisualElement();
            row.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.unity.ai.assistant/Editor/Assistant/SessionBanner/UpdateBannerButtons.uss"));
            row.AddToClassList("update-banner-actions-row");
            row.Add(UpdateBannerButtons.BuildAccentButton("Retry", onRetry));
            row.Add(new Button(PackageUpdateState.instance.Dismiss) { text = "Dismiss" });
            Add(row);
        }
    }
}
