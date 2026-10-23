using System;
using System.Collections.Generic;
using System.Linq;
using AiEditorToolsSdk.Components.Common.Enums;
using UnityEngine;

namespace Unity.AI.ModelSelector.Services.Stores.States
{
    [Serializable]
    record ImageDimensions
    {
        public int width;
        public int height;
    }

    [Serializable]
    record ModelSettings
    {
        public string id;
        public string name;
        public List<string> tags = new();
        public string description;
        public List<string> thumbnails = new();
        public string icon;
        public string provider = ModelConstants.Providers.None;
        public string modality = ModelConstants.Modalities.None;
        public string status;
        public string deprecationWarning;
        public string replacementModelId;
        public string minSdkVersion;
        public List<string> operations = new();
        public ImageDimensions nativeResolution = new() { width = 1024, height = 1024 };
        public List<ImageDimensions> imageSizes = new[]{ new ImageDimensions { width = 1024, height = 1024 } }.ToList();
        public List<string> aspectRatios = new();
        public string sizingMode = "dimensions";
        public string baseModelId;
        public bool isFavorite;
        public bool favoriteProcessing;
        public bool isCustom;
        public List<string> constants = new();
        public List<string> limitations = new();
        public int maxReferenceImages;
        public string referenceImagesParamKey;
        public ModelParamsSchema paramsSchema;
        public ModelUiSchema uiSchema;
        public List<string> consumers = new();
        public string category;
        public List<string> capabilities = new();

        public bool SupportsParam(string key)
        {
            if (paramsSchema?.Properties == null)
                return false;
            if (paramsSchema.Properties.ContainsKey(key))
                return true;
            var variant = ModelConstants.SchemaKeys.GetVariant(key);
            return variant != null && paramsSchema.Properties.ContainsKey(variant);
        }
    }
}
