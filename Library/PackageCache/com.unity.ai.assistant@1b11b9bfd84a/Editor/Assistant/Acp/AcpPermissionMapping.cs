using System.Linq;
using Unity.AI.Assistant.FunctionCalling;

namespace Unity.AI.Assistant.Editor.Acp
{
    /// <summary>
    /// Provides mapping between ACP permission options and PermissionUserAnswer.
    /// </summary>
    static class AcpPermissionMapping
    {
        /// <summary>
        /// ACP option kind for "allow once".
        /// </summary>
        public const string AllowOnceKind = "allow_once";

        /// <summary>
        /// ACP option kind for "allow always" (for this session/conversation).
        /// </summary>
        public const string AllowAlwaysKind = "allow_always";

        /// <summary>
        /// ACP option kind for "reject once".
        /// </summary>
        public const string RejectOnceKind = "reject_once";

        /// <summary>
        /// ACP option kind for "reject always" (for this session/conversation).
        /// </summary>
        public const string RejectAlwaysKind = "reject_always";

        /// <summary>
        /// Converts an ACP permission option kind to the corresponding PermissionUserAnswer.
        /// </summary>
        public static PermissionUserAnswer ToUserAnswer(string acpKind)
        {
            return acpKind switch
            {
                AllowOnceKind => PermissionUserAnswer.AllowOnce,
                AllowAlwaysKind => PermissionUserAnswer.AllowAlways,
                RejectOnceKind => PermissionUserAnswer.DenyOnce,
                RejectAlwaysKind => PermissionUserAnswer.DenyAlways,
                _ => PermissionUserAnswer.DenyOnce
            };
        }

        /// <summary>
        /// Converts a PermissionUserAnswer to the corresponding ACP option kind.
        /// </summary>
        public static string ToAcpKind(PermissionUserAnswer answer)
        {
            return answer switch
            {
                PermissionUserAnswer.AllowOnce => AllowOnceKind,
                PermissionUserAnswer.AllowAlways => AllowAlwaysKind,
                PermissionUserAnswer.DenyOnce => RejectOnceKind,
                PermissionUserAnswer.DenyAlways => RejectAlwaysKind,
                _ => RejectOnceKind
            };
        }

        /// <summary>
        /// Finds the ACP option ID that matches the given UserAnswer.
        /// Falls back from session-scoped answers to single-use equivalents
        /// when the tool doesn't support the broader scope.
        /// </summary>
        public static string FindOptionId(AcpPermissionOption[] options, PermissionUserAnswer answer)
        {
            if (options == null)
            {
                return null;
            }

            var kind = ToAcpKind(answer);
            var optionId = options.FirstOrDefault(o => o != null && o.Kind == kind)?.OptionId;

            if (optionId == null)
            {
                var fallbackKind = GetFallbackKind(kind);
                if (fallbackKind != null)
                {
                    optionId = options.FirstOrDefault(o => o != null && o.Kind == fallbackKind)?.OptionId;
                }
            }

            return optionId;
        }

        static string GetFallbackKind(string kind)
        {
            return kind switch
            {
                AllowAlwaysKind => AllowOnceKind,
                RejectAlwaysKind => RejectOnceKind,
                _ => null
            };
        }
    }
}
