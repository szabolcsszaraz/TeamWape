using Unity.AI.Assistant.Tools.Editor.UI;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class AssistantUISettingsPage : ManagedTemplate
    {
        static readonly string k_FigmaAccessTokenAuthFailureWarning = "Token verification failed and the token is not saved. Please make sure the token is not expired and has read access, and try again.";
        static readonly string k_FigmaAccessTokenAuthSuccessMessage = "Token verified and saved successfully.";
        static readonly string k_FigmaAccessTokenDeleteSuccessMessage = "Token deleted successfully.";
        
        VisualElement m_FigmaAccessTokenAuthFailureWarning;
        VisualElement m_FigmaAccessTokenOperationSuccess;
        TextField m_FigmaAccessTokenField;
        Button m_NewButton;
        Label m_FigmaAccessTokenSuccessText;

        public AssistantUISettingsPage() : base(AssistantUIConstants.UIModulePath) { }

        protected override void InitializeView(TemplateContainer view)
        {
            LoadStyle(view, "AssistantUISettingsPage");
            LoadStyle(view, EditorGUIUtility.isProSkin
                ? AssistantUIConstants.AssistantSharedStyleDark
                : AssistantUIConstants.AssistantSharedStyleLight);
            LoadStyle(view, AssistantUIConstants.AssistantBaseStyle, true);

            InitializeCustomInstructionsUI(view);
        }

        void InitializeCustomInstructionsUI(TemplateContainer view)
        {
            m_FigmaAccessTokenAuthFailureWarning = view.Q<VisualElement>("figmaAccessTokenValidationFailureWarningContainer");
            m_FigmaAccessTokenAuthFailureWarning.SetDisplay(false);
            
            var figmaAccessTokenWarningText = view.Q<Label>("figmaAccessTokenValidationFailureWarningText");
            figmaAccessTokenWarningText.text = k_FigmaAccessTokenAuthFailureWarning;

            m_FigmaAccessTokenOperationSuccess = view.Q<VisualElement>("figmaAccessTokenValidationSuccessContainer");
            m_FigmaAccessTokenOperationSuccess.SetDisplay(false);

            m_FigmaAccessTokenSuccessText = view.Q<Label>("figmaAccessTokenValidationSuccessText");

            m_FigmaAccessTokenField = view.Q<TextField>("figmaAccessTokenField");
            CheckExistingToken();

            view.SetupButton("figmaAccessTokenSaveButton", OnNewFigmaAccessToken);
        }

        async void CheckExistingToken()
        {
            await FigmaToUI.RefreshTokenState();
            if (FigmaToUI.HasToken)
                m_FigmaAccessTokenField.SetValueWithoutNotify("********");
        }
        
        async void OnNewFigmaAccessToken(PointerUpEvent evt)
        {
            try
            {
                var token = m_FigmaAccessTokenField?.value;
                if (string.IsNullOrWhiteSpace(token))
                {
                    await FigmaToUI.RemoveToken();
                    
                    m_FigmaAccessTokenAuthFailureWarning.SetDisplay(false);
                    m_FigmaAccessTokenSuccessText.text = k_FigmaAccessTokenDeleteSuccessMessage;
                    m_FigmaAccessTokenOperationSuccess.SetDisplay(true);
                    return;
                }

                var isValid = await FigmaToUI.VerifyFigmaAuthToken(token);
                if (isValid)
                    m_FigmaAccessTokenSuccessText.text = k_FigmaAccessTokenAuthSuccessMessage;
                m_FigmaAccessTokenAuthFailureWarning.SetDisplay(!isValid);
                m_FigmaAccessTokenOperationSuccess.SetDisplay(isValid);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                m_FigmaAccessTokenAuthFailureWarning.SetDisplay(true);
                m_FigmaAccessTokenOperationSuccess.SetDisplay(false);
            }
        }
    }
}
