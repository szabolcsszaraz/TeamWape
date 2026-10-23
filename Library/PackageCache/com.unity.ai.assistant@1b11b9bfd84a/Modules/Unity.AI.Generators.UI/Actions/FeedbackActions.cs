using System;
using AiEditorToolsSdk.Components.Common.Enums;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Toolkit.Accounts.Services.Core;
using Unity.AI.Toolkit.Asset;
using UnityEngine;

namespace Unity.AI.Generators.UI.Actions
{
    /// <summary>
    /// Feedback sentiment for generation feedback.
    /// </summary>
    internal enum GenerationFeedbackSentiment
    {
        Positive,
        Negative
    }

    /// <summary>
    /// Identifies where the feedback was submitted from.
    /// </summary>
    internal enum FeedbackSource
    {
        /// <summary>
        /// Feedback submitted from a standalone generator window (e.g. Image, Mesh, Sound).
        /// </summary>
        Generators,

        /// <summary>
        /// Feedback submitted from the AI Assistant chat window.
        /// </summary>
        Assistant
    }

    /// <summary>
    /// Payload for submitting generation feedback.
    /// </summary>
    /// <param name="asset">The asset reference for the generation.</param>
    /// <param name="generationUri">The URI of the generated asset to provide feedback on.</param>
    /// <param name="sentiment">The sentiment of the feedback (positive/negative).</param>
    /// <param name="dialogType">The dialog type for the generation modality.</param>
    /// <param name="feedbackText">Optional text feedback from the user.</param>
    /// <param name="feedbackCategories">Categories the user selected, or null when none were selected.</param>
    /// <param name="downloadedAssetId">The ID of the downloaded asset for correlation.</param>
    /// <param name="feedbackSource">The source window where feedback was submitted from.</param>
    internal record SubmitGenerationFeedbackPayload(
        AssetReference asset,
        string generationUri,
        GenerationFeedbackSentiment sentiment,
        string dialogType = FeedbackActions.ImageGenerationDialogType,
        string feedbackText = null,
        string[] feedbackCategories = null,
        string downloadedAssetId = null,
        FeedbackSource? feedbackSource = null);

    /// <summary>
    /// Payload for tracking that feedback was submitted.
    /// </summary>
    /// <param name="asset">The asset reference for the generation.</param>
    /// <param name="generationUri">The URI of the generated asset that received feedback.</param>
    /// <param name="sentiment">The sentiment that was submitted.</param>
    internal record GenerationFeedbackSubmittedPayload(
        AssetReference asset,
        string generationUri,
        GenerationFeedbackSentiment sentiment);

    /// <summary>
    /// Actions for submitting feedback on generated assets.
    /// </summary>
    static class FeedbackActions
    {
        public static readonly string slice = "generationResults";

        /// <summary>
        /// Dialog type for image generation feedback.
        /// </summary>
        public const string ImageGenerationDialogType = "editor-generators-image";

        /// <summary>
        /// Dialog type for animation generation feedback.
        /// </summary>
        public const string AnimateGenerationDialogType = "editor-generators-animate";

        /// <summary>
        /// Dialog type for mesh generation feedback.
        /// </summary>
        public const string MeshGenerationDialogType = "editor-generators-mesh";

        /// <summary>
        /// Dialog type for PBR/material generation feedback.
        /// </summary>
        public const string PbrGenerationDialogType = "editor-generators-pbr";

        /// <summary>
        /// Dialog type for sound generation feedback.
        /// </summary>
        public const string SoundGenerationDialogType = "editor-generators-sound";

        /// <summary>
        /// Action to mark a generation as having received feedback.
        /// </summary>
        public static Creator<GenerationFeedbackSubmittedPayload> setGenerationFeedbackSubmitted => new($"{slice}/setGenerationFeedbackSubmitted");

        /// <summary>
        /// Async thunk to submit feedback for a generation.
        /// </summary>
        public static readonly AsyncThunkCreatorWithArg<SubmitGenerationFeedbackPayload> submitGenerationFeedback = new(
            $"{slice}/submitGenerationFeedback",
            async (payload, api) =>
            {
                try
                {
                    var sdkSentiment = payload.sentiment == GenerationFeedbackSentiment.Positive
                        ? FeedbackSentimentEnum.Positive
                        : FeedbackSentimentEnum.Negative;

                    // Only send categories the user actually selected (FUTR-2541).
                    var categories = payload.feedbackCategories;
                    var feedbackText = payload.feedbackText ?? string.Empty;

                    var dialogType = payload.dialogType ?? ImageGenerationDialogType;

                    var feedbackSource = payload.feedbackSource?.ToString();

                    var success = await AccountApi.SubmitFeedback(
                        payload.asset,
                        dialogType,
                        feedbackText,
                        categories,
                        sdkSentiment,
                        payload.downloadedAssetId,
                        feedbackSource);

                    if (success)
                    {
                        // Mark the generation as having received feedback
                        api.Dispatch(setGenerationFeedbackSubmitted, new GenerationFeedbackSubmittedPayload(
                            payload.asset,
                            payload.generationUri,
                            payload.sentiment));

                        // Persist feedback to the metadata JSON file so it survives editor restarts
                        var feedbackValue = payload.sentiment == GenerationFeedbackSentiment.Positive ? 1 : -1;
                        if (Uri.TryCreate(payload.generationUri, UriKind.Absolute, out var uri))
                            _ = GeneratedAssetMetadata.WriteFeedbackToMetadata(uri, feedbackValue);
                    }
                    else
                    {
                        const string message = "Failed to submit generation feedback.";
                        Debug.LogWarning(message);
                        api.Dispatch(GenerationActions.addGenerationFeedback,
                            new Payloads.GenerationsFeedbackData(payload.asset, new Payloads.GenerationFeedbackData(message)));
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            });
    }
}
