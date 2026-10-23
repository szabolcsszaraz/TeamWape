using System;
using Unity.AI.Assistant.Editor.Utils;
using UnityEditor;

namespace Unity.AI.Assistant.Editor
{
    static class AssistantConstants
    {
        internal const string PackageName = "com.unity.ai.assistant";

        // Surface attribution. The backend differentiates its frontends via the wire header
        // `conversation-kind` -> Session.conversation_kind. In-edit AIA must send `unity_editor`
        // explicitly rather than relying on the backend default, so traffic is reliably attributed
        // to client="unity_editor" in the orchestration / TTFT / error metrics. See muse-editor#2612
        // and the cross-ticket contract on muse#2211 (label name, value domain). Value must match
        // the backend ConversationKind literal in
        // muse ai_assistant/ai_assistant/datamodels/conversation_kind.py.
        internal const string ConversationKindHeader = "conversation-kind";
        internal const string ConversationKindEditor = "unity_editor";

        internal const int MaxConversationHistory = 1000;

        internal const string TextCutoffSuffix = "...";

        internal static readonly string SourceReferenceColor = EditorGUIUtility.isProSkin ? "4c7effff" : "055b9fff";
        internal static readonly string SourceReferencePrefix = "REF:";

        internal static readonly string InlineCodeTextColor = EditorGUIUtility.isProSkin ? "#E6E6E6" : "#141414";
        internal static readonly string ChatElementLineHeight = "18px";

        internal static readonly string UpdateButtonAccentColor = EditorGUIUtility.isProSkin ? "#2c5d87" : "#3a72b0";

        internal const string NewLineCRLF = "\r\n";
        internal const string NewLineLF = "\n";

        internal const string ProjectIdTagPrefix = "projId:";

        internal const string ContextTag = "#PROJECTCONTEXT#";
        internal static readonly string ContextTagEscaped = ContextTag.Replace("#", @"\#");

        internal const int AttachedContextDisplayLimit = 8;

        internal const long MaxImageFileSizeMB = 5;
        internal const long MaxImageFileSize = MaxImageFileSizeMB * BytesPerMegabyte;
        internal const long BytesPerMegabyte = 1024 * 1024;

        internal const long MaxTotalAttachmentSizeMB = 10;
        internal const long MaxTotalAttachmentSize = MaxTotalAttachmentSizeMB * BytesPerMegabyte;
        internal const long MaxGetFileContentSizeMB = MaxTotalAttachmentSizeMB;
        internal const long MaxGetFileContentSize = MaxGetFileContentSizeMB * BytesPerMegabyte;

        internal const int ChatPreAuthorizePoints = 25; // Defined backend side: Estimated points preauthorized per request. Set to cover P99 plus buffer

        internal static readonly string[] SupportedImageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tga", ".tif", ".tiff", ".psd", ".exr", ".hdr", ".iff", ".pct" };
        internal const int UserGuidelineCharacterLimit = 16384;

        internal static string GetDisclaimerHeader(string codeFormat = CodeFormat.CSharp)
        {
            const string disclaimerText = @"{0} AI-Tag
This was created with the help of Assistant, a Unity Artificial Intelligence product.";

            return CodeUtils.GetCommentedLines(string.Format(disclaimerText, DateTime.UtcNow.ToString("yyyy-MM-dd")), codeFormat);
        }

        internal const string DefaultCodeBlockCsharpFilename = "Code";
        internal const string DefaultCodeBlockCsharpExtension = "cs";
        internal const string DefaultCodeBlockShaderFilename = "NewShader";
        internal const string DefaultCodeBlockShaderExtension = "shader";
        internal const string DefaultCodeBlockTextFilename = "Output";
        internal const string DefaultCodeBlockTextExtension = "txt";

        internal static readonly string[] ShaderCodeBlockTypes = new string[] { "glsl", "hlsl", "shader" };

        internal const string CodeBlockCsharpFiletype = "cs";
        internal const string CodeBlockCsharpValidateFiletype = "csharp_validate";

        internal const string UxmlExtension = ".uxml";
        internal const string UssExtension = ".uss";

        internal const string DefaultConversationTitle = "New conversation";

        internal const string ProfilerDataValueType = "Profiler Data";
    }
}
