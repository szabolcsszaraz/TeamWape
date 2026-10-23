using System.Text;

namespace Unity.InferenceEngine.Tokenization.Normalizers
{
    /// <summary>
    /// Applies standard Unicode normalization.
    /// </summary>
    public class UnicodeNormalizer : INormalizer
    {
        readonly NormalizationForm m_Form;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnicodeNormalizer"/> type.
        /// </summary>
        /// <param name="form">
        /// The standard unicode normalization form.
        /// </param>
        public UnicodeNormalizer(NormalizationForm form = NormalizationForm.FormC) => m_Form = form;

        /// <inheritdoc />
        public SubString Normalize(SubString input) => input.ToString().Normalize(m_Form);
    }
}
