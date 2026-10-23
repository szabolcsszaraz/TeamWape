using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace UnityEditor.U2D.Sprites
{
    internal class SpriteEditorWindowSettings : SettingsProvider
    {
        public const string kSettingsUniqueKey = "UnityEditor.U2D.Sprites/SpriteEditorWindow";
        public const string kShowRevertConfirmation = kSettingsUniqueKey + "RevertConfirmation";
        public const string kShowApplyConfirmation = kSettingsUniqueKey + "ApplyConfirmation";
        public const string kMinZoomLevel = kSettingsUniqueKey + "MinZoomLevel";
        public const string kMaxZoomLevel = kSettingsUniqueKey + "MaxZoomLevel";
        public static readonly GUIContent kShowRevertConfirmationLabel = EditorGUIUtility.TrTextContent("Show Revert Confirmation");
        public static readonly GUIContent kShowApplyConfirmationLabel = EditorGUIUtility.TrTextContent("Show Apply Confirmation");
        public static readonly GUIContent kZoomLevelLabel = EditorGUIUtility.TrTextContent("Zoom Level (%)");

        private const float kDefaultMinZoomLevel = 0.9f;
        private const float kDefaultMaxZoomLevel = 50.0f;
        private const float kMinZoomLevelLimit = 0.5f;
        private const float kMaxZoomLevelLimit = 50.0f;
        private const float kMinZoomDifference = 0.1f;
        private const float kZoomPercentFactor = 100.0f;
        private static readonly float s_LogMinZoomLevelLimit = Mathf.Log(kMinZoomLevelLimit);
        private static readonly float s_LogMaxZoomLevelLimit = Mathf.Log(kMaxZoomLevelLimit);
        private const float kZoomSliderSideMargin = 4.0f;

        public SpriteEditorWindowSettings() : base("Preferences/2D/Sprite Editor Window", SettingsScope.User)
        {
            guiHandler = OnGUI;
        }

        [SettingsProvider]
        private static SettingsProvider CreateSettingsProvider()
        {
            return new SpriteEditorWindowSettings()
            {
                guiHandler = SettingsGUI
            };
        }

        private static void SettingsGUI(string searchContext)
        {
            using (new SettingsWindow.GUIScope())
            {
                showApplyConfirmation = EditorGUILayout.Toggle(kShowApplyConfirmationLabel, showApplyConfirmation);
                showRevertConfirmation = EditorGUILayout.Toggle(kShowRevertConfirmationLabel, showRevertConfirmation);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(kZoomLevelLabel);

                EditorGUI.BeginChangeCheck();
                int minPercent = EditorGUILayout.IntField((int)ZoomScaleToPercent(minZoomLevel), GUILayout.Width(40.0f));
                if (EditorGUI.EndChangeCheck())
                {
                    minZoomLevel = ZoomPercentToScale((float)minPercent);
                    minZoomLevel = Mathf.Clamp(minZoomLevel, kMinZoomLevelLimit, maxZoomLevel - kMinZoomDifference);
                }

                GUILayout.Space(kZoomSliderSideMargin);

                float prevMinSlider = ZoomScaleToSlider(minZoomLevel);
                float prevMaxSlider = ZoomScaleToSlider(maxZoomLevel);
                float minSlider = prevMinSlider;
                float maxSlider = prevMaxSlider;
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.MinMaxSlider(ref minSlider, ref maxSlider, 0.0f, 1.0f, GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck())
                {
                    const float epsilon = 0.0001f;
                    bool minChanged = Mathf.Abs(minSlider - prevMinSlider) > epsilon;
                    bool maxChanged = Mathf.Abs(maxSlider - prevMaxSlider) > epsilon;

                    if (minChanged && !maxChanged)
                    {
                        minZoomLevel = ZoomSliderToScale(minSlider);
                        minZoomLevel = Mathf.Clamp(minZoomLevel, kMinZoomLevelLimit, maxZoomLevel - kMinZoomDifference);
                    }
                    else if (!minChanged && maxChanged)
                    {
                        maxZoomLevel = ZoomSliderToScale(maxSlider);
                        maxZoomLevel = Mathf.Clamp(maxZoomLevel, minZoomLevel + kMinZoomDifference, kMaxZoomLevelLimit);
                    }
                    else if (minChanged && maxChanged)
                    {
                        minZoomLevel = ZoomSliderToScale(minSlider);
                        maxZoomLevel = ZoomSliderToScale(maxSlider);
                        minZoomLevel = Mathf.Clamp(minZoomLevel, kMinZoomLevelLimit, maxZoomLevel - kMinZoomDifference);
                        maxZoomLevel = Mathf.Clamp(maxZoomLevel, minZoomLevel + kMinZoomDifference, kMaxZoomLevelLimit);
                    }
                }

                GUILayout.Space(kZoomSliderSideMargin);

                EditorGUI.BeginChangeCheck();
                int maxPercent = EditorGUILayout.IntField((int)ZoomScaleToPercent(maxZoomLevel), GUILayout.Width(40.0f));
                if (EditorGUI.EndChangeCheck())
                {
                    maxZoomLevel = ZoomPercentToScale((float)maxPercent);
                    maxZoomLevel = Mathf.Clamp(maxZoomLevel, minZoomLevel + kMinZoomDifference, kMaxZoomLevelLimit);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        internal static float ZoomScaleToSlider(float scale)
        {
            float t = Mathf.Clamp(scale, kMinZoomLevelLimit, kMaxZoomLevelLimit);
            return (Mathf.Log(t) - s_LogMinZoomLevelLimit) / (s_LogMaxZoomLevelLimit - s_LogMinZoomLevelLimit);
        }

        internal static float ZoomSliderToScale(float slider)
        {
            float t = Mathf.Clamp01(slider);
            return Mathf.Pow(kMinZoomLevelLimit, 1.0f - t) * Mathf.Pow(kMaxZoomLevelLimit, t);
        }

        internal static float ZoomScaleToPercent(float scale)
        {
            return scale * kZoomPercentFactor;
        }

        internal static float ZoomPercentToScale(float percent)
        {
            return percent / kZoomPercentFactor;
        }

        public static bool showRevertConfirmation
        {
            get { return EditorPrefs.GetBool(kShowRevertConfirmation, true); }
            set { EditorPrefs.SetBool(kShowRevertConfirmation, value); }
        }

        public static bool showApplyConfirmation
        {
            get { return EditorPrefs.GetBool(kShowApplyConfirmation, false); }
            set { EditorPrefs.SetBool(kShowApplyConfirmation, value); }
        }

        public static float minZoomLevel
        {
            get { return EditorPrefs.GetFloat(kMinZoomLevel, kDefaultMinZoomLevel); }
            set { EditorPrefs.SetFloat(kMinZoomLevel, value); }
        }

        public static float maxZoomLevel
        {
            get { return EditorPrefs.GetFloat(kMaxZoomLevel, kDefaultMaxZoomLevel); }
            set { EditorPrefs.SetFloat(kMaxZoomLevel, value); }
        }
    }
}
