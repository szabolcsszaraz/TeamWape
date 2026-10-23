using NUnit.Framework;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace UnityEditor.U2D.Sprites.EditorTests
{
    internal class SpriteUtilityWindowTests
    {
        float m_InitialMinZoomLevel;
        float m_InitialMaxZoomLevel;

        // Test window that exposes protected members for testing
        private class TestableWindow : SpriteUtilityWindow
        {
            public float GetZoom() => m_Zoom;
            public void SetZoom(float value) => m_Zoom = value;
            public Rect GetTextureViewRect() => textureViewRect;
            public void SetTextureViewRect(Rect rect) => textureViewRect = rect;

            public ITexture2D GetTexture() => m_Texture;
            public void SetTextureInternal(ITexture2D texture) => m_Texture = texture;

            public float CallGetDefaultZoom() => GetDefaultZoom();
            public float CallGetFitToViewZoom() => GetFitToViewZoom();
            public float CallGetMinZoom() => GetMinZoom();
            public float CallGetMaxZoom() => GetMaxZoom();

            public void CallSetNewTexture(Texture2D texture) => SetNewTexture(texture);
        }

        private TestableWindow m_Window;

        [SetUp]
        public void SetUp()
        {
            m_InitialMinZoomLevel = SpriteEditorWindowSettings.minZoomLevel;
            m_InitialMaxZoomLevel = SpriteEditorWindowSettings.maxZoomLevel;
            SpriteEditorWindowSettings.minZoomLevel = 0.5f;
            SpriteEditorWindowSettings.maxZoomLevel = 50.0f;
            m_Window = ScriptableObject.CreateInstance<TestableWindow>();
        }

        [TearDown]
        public void TearDown()
        {
            SpriteEditorWindowSettings.minZoomLevel = m_InitialMinZoomLevel;
            SpriteEditorWindowSettings.maxZoomLevel = m_InitialMaxZoomLevel;
            if (m_Window != null)
                Object.DestroyImmediate(m_Window);
        }

        [Test]
        public void GetDefaultZoom_WhenTextureViewRectIsZero_ReturnsZero()
        {
            // Setup: Create a texture but leave textureViewRect at zero
            Texture2D texture = new Texture2D(100, 100);
            try
            {
                m_Window.SetTextureInternal(new Texture2DWrapper(texture));
                m_Window.SetTextureViewRect(new Rect(0.0f, 0.0f, 0.0f, 0.0f));

                // Act
                float defaultZoom = m_Window.CallGetDefaultZoom();

                // Assert
                Assert.AreEqual(0.0f, defaultZoom, "GetDefaultZoom should return 0 when textureViewRect has zero dimensions");
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void GetDefaultZoom_WhenTextureViewRectIsValid_ReturnsValidZoom()
        {
            // Setup
            Texture2D texture = new Texture2D(100, 100);
            try
            {
                m_Window.SetTextureInternal(new Texture2DWrapper(texture));
                m_Window.SetTextureViewRect(new Rect(0.0f, 0.0f, 800.0f, 600.0f));

                // Act
                float defaultZoom = m_Window.CallGetDefaultZoom();

                // Assert
                Assert.Greater(defaultZoom, 0.0f, "GetDefaultZoom should return a positive value when textureViewRect is valid");

                // defaultZoom should be approximately (min(800/100, 600/100) * 0.9) = min(8, 6) * 0.9 = 5.4
                float expected = 6.0f * 0.9f;
                Assert.AreEqual(expected, defaultZoom, 0.01f, "GetDefaultZoom should calculate correct zoom value");
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void ZoomLevel_WhenZoomIsNegative_InitializesWithGetDefaultZoom()
        {
            // Setup
            Texture2D texture = new Texture2D(100, 100);
            try
            {
                m_Window.SetTextureInternal(new Texture2DWrapper(texture));
                m_Window.SetTextureViewRect(new Rect(0.0f, 0.0f, 800.0f, 600.0f));
                m_Window.SetZoom(-1.0f); // Uninitialized state

                // Act
                float zoomLevel = m_Window.zoomLevel;

                // Assert
                Assert.Greater(zoomLevel, 0.0f, "zoomLevel should be initialized to a positive value");
                Assert.AreEqual(m_Window.CallGetDefaultZoom(), zoomLevel, 0.01f, "zoomLevel should be initialized with GetDefaultZoom");
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void ZoomLevel_WhenZoomIsZero_ReinitializesWithGetDefaultZoom()
        {
            // Setup: Simulate the scenario where GetFitToViewZoom returns 0 initially
            Texture2D texture = new Texture2D(100, 100);
            try
            {
                m_Window.SetTextureInternal(new Texture2DWrapper(texture));
                m_Window.SetTextureViewRect(new Rect(0.0f, 0.0f, 0.0f, 0.0f)); // Zero dimensions
                m_Window.SetZoom(-1.0f);

                // First access with zero textureViewRect - should set m_Zoom to 0
                float _ = m_Window.zoomLevel;
                Assert.AreEqual(0.0f, m_Window.GetZoom(), "m_Zoom should be 0 when textureViewRect is zero");

                // Act: Update textureViewRect to valid dimensions
                m_Window.SetTextureViewRect(new Rect(0.0f, 0.0f, 800.0f, 600.0f));

                // Second access - should recalculate because m_Zoom <= 0
                float zoomLevel = m_Window.zoomLevel;

                // Assert
                Assert.Greater(zoomLevel, 0.0f, "zoomLevel should be recalculated to a positive value");
                Assert.Greater(m_Window.GetZoom(), 0.0f, "m_Zoom should be updated to a positive value");
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void ZoomLevel_Setter_WhenZoomIsPositive_ClampsToValidRange()
        {
            // Setup
            Texture2D texture = new Texture2D(100, 100);
            try
            {
                m_Window.SetTextureInternal(new Texture2DWrapper(texture));
                m_Window.SetTextureViewRect(new Rect(0.0f, 0.0f, 800.0f, 600.0f));
                m_Window.SetZoom(5.0f); // Valid positive value

                // Act: Try to set a very large zoom
                m_Window.zoomLevel = 1000.0f;

                // Assert: Should be clamped to max zoom
                float maxZoom = m_Window.CallGetMaxZoom();
                Assert.LessOrEqual(m_Window.GetZoom(), maxZoom, "zoomLevel should be clamped to max zoom");
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void SetNewTexture_ResetsZoomToNegativeOne()
        {
            // Setup
            Texture2D texture1 = new Texture2D(100, 100);
            Texture2D texture2 = new Texture2D(200, 200);
            try
            {
                m_Window.SetTextureInternal(new Texture2DWrapper(texture1));
                m_Window.SetTextureViewRect(new Rect(0.0f, 0.0f, 800.0f, 600.0f));

                // Initialize zoom to a valid value
                float _ = m_Window.zoomLevel;
                Assert.Greater(m_Window.GetZoom(), 0.0f, "Zoom should be initialized");

                // Act: Set a new texture
                m_Window.CallSetNewTexture(texture2);

                // Assert
                Assert.AreEqual(-1.0f, m_Window.GetZoom(), "m_Zoom should be reset to -1 when texture changes");
            }
            finally
            {
                Object.DestroyImmediate(texture1);
                Object.DestroyImmediate(texture2);
            }
        }

        [Test]
        public void ScrollPosition_WhenZoomIsZero_InitializesZoom()
        {
            // Setup
            Texture2D texture = new Texture2D(100, 100);
            try
            {
                m_Window.SetTextureInternal(new Texture2DWrapper(texture));
                m_Window.SetTextureViewRect(new Rect(0.0f, 0.0f, 800.0f, 600.0f));
                m_Window.SetZoom(0.0f); // Zero state

                // Act: Access scrollPosition which should trigger zoom initialization
                m_Window.scrollPosition = new Vector2(10.0f, 10.0f);

                // Assert
                Assert.Greater(m_Window.GetZoom(), 0.0f, "m_Zoom should be initialized when scrollPosition is set");
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void GetMinZoom_WhenDefaultZoomIsZero_ReturnsConstantMinZoom()
        {
            // Setup: Create scenario where GetDefaultZoom returns 0
            Texture2D texture = new Texture2D(100, 100);
            try
            {
                m_Window.SetTextureInternal(new Texture2DWrapper(texture));
                m_Window.SetTextureViewRect(new Rect(0.0f, 0.0f, 0.0f, 0.0f)); // Zero dimensions

                // Act
                float minZoom = m_Window.CallGetMinZoom();

                // Assert
                Assert.AreEqual(SpriteEditorWindowSettings.minZoomLevel, minZoom, "GetMinZoom should return min zoom from preferences when defaultZoom is 0");
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void GetMaxZoom_WhenDefaultZoomIsZero_ReturnsConstantMaxZoom()
        {
            // Setup: Create scenario where GetDefaultZoom returns 0
            Texture2D texture = new Texture2D(100, 100);
            try
            {
                m_Window.SetTextureInternal(new Texture2DWrapper(texture));
                m_Window.SetTextureViewRect(new Rect(0.0f, 0.0f, 0.0f, 0.0f)); // Zero dimensions

                // Act
                float maxZoom = m_Window.CallGetMaxZoom();

                // Assert
                Assert.AreEqual(SpriteEditorWindowSettings.maxZoomLevel, maxZoom, "GetMaxZoom should return max zoom from preferences when defaultZoom is 0");
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void GetMinZoom_WhenDefaultZoomIsZero_UsesCustomMinZoomFromSettings()
        {
            Texture2D texture = new Texture2D(100, 100);
            try
            {
                SpriteEditorWindowSettings.minZoomLevel = 0.75f;
                m_Window.SetTextureInternal(new Texture2DWrapper(texture));
                m_Window.SetTextureViewRect(new Rect(0.0f, 0.0f, 0.0f, 0.0f));

                Assert.AreEqual(0.75f, m_Window.CallGetMinZoom(), 1e-5f);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void GetMaxZoom_WhenDefaultZoomIsZero_UsesCustomMaxZoomFromSettings()
        {
            Texture2D texture = new Texture2D(100, 100);
            try
            {
                SpriteEditorWindowSettings.maxZoomLevel = 25.0f;
                m_Window.SetTextureInternal(new Texture2DWrapper(texture));
                m_Window.SetTextureViewRect(new Rect(0.0f, 0.0f, 0.0f, 0.0f));

                Assert.AreEqual(25.0f, m_Window.CallGetMaxZoom(), 1e-5f);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void GetFitToViewZoom_IsCappedByMaxZoomFromSettings()
        {
            Texture2D texture = new Texture2D(100, 100);
            try
            {
                SpriteEditorWindowSettings.maxZoomLevel = 12.5f;
                m_Window.SetTextureInternal(new Texture2DWrapper(texture));
                m_Window.SetTextureViewRect(new Rect(0.0f, 0.0f, 5000.0f, 5000.0f));

                Assert.AreEqual(12.5f, m_Window.CallGetFitToViewZoom(), 1e-5f);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
