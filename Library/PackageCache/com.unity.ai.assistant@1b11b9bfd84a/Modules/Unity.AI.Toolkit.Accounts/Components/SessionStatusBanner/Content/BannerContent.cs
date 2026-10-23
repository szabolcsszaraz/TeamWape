using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Toolkit.Accounts.Components
{
    class BasicBannerContent : VisualElement
    {
        public VisualElement content = new();

        public BasicBannerContent(string message, LabelLink link) : this(message, new List<LabelLink> {link}) { }

        public BasicBannerContent(string message = "", IEnumerable<LabelLink> links = null)
            : this(message, links, false) { }

        public BasicBannerContent(string message, IEnumerable<LabelLink> links, bool useInfoIcon)
        {
            Init();

            content.AddToClassList("banner-content");
            content.Add(useInfoIcon ? CreateInfoIcon() : CreateWarningIcon());
            content.Add(CreateScrollableLabel(message, links));
        }

        public BasicBannerContent(string message, IEnumerable<LabelLink> links, string buttonText, Action buttonAction)
        {
            Init();

            content.AddToClassList("banner-content-button");
            content.Add(CreateScrollableLabel(message, links));

            if (buttonAction == null) return;

            var button = new Button { text = buttonText };
            button.clicked += buttonAction;

            content.Add(button);
        }

        public BasicBannerContent(string message, string buttonText, Action buttonAction, bool useInfoIcon = false)
        {
            InitWithIconAndMessage(message, useInfoIcon);

            var button = new Button { text = buttonText };
            button.clicked += buttonAction;
            button.AddToClassList("banner-right-action");
            Add(button);
        }

        public BasicBannerContent(string message, string buttonText, Action buttonAction, string linkText, Action linkAction)
        {
            InitWithIconAndMessage(message);

            var actionsRow = new VisualElement { name = "actions-row" };
            actionsRow.AddToClassList("banner-actions-row");

            var button = new Button { text = buttonText };
            button.clicked += buttonAction;
            button.AddToClassList("banner-right-action");
            actionsRow.Add(button);

            var link = new Label(linkText);
            link.AddToClassList("banner-dismiss-link");
            link.RegisterCallback<ClickEvent>(_ => linkAction?.Invoke());
            actionsRow.Add(link);

            Add(actionsRow);
        }

        public BasicBannerContent(string message, IEnumerable<LabelLink> links, string loadingMessage, TimeSpan? displayLoadingDuration = null)
        {
            Init();
            content.AddToClassList("banner-content");
            var warningIcon = CreateWarningIcon();
            var richLabel = CreateScrollableLabel(message, links);
            var dropdownLoading = new DropdownLoading(loadingMessage);

            RegisterCallback<AttachToPanelEvent>(async _ =>
            {
                content.Clear();
                content.Add(dropdownLoading);
                await EditorTask.Delay(displayLoadingDuration != null ? (int)displayLoadingDuration.Value.TotalMilliseconds : 30000);
                content.Clear();
                content.Add(warningIcon);
                content.Add(richLabel);
            });
        }

        /// <summary>
        /// Explains, on the action button, what the dashboard will answer. The access banners state a
        /// problem the reader usually cannot fix from the editor, so the button says where to go and
        /// this says what is waiting there, rather than leaving them to click and find out.
        /// </summary>
        protected void ExplainDashboardDestination()
        {
            const string tooltip =
                "Open the dashboard to see your organization's AI subscription, credit balance, and who can assign seats.";

            var button = this.Q<Button>(className: "banner-right-action");
            if (button != null)
                button.tooltip = tooltip;
        }

        protected void Init()
        {
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.ai.assistant/Modules/Unity.AI.Toolkit.Accounts/Components/SessionStatusBanner/SessionStatusBanner.uss"));
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.ai.assistant/Modules/Unity.AI.Toolkit.Accounts/Components/AIDropdownRoot/AIDropdownRoot.uss"));
            AddToClassList("banner");
            Add(content);
        }

        void InitWithIconAndMessage(string message, bool useInfoIcon = false)
        {
            Init();
            content.AddToClassList("banner-content");
            content.Add(useInfoIcon ? CreateInfoIcon() : CreateWarningIcon());
            content.Add(CreateScrollableLabel(message, null));
        }

        static ScrollView CreateScrollableLabel(string message, IEnumerable<LabelLink> links)
        {
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            scrollView.style.flexShrink = 1;
            scrollView.style.flexWrap = Wrap.Wrap;
            scrollView.Add(new RichLabel(message, links));
            return scrollView;
        }

        protected static Image CreateWarningIcon()
        {
            var warningIcon = new Image { image = EditorGUIUtility.IconContent("console.warnicon").image as Texture2D };
            warningIcon.AddToClassList("warning-icon");
            return warningIcon;
        }

        protected static Image CreateInfoIcon()
        {
            var infoIcon = new Image { image = EditorGUIUtility.IconContent("console.infoicon").image as Texture2D };
            infoIcon.AddToClassList("info-icon");
            return infoIcon;
        }
    }
}
