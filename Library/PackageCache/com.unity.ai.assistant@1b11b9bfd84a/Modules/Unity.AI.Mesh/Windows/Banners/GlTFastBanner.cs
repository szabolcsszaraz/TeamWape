using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.Mesh.Services.Stores.Selectors;
using Unity.AI.Mesh.Windows;
using Unity.AI.Toolkit.Accounts.Components;
using UnityEngine.UIElements;

namespace Unity.AI.Mesh.Components
{
    [UxmlElement]
    partial class GlTFastBanner : VisualElement
    {
        bool m_RequiresGlTFast;

        public GlTFastBanner()
        {
            AddToClassList("session-status-banner");

            this.Use(state => state.SelectRequiresGlTFast(this), requiresGlTFast =>
            {
                m_RequiresGlTFast = requiresGlTFast;
                Refresh();
            });

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                GlTFastInstaller.OnStateChanged += Refresh;
                GlTFastInstaller.RefreshInstallState();
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                GlTFastInstaller.OnStateChanged -= Refresh;
            });
        }

        void Refresh()
        {
            Clear();
            if (GlTFastInstaller.IsInstalled)
                return;

            if (GlTFastInstaller.IsInstalling)
            {
                Add(new DropdownLoading("Installing glTFast..."));
                return;
            }

            if (!m_RequiresGlTFast)
                return;

            Add(new BasicBannerContent(
                "glTFast is required for 3D operations.",
                "Install",
                GlTFastInstaller.InstallGlTFastIfNeeded));
        }
    }
}
