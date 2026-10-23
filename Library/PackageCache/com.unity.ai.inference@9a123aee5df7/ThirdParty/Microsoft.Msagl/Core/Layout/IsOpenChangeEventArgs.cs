using System;

namespace Microsoft.Msagl.Core.Layout {
    /// <summary>
    ///
    /// </summary>
    internal class IsOpenChangeEventArgs : EventArgs {
        /// <summary>
        ///
        /// </summary>
        public readonly bool PreviousValue;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="previousValue"></param>
        public IsOpenChangeEventArgs(bool previousValue) {
            PreviousValue = previousValue;
        }
    }
}