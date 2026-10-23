using Unity.Properties;
using UnityEngine.UIElements;

namespace Unity.Multiplayer.Center.Editor
{
    /// <summary>
    /// Visual element that switches between question mode and answer mode.
    /// </summary>
    /// <remarks>
    /// Using a custom visual element to switch the styling allows
    /// showing the <see cref="QuestionMode"/> property in UIBuilder
    /// and makes editing the uxml easier.
    /// </remarks>
    [UxmlElement]
    partial class QuestionModeSwitcher : VisualElement
    {
        [UxmlAttribute, CreateProperty]
        public bool QuestionMode
        {
            get => ClassListContains(StyleClasses.QuestionMode);
            set => EnableInClassList(StyleClasses.QuestionMode,value);
        }
    }
}
