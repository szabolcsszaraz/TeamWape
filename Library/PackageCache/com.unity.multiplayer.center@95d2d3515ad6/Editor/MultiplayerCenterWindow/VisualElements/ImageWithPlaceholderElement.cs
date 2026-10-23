using UnityEditor;
using UnityEngine.UIElements;


namespace Unity.Multiplayer.Center.Editor
{
    [UxmlElement]
    partial class ImageWithPlaceholderElement : Image
    {
        public ImageWithPlaceholderElement()
        {
            AddToClassList(StyleClasses.Image);

            var placeholderStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.unity.multiplayer.center/Editor/MultiplayerCenterWindow/UI/PlaceHolder.uss");

            styleSheets.Add(placeholderStyle);

            Add(CreatePlaceholderElement());

            EnableInClassList(StyleClasses.NoImage, image == null);
        }

        private VisualElement CreatePlaceholderElement()
        {
            var placeholderContainer = new VisualElement { name = "placeholder" };
            placeholderContainer.AddToClassList(StyleClasses.PlaceHolder);

            var placeholderImage = new VisualElement { name = "placeholder-icon" };
            placeholderImage.AddToClassList(StyleClasses.PlaceHolderIcon);

            placeholderContainer.Add(placeholderImage);

            return placeholderContainer;
        }

        protected override void HandleEventTrickleDown(EventBase evt)
        {
            if (evt is IChangeEvent propChanged
                && evt.currentTarget is ImageWithPlaceholderElement target)
            {
                EnableInClassList(StyleClasses.NoImage, target.image == null);
            }

            base.HandleEventTrickleDown(evt);
        }
    }
}
