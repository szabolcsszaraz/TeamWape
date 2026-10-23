using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Tools.Editor;
using Unity.AI.Assistant.UI.Editor.Scripts;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    abstract class ImagePreviewFunctionCallElementBase : VisualElement, IFunctionCallRenderer, IAssistantUIContextAware
    {
        public abstract string Title { get; }
        public string TitleDetails { get; private set; }
        public bool Expanded => true;
        public AssistantUIContext Context { get; set; }

        protected abstract string LoadingMessage { get; }
        protected abstract string ErrorPrefix { get; }
        protected abstract string LoadFailedMessage { get; }

        VisualElement m_ImageContainer;
        Texture2D m_PreviewTexture;
        byte[] m_CachedImageData;

        protected ImagePreviewFunctionCallElementBase()
        {
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        public void OnCallRequest(AssistantFunctionCall functionCall)
        {
            TitleDetails = string.Empty;

            DestroyPreviewTexture();
            m_CachedImageData = null;

            m_ImageContainer = new VisualElement();
            Add(m_ImageContainer);
        }

        public void OnCallSuccess(string functionId, Guid callId, FunctionCallResult result)
        {
            JObject jsonResult;
            try
            {
                jsonResult = result.Result is JObject jObj ? jObj : JObject.Parse(result.Result?.ToString() ?? "{}");
            }
            catch
            {
                m_ImageContainer.Add(new Label("Invalid response format."));
                return;
            }

            var imageUrl = jsonResult["sas_url"]?.ToString() ?? jsonResult["url"]?.ToString();

            if (string.IsNullOrEmpty(imageUrl))
            {
                m_ImageContainer.Add(new Label("No image URL available."));
                return;
            }

            var width = jsonResult["width"]?.ToObject<int>() ?? 0;
            var height = jsonResult["height"]?.ToObject<int>() ?? 0;
            DisplayImage(imageUrl, width, height);
        }

        public void OnCallError(string functionId, Guid callId, string error)
        {
            m_ImageContainer.Add(new Label(ErrorPrefix + error));
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            DestroyPreviewTexture();
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            if (m_PreviewTexture == null && m_CachedImageData != null)
                RestorePreviewTexture();
        }

        void RestorePreviewTexture()
        {
            var texture = new Texture2D(2, 2);
            if (!texture.LoadImage(m_CachedImageData))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                return;
            }

            m_PreviewTexture = texture;

            var image = m_ImageContainer?.Q<Image>();
            if (image != null)
                image.image = m_PreviewTexture;
        }

        void DestroyPreviewTexture()
        {
            if (m_PreviewTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(m_PreviewTexture);
                m_PreviewTexture = null;
            }
        }

        async void DisplayImage(string imageUrl, int width, int height)
        {
            var loadingLabel = new Label(LoadingMessage);
            loadingLabel.AddToClassList("screenshot-preview-loading");
            m_ImageContainer.Add(loadingLabel);

            var imageData = await DownloadImageDataAsync(imageUrl);

            loadingLabel.RemoveFromHierarchy();
            ShowDownloadedImage(imageData);

            // The download lands passes after the card it fills was measured, and the card's own scroller
            // absorbs the growth, so a container that positions the card only learns of it by being told.
            if (panel != null)
                this.ReportHeightChange();
        }

        void ShowDownloadedImage(byte[] imageData)
        {
            if (imageData == null)
            {
                m_ImageContainer.Add(new Label(LoadFailedMessage));
                return;
            }

            m_CachedImageData = imageData;

            var texture = new Texture2D(2, 2);
            if (!texture.LoadImage(imageData))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                m_CachedImageData = null;
                m_ImageContainer.Add(new Label(LoadFailedMessage));
                return;
            }

            DestroyPreviewTexture();
            m_PreviewTexture = texture;

            var image = new Image
            {
                image = m_PreviewTexture,
                scaleMode = ScaleMode.ScaleToFit
            };
            image.AddToClassList("screenshot-preview-image");
            m_ImageContainer.Add(image);
        }

        static async Task<byte[]> DownloadImageDataAsync(string url)
        {
            using var request = UnityWebRequest.Get(url);
            var operation = request.SendWebRequest();

            while (!operation.isDone)
                await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
                return null;

            return request.downloadHandler.data;
        }
    }
}
