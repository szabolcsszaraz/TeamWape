using Unity.AI.Assistant.Editor;
using UnityEditor;

namespace Unity.AI.Assistant.UI.Editor.Scripts
{
    internal static class AssistantUIConstants
    {
        internal const int CompactWindowThreshold = 600;
        internal const string CompactStyle = "mui-compact";
        internal const string IconStylePrefix = "mui-icon-";

        internal const char UnityPathSeparator = '/';
        internal const string TemplateExtension = ".uxml";
        internal const string StyleExtension = ".uss";

        internal const string ResourceFolderName = "Resources";

        internal const string PackageRoot = "";
        internal const string BasePath = "Packages/" + AssistantConstants.PackageName + PackageRoot + "/";
        internal const string UIEditorPath = "Editor/UI/";

        internal const string AssetFolder = "Assets/";
        internal const string ViewFolder = "Views/";
        internal const string StyleFolder = "Styles/";
        internal const string UIModulePath = BasePath + UIEditorPath;
        internal const string UIStylePath = UIModulePath + StyleFolder;

        internal const string AssistantBaseStyle = "Assistant.tss";
        internal const string AssistantSharedStyleDark = "AssistantSharedDark";
        internal const string AssistantSharedStyleLight = "AssistantSharedLight";

        internal const string ActiveActionButtonClass = "mui-action-button-active";
        internal const string RichTextLinkHoverClass = "rich-text-link";

        internal const int UIPreviewFullHDWidth = 1920;
        internal const int UIPreviewFullHDHeight = 1080;

        internal const string CodeLineColorTransparency = "40";
        internal const string CodeLineAddedColor = "#a8e6a8" + CodeLineColorTransparency;
        internal const string CodeLineRemovedColor = "#e6a8a8" + CodeLineColorTransparency;
        internal const string CodeLineDefaultColor = "#606060" + CodeLineColorTransparency;
        internal const string LinkColorDark = "#7BAEFA";
        internal const string LinkColorLight = "#0479D9";

        internal static string LinkColorForCurrentSkin => EditorGUIUtility.isProSkin ? LinkColorDark : LinkColorLight;

        internal const string FeedbackButtonDefaultTitle = "Send";
        internal const string FeedbackDownVotePlaceholder = "Tell us what went wrong";
        internal const string FeedbackUpVotePlaceholder = "Tell us what went well";
        internal const string FeedbackCommentSentMessage = "Thank you for sending your feedback.";
        internal const string FeedbackSendFailedMessage = "Feedback failed to send. Try again.";
        internal const string FeedbackPanelTitle = "Send a comment";
        internal const string FeedbackAddAnotherCommentTitle = "Add another comment";
        internal const string FeedbackPrivacyPolicyUrl = "https://unity.com/legal/privacy-policy";
        internal const string FeedbackSendingTitle = "Sending...";
        internal const string FeedbackConsentText = "I agree to be contacted by Unity via email to discuss my feedback and participate in follow-up research. For more information on how we handle your data, please see our <color=#7BAEFA><link=\"" + FeedbackPrivacyPolicyUrl + "\">Privacy Policy</link></color>.";
        internal const string FeedbackContactFoldoutTitle = "Can we contact you regarding your feedback? (optional)";

        internal const string WhatsNewUrl = "https://discussions.unity.com/lists/ai";

        internal const string ReportUnacceptableContentUrl = "https://unity-transparency.atlassian.net/servicedesk/customer/portal/1";
        internal const string ContentTransparencyPolicyUrl = "https://unity.com/legal/unity-services-content-transparency#reporting-unacceptable-content";
        internal const string ReportContentLinkIdReport = "report";
        internal const string ReportContentLinkIdLearn = "learn";

        internal static string ReportContentRichText
        {
            get
            {
                var color = LinkColorForCurrentSkin;
                return $"Found something inappropriate? <link={ReportContentLinkIdReport}><color={color}>Report it</color></link> or <link={ReportContentLinkIdLearn}><color={color}>learn more</color></link>.";
            }
        }

        internal const float UIAnalyticsDebounceInterval = 0.5f;
    }
}
