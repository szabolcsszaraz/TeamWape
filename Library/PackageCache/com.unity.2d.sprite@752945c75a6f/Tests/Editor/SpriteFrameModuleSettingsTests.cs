using NUnit.Framework;
using UnityEditor.U2D.Sprites.ProjectSettings;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Sprites.EditorTests
{
    internal class SpriteFrameModuleSettingsTests
    {
        class Host : EditorWindow
        {
        }

        Host m_Host;
        bool m_InitialAllowOverride;

        [SetUp]
        public void SetUp()
        {
            m_InitialAllowOverride = SpriteFrameModuleSettingsAsset.allowSpriteFrameEditCapabilityOverride;
            // The settings UI connects to SpriteFrameModuleSettingsAsset.SettingsChanged on AttachToPanelEvent, so it needs a real panel to attach to.
            m_Host = ScriptableObject.CreateInstance<Host>();
            m_Host.ShowUtility();
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Host != null)
                m_Host.Close();
            SpriteFrameModuleSettingsAsset.allowSpriteFrameEditCapabilityOverride = m_InitialAllowOverride;
        }

        [Test]
        public void WarningBox_HidesWhenOverrideIsResetExternally()
        {
            SpriteFrameModuleSettingsAsset.allowSpriteFrameEditCapabilityOverride = true;

            var ve = SpriteFrameModuleSettings.CreateSettingsProvider();
            m_Host.rootVisualElement.Add(ve);

            var toggle = ve.Q<Toggle>("spriteFrameEditCapabilityOverride");
            var warning = ve.Q<VisualElement>("spriteFrameEditCapabilityOverrideWarning");
            Assert.IsTrue(warning.visible, "Warning should be visible while the override is enabled.");

            // Simulate what SpriteFrameModuleBase.OnModuleDeactivate does when the Sprite Editor window closes.
            SpriteFrameModuleSettingsAsset.allowSpriteFrameEditCapabilityOverride = false;

            Assert.IsFalse(toggle.value, "Toggle should reflect the externally reset override.");
            Assert.IsFalse(warning.visible, "Warning should hide once the override is externally reset.");
        }
    }
}
