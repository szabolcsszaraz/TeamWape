using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Unity.InferenceEngine.Tokenization.PostProcessors.Templating;

namespace Unity.InferenceEngine.Tokenization.PostProcessors
{
    using static Utility;

    /// <summary>
    /// Post processor using the templating approach.
    /// </summary>
    public class TemplatePostProcessor : IPostProcessor
    {
        static void CheckTemplate(
            Template template,
            string nameofTemplate,
            IReadOnlyDictionary<string, int> specialTokens,
            bool isSingle)
        {
            var sequenceAFound = false;
            var sequenceBFound = false;

            foreach (var piece in template.Pieces)
                switch (piece)
                {
                    case SpecialToken specialToken
                        when !specialTokens.ContainsKey(specialToken.Value):
                        throw new KeyNotFoundException(
                            $"Token {specialToken.Value} in template {nameofTemplate} not found in the special tokens list.");
                    case Sequence sequence:
                    {
                        sequenceAFound |= sequence.Identifier is SequenceIdentifier.A;
                        sequenceBFound |= sequence.Identifier is SequenceIdentifier.B;
                        break;
                    }
                }

            if (!sequenceAFound)
                throw new FormatException(
                    "Sequence B cannot be used in a single sequence template.");

            switch (isSingle)
            {
                case true when sequenceBFound:
                    throw new FormatException(
                        "Sequence B cannot be used in a single sequence template.");
                case false when !sequenceBFound:
                    throw new FormatException("Sequence B must appears in the template.");
            }
        }

        Pool<List<Token>> m_TokenPool;
        Pool<List<List<Token>>> m_SequencePool;

        readonly Template m_PairSequenceTemplate;
        readonly Template m_SingleSequenceTemplate;
        readonly IReadOnlyDictionary<string, int> m_SpecialTokens;

        /// <summary>
        /// Initializes a new instance of the <see cref="TemplatePostProcessor"/> type.
        /// </summary>
        /// <param name="single">
        /// <see cref="Template" /> for processing of single sequence.
        /// </param>
        /// <param name="pair">
        /// <see cref="Template"/> for processing of paired sequences.
        /// </param>
        /// <param name="specialTokens">
        /// Special tokens used in the templates.
        /// </param>
        public TemplatePostProcessor(
            [NotNull] Template single,
            [CanBeNull] Template pair,
            IEnumerable<(string value, int id)> specialTokens)
        {
            if (single == null)
                throw new ArgumentNullException(nameof(single));

            m_SpecialTokens = specialTokens.ToDictionary(x => x.value, x => x.id);

            CheckTemplate(single, nameof(single), m_SpecialTokens, true);
            m_SingleSequenceTemplate = single;

            if (pair is not null)
            {
                CheckTemplate(pair, nameof(pair), m_SpecialTokens, false);
                m_PairSequenceTemplate = pair;
            }

            (m_TokenPool, m_SequencePool) = InitSequencePool();
        }

        /// <inheritdoc />
        public int GetNumAddedTokens(bool isPair) =>
            (isPair ? m_PairSequenceTemplate : m_SingleSequenceTemplate).Pieces.Count(p =>
                p is SpecialToken);

        /// <inheritdoc />
        public void PostProcess(
            IReadOnlyList<IReadOnlyList<Token>> sequenceA,
            IReadOnlyList<IReadOnlyList<Token>> sequenceB,
            bool addSpecialTokens,
            Output<IEnumerable<IEnumerable<Token>>> output)
        {
            if (sequenceA == null)
                throw new ArgumentNullException(nameof(sequenceA));

            var template = sequenceB is not null
                ? m_PairSequenceTemplate
                : m_SingleSequenceTemplate;

            foreach (var piece in template.Pieces)
                switch (piece)
                {
                    case Sequence sequencePiece:
                    {
                        var sequence = sequencePiece.Identifier == SequenceIdentifier.A
                            ? sequenceA
                            : sequenceB;
                        if (sequence is not null)
                            AddSequence(sequence, sequencePiece.SequenceId, output);
                        break;
                    }
                    case SpecialToken specialTokenPiece:
                    {
                        if (addSpecialTokens)
                            AddSpecialToken(
                                new(m_SpecialTokens[specialTokenPiece.Value], specialTokenPiece.Value),
                                specialTokenPiece.SequenceId, output);
                        break;
                    }
                }

            return;

            void AddSequence(
                IReadOnlyList<IReadOnlyList<Token>> pSequence,
                int pTypeId,
                Output<IEnumerable<IEnumerable<Token>>> pOutput)
            {
                using var _ = m_SequencePool.Get(out var newSequence);
                for (var sI = 0; sI < pSequence.Count; sI++)
                {
                    var seqTokens = pSequence[sI];
                    var tokens = m_TokenPool.Get();
                    for (var tI = 0; tI < seqTokens.Count; tI++)
                    {
                        var token = seqTokens[tI].SetAttention(true);

                        // for some reason, tokenizers only applies type_id to the main sequence.
                        if (sI == 0)
                            token = token.SetTypeId(pTypeId);
                        tokens.Add(token);
                    }

                    newSequence.Add(tokens);
                }

                pOutput.Add(newSequence);
            }

            void AddSpecialToken(
                Token pToken,
                int pTypeId,
                Output<IEnumerable<IEnumerable<Token>>> pOutput)
            {
                using var _ = m_SequencePool.Get(out var newSequence);
                var tokens = m_TokenPool.Get();
                tokens.Add(pToken.SetAttention(true).SetTypeId(pTypeId).SetSpecial(true));
                newSequence.Add(tokens);
                pOutput.Add(newSequence);
            }
        }
    }
}
