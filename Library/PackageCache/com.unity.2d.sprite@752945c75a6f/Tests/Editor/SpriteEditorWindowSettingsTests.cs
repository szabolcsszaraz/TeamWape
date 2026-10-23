using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D.Sprites;

namespace UnityEditor.U2D.Sprites.EditorTests
{
    internal class SpriteEditorWindowSettingsTests
    {
        float m_InitialMinZoomLevel;
        float m_InitialMaxZoomLevel;

        [SetUp]
        public void SetUp()
        {
            m_InitialMinZoomLevel = SpriteEditorWindowSettings.minZoomLevel;
            m_InitialMaxZoomLevel = SpriteEditorWindowSettings.maxZoomLevel;
        }

        [TearDown]
        public void TearDown()
        {
            SpriteEditorWindowSettings.minZoomLevel = m_InitialMinZoomLevel;
            SpriteEditorWindowSettings.maxZoomLevel = m_InitialMaxZoomLevel;
        }

        [Test]
        public void ZoomSliderMapping_RoundTripsThroughLogScale()
        {
            float[] samples = { 0.5f, 0.75f, 1.0f, 10.0f, 25.0f, 50.0f };
            foreach (float scale in samples)
            {
                float slider = SpriteEditorWindowSettings.ZoomScaleToSlider(scale);
                float restored = SpriteEditorWindowSettings.ZoomSliderToScale(slider);
                Assert.AreEqual(scale, restored, 1e-4f, $"Round-trip failed for scale {scale} (slider t={slider})");
            }
        }

        [Test]
        public void ZoomScaleToSlider_MinLimitMapsToZero()
        {
            Assert.AreEqual(0.0f, SpriteEditorWindowSettings.ZoomScaleToSlider(0.5f), 1e-6f);
        }

        [Test]
        public void ZoomScaleToSlider_MaxLimitMapsToOne()
        {
            Assert.AreEqual(1.0f, SpriteEditorWindowSettings.ZoomScaleToSlider(50.0f), 1e-5f);
        }

        [Test]
        public void ZoomScaleToSlider_ClampsBelowMinToMin()
        {
            float slider = SpriteEditorWindowSettings.ZoomScaleToSlider(0.001f);
            Assert.AreEqual(0.0f, slider, 1e-6f);
        }

        [Test]
        public void ZoomScaleToSlider_ClampsAboveMaxToMax()
        {
            float slider = SpriteEditorWindowSettings.ZoomScaleToSlider(1000.0f);
            Assert.AreEqual(1.0f, slider, 1e-5f);
        }

        [Test]
        public void ZoomPercentMapping_RoundTrips()
        {
            float scale = 2.5f;
            float percent = SpriteEditorWindowSettings.ZoomScaleToPercent(scale);
            Assert.AreEqual(250.0f, percent, 1e-5f);
            Assert.AreEqual(scale, SpriteEditorWindowSettings.ZoomPercentToScale(percent), 1e-5f);
        }

        [Test]
        public void MinMaxZoomLevel_PersistThroughEditorPrefs()
        {
            const float minZ = 0.75f;
            const float maxZ = 25.0f;
            SpriteEditorWindowSettings.minZoomLevel = minZ;
            SpriteEditorWindowSettings.maxZoomLevel = maxZ;
            Assert.AreEqual(minZ, EditorPrefs.GetFloat(SpriteEditorWindowSettings.kMinZoomLevel, -1f), 1e-5f);
            Assert.AreEqual(maxZ, EditorPrefs.GetFloat(SpriteEditorWindowSettings.kMaxZoomLevel, -1f), 1e-5f);
            Assert.AreEqual(minZ, SpriteEditorWindowSettings.minZoomLevel, 1e-5f);
            Assert.AreEqual(maxZ, SpriteEditorWindowSettings.maxZoomLevel, 1e-5f);
        }
    }
}
