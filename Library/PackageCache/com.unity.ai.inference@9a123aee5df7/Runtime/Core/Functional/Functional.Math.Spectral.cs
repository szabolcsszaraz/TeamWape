using System;

namespace Unity.InferenceEngine
{
    public static partial class Functional
    {
        /// <summary>
        /// Computes the Short-Time Fourier Transform (STFT) of the `input` signal.
        /// </summary>
        /// <remarks>
        /// This operation computes the Short-Time Fourier Transform (STFT) of the `input` signal.
        /// The `input` must have rank `3` with shape `[batch, channels, signal_length]`. If a `window` is provided, it must have rank `1`.
        /// Promotes `input` and `window` to float type if necessary.
        /// When `onesided` is `true`, returns only positive frequencies.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var signal = Functional.Constant(new TensorShape(1, 8, 1), new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f }); // Shape: [1, 8, 1]
        /// var window = Functional.HannWindow(windowLength: 4, periodic: true);
        /// var result = Functional.STFT(signal, hop_length: 2, window: window, n_fft: 4, onesided: true);
        /// // Result: Frequency components over time, with shape [1, 3, 3, 2]:
        /// // [[[[0.6, 0.0], [-0.3, 0.1], [0.0, 0.0]], [[1.0, 0.0], [-0.5, 0.1], [0.0, 0.0]], [[1.4, 0.0], [-0.7, 0.1], [0.0, 0.0]]]]
        /// ]]></code>
        /// </example>
        /// <param name="input">The input signal tensor.</param>
        /// <param name="hop_length">The stride (in the signal) between two frames to be processed.</param>
        /// <param name="window">The optional window tensor that modulates a signal frame.</param>
        /// <param name="n_fft">The size of a single frame. If window is specified, it has to be the same as the window length.</param>
        /// <param name="onesided">Returns only half of the DFT frequency results (real signals have a symmetric DFT spectrum).</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor STFT(FunctionalTensor input, int hop_length, FunctionalTensor window, int n_fft, bool onesided)
        {
            DeclareRank(input, 3);
            if (window != null)
                DeclareRank(window, 1);
            input = input.Float();
            window = window?.Float();
            return FunctionalLayer.STFT(input, Constant(hop_length), window, Constant(n_fft), windowedDFTMatrix: null, onesided);
        }

        /// <summary>
        /// Returns a Blackman window of shape `[windowLength]`.
        /// </summary>
        /// <remarks>
        /// This operation generates a Blackman window used for spectral analysis and filtering.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var window = Functional.BlackmanWindow(windowLength: 8, periodic: true);
        /// // Result: Blackman window with shape [8]: [0.0, 0.066, 0.34, 0.774, 1.0, 0.774, 0.34, 0.066]
        /// ]]></code>
        /// </example>
        /// <param name="windowLength">The size of the window.</param>
        /// <param name="periodic">If `true`, returns a window to use as a periodic function. If `false`, return a symmetric window.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor BlackmanWindow(int windowLength, bool periodic)
        {
            return FunctionalLayer.BlackmanWindow(Constant(windowLength), periodic);
        }

        /// <summary>
        /// Returns a Hamming window of shape `[windowLength]` with `α = 0.54347826087` and `β = 0.45652173913`.
        /// </summary>
        /// <remarks>
        /// This operation generates a Hamming window used for spectral analysis and filtering.
        /// Computes the window as `α - β * cos(2π * n / (N-1))` for symmetric, or `α - β * cos(2π * n / N)` for periodic.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var window = Functional.HammingWindow(windowLength: 16, periodic: true);
        /// // Result: Hamming window with shape [16]: [0.087, 0.122, 0.221, 0.369, 0.543, 0.718, 0.866, 0.965, 1, 0.965, 0.866, 0.718, 0.543, 0.369, 0.221, 0.122]
        /// ]]></code>
        /// </example>
        /// <param name="windowLength">The size of the window.</param>
        /// <param name="periodic">If `true`, returns a window to use as a periodic function. If `false`, return a symmetric window.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor HammingWindow(int windowLength, bool periodic)
        {
            return FunctionalLayer.HammingWindow(Constant(windowLength), periodic);
        }

        /// <summary>
        /// Returns a Hann window of shape `[windowLength]`.
        /// </summary>
        /// <remarks>
        /// This operation generates a Hann window used for spectral analysis and filtering.
        /// Computes the window as `0.5 * (1 - cos(2π * n / (N-1)))` for symmetric, or `0.5 * (1 - cos(2π * n / N))` for periodic.
        /// </remarks>
        /// <example>
        /// <code lang="cs"><![CDATA[
        /// var window = Functional.HannWindow(windowLength: 16, periodic: false);
        /// // Result: Hann window with shape [16]: [0, 0.043, 0.165, 0.345, 0.552, 0.75, 0.905, 0.989, 0.989, 0.905, 0.75, 0.552, 0.345, 0.165, 0.043, 0]
        /// ]]></code>
        /// </example>
        /// <param name="windowLength">The size of the window.</param>
        /// <param name="periodic">If `true`, returns a window to use as a periodic function. If `false`, return a symmetric window.</param>
        /// <returns>The output tensor.</returns>
        public static FunctionalTensor HannWindow(int windowLength, bool periodic)
        {
            return FunctionalLayer.HannWindow(Constant(windowLength), periodic);
        }
    }
}
