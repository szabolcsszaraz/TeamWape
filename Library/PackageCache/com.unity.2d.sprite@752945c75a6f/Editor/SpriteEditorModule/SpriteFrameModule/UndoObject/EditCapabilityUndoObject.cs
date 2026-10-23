using System;
using UnityEditor.U2D.Sprites.ProjectSettings;
using UnityEngine;

namespace UnityEditor.U2D.Sprites
{
    [Serializable]
    class EditCapabilityUndoObject : UndoObject<EditCapability>
    {
        [SerializeField]
        EditCapability m_OriginalData;

        public EditCapability originalData
        {
            get => m_OriginalData;
            set => m_OriginalData = value;
        }

        protected override void InitInherit()
        {
            originalData = data;
            var loadedCapability = data;
            var savedCapability = SpriteFrameCapabilityUserSettings.capability;
            foreach (EEditCapability flag in System.Enum.GetValues(typeof(EEditCapability)))
            {
                if (flag == EEditCapability.None || flag == EEditCapability.All)
                    continue;
                if (data.HasCapability(flag))
                {
                    loadedCapability.SetCapability(flag , savedCapability.HasCapability(flag));
                }
            }
            SetData(loadedCapability);
        }

        public bool HasCapability(EEditCapability hasCapability)
        {
            var allowOverride = SpriteFrameModuleSettingsAsset.allowSpriteFrameEditCapabilityOverride;
            var enabled = data.HasCapability(hasCapability);
            var originalCapability = originalData.HasCapability(hasCapability);
            return (originalCapability && enabled) || (allowOverride && enabled);
        }

        public override void Dispose()
        {
            base.Dispose();
            var savedCapability = SpriteFrameCapabilityUserSettings.capability;
            foreach (EEditCapability flag in System.Enum.GetValues(typeof(EEditCapability)))
            {
                if (flag == EEditCapability.None || flag == EEditCapability.All)
                    continue;
                if (m_OriginalData.HasCapability(flag))
                {
                    savedCapability.SetCapability(flag , data.HasCapability(flag));
                }
            }

            SpriteFrameCapabilityUserSettings.capability = savedCapability;
        }
    }
}
