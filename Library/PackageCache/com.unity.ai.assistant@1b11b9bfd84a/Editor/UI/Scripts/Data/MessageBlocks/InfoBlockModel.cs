using System;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Data.MessageBlocks
{
    /// <summary>
    /// UI model for non-error informational notices (e.g. server graceful disconnect for
    /// maintenance). Mirrors <see cref="ErrorBlockModel"/> but is rendered with informational
    /// styling rather than as a failure.
    /// </summary>
    class InfoBlockModel : IMessageBlockModel, IEquatable<InfoBlockModel>
    {
        public string Message;

        public bool Equals(InfoBlockModel other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Message == other.Message;
        }

        public override bool Equals(object obj) => obj is InfoBlockModel other && Equals(other);

        public override int GetHashCode() => Message?.GetHashCode() ?? 0;
    }
}
