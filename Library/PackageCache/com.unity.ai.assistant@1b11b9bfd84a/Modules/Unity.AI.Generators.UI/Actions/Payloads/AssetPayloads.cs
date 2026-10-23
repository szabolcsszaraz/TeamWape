using System;
using System.Collections.Generic;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Toolkit.Asset;

namespace Unity.AI.Generators.UI.Payloads
{
    [Serializable] record AsssetContext(AssetReference asset);
    [Serializable] record GenerationArgs(AssetReference asset, bool autoApply, bool waitForCompletion = true) : AsssetContext(asset);
    [Serializable] record QuotePricingDetails(long worstCaseCost = 0, bool flatPricing = true, string providerName = "");
    [Serializable] record GenerationValidationResult(bool success, string error, long cost, List<GenerationFeedbackData> feedback, QuotePricingDetails pricingDetails = null)
    {
        public long effectiveCost => pricingDetails is { worstCaseCost: > 0 } ? pricingDetails.worstCaseCost : cost;
    }
    [Serializable] record GenerationsValidationResult(AssetReference asset, GenerationValidationResult result) : AsssetContext(asset);
    [Serializable] record GenerationFeedbackData(string message);
    [Serializable] record GenerationsFeedbackData(AssetReference asset, GenerationFeedbackData feedback) : AsssetContext(asset);

    [Serializable] record GenerationProgressData(int taskID, int count, float progress);
    [Serializable] record GenerationsProgressData(AssetReference asset, GenerationProgressData progress) : AsssetContext(asset);
    [Serializable] record GenerationAllowedData(AssetReference asset, bool allowed) : AsssetContext(asset);
    [Serializable] record GeneratedResultVisibleData(AssetReference asset, string elementID, int count) : AsssetContext(asset);
    [Serializable] record FulfilledSkeletons(AssetReference asset, List<FulfilledSkeleton> skeletons) : AsssetContext(asset);
    [Serializable] record ImageReferenceClearAllData;
}
