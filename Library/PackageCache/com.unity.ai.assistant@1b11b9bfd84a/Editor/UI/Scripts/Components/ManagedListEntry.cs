using System;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    abstract class ManagedListEntry : ManagedTemplate
    {
        protected ManagedListEntry(string basePath = null)
            : base(basePath ?? AssistantUIConstants.UIModulePath)
        {
        }

        public virtual void SetData(int index, object data, bool isSelected = false)
        {
        }
    }
}
