using System;
using System.Collections.Generic;
using Unity.AI.Toolkit.Accounts.Services;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Toolkit.Accounts.Components
{
    [UxmlElement]
    partial class LegalAgreement : VisualElement
    {
        internal record AIData
        {
            public string text;
            public List<LabelUrlLink> links;
            public List<string> packages;
            public string installButtonText;
            public string noInternet;
            public string installingPackages;
            public Action installButtonAction;
        }

        internal static readonly AIData data = new()
        {
            text = "I have read and agree to the <link=terms><color=#7BAEFA>Unity Terms of Service</color></link> and the <link=thirdparty><color=#7BAEFA>Third-Party AI Terms</color></link>."
                + "\n\nI understand that:"
                + "\n\n\u2022 My use of Unity AI Assistant is governed by the <link=terms><color=#7BAEFA>Unity Terms of Service</color></link>, including its confidentiality provisions applicable to non-public beta features, and grants me a limited, personal, and revocable license."
                + "\n\n\u2022 Unity AI Assistant is currently an \"Evaluation Version\" under the Terms of Service but may be used for commercial purposes."
                + "\n\n\u2022 Use of Unity AI Assistant is managed via credits, and Unity may change the product, pricing, terms, and entitlements at any time. See <link=pricing><color=#7BAEFA>Unity AI Pricing & Credits</color></link> for current details."
                + "\n\n\u2022 My usage data will be collected and analyzed to evaluate and improve the service, as further described in Unity\u2019s <link=supplemental><color=#7BAEFA>Generative AI Supplemental Privacy Notice</color></link>. Deleting a conversation will remove it from my visible history and future context, but certain backend logs may be retained under Unity's data retention practices."
                + "\n\n\u2022 I am responsible for ensuring that my use of Unity AI, including third-party models, and any assets I generate do not infringe on third-party rights and are appropriate for my use. See <link=guiding><color=#7BAEFA>Unity AI Guiding Principles</color></link> for more information."
                + "\n\nBy continuing, I acknowledge and agree to these terms.",
            links = new()
            {
                new() {id = "terms", url = "https://unity.com/legal/terms-of-service"},
                new() {id = "thirdparty", url = "https://unity.com/legal/third-party-ai-terms"},
                new() {id = "supplemental", url = "https://unity.com/legal/supplemental-privacy-statement-unity-muse"},
                new() {id = "pricing", url = "https://unity.com/products"},
                new() {id = "guiding", url = "https://unity.com/legal/unityai-guiding-principles"}
            },
            noInternet = "You need an internet connection to be able to use the AI features.",
            installingPackages = "Installing packages",

            packages = new()
            {
                "com.unity.ai.assistant"
            },

            installButtonText = "Agree and continue to Unity AI Beta",
            installButtonAction = () => _ = AccountController.SetTermsOfService()
        };

        public LegalAgreement()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.unity.ai.assistant/Modules/Unity.AI.Toolkit.Accounts/Components/LegalAgreement/LegalAgreement.uxml");
            tree.CloneTree(this);

            var text = this.Q<RichLabel>("legal-text");
            text.links = data.links;
            text.text = data.text;

            var button = this.Q<Button>("agree-button");
            button.text = data.installButtonText;
            button.clicked += data.installButtonAction;
        }
    }
}
