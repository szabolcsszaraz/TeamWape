namespace UnityEditor.U2D.Sprites
{
    class SpriteFrameCapabilityUserSettings
    {
        const string kCapabilityKey = "SpriteFrameCapabilityUserSettings.Capability";

        [MenuItem("internal:2D Debug Menu/Clear Sprite Frame Capability User Settings", priority = 1000)]
        static void ClearUserSettings()
        {
            EditorPrefs.DeleteKey(kCapabilityKey);
        }

        public static EditCapability capability
        {
            get
            {
                var raw = EditorPrefs.GetInt(kCapabilityKey, (int)EEditCapability.All);
                var cap = new EditCapability();
                cap.rawCapability = (EEditCapability)raw;
                return cap;
            }
            set
            {
                var raw = value.rawCapability;
                EditorPrefs.SetInt(kCapabilityKey, (int)raw);
            }
        }
    }
}
