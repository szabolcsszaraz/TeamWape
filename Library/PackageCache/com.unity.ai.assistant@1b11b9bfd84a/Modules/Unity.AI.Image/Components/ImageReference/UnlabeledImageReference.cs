using System;
using Unity.AI.Image.Services.Contexts;
using Unity.AI.Image.Services.Stores.Actions;
using Unity.AI.Image.Services.Stores.Actions.Payloads;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Image.Services.Stores.States;
using Unity.AI.Image.Services.Utilities;
using Unity.AI.Image.Utilities;
using Unity.AI.Image.Windows;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Contexts;
using Unity.AI.Generators.IO.Utilities;
using Unity.AI.Generators.UI;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.Toolkit.Asset;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Components
{
    [UxmlElement]
    partial class UnlabeledImageReference : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.assistant/Modules/Unity.AI.Image/Components/ImageReference/UnlabeledImageReference.uxml";

        int m_Index;

        public int Index
        {
            get => m_Index;
            set => m_Index = value;
        }

        public UnlabeledImageReference()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            AddToClassList("unlabeled-image-reference");

            var objectField = this.Q<ObjectField>();
            var imageReferenceObjectField = this.Q<VisualElement>("image-reference-object-field");
            var doodleBackground = imageReferenceObjectField.Q<VisualElement>("doodle-pad-background-image");
            var doodleCanvas = imageReferenceObjectField.Q<UnityEngine.UIElements.Image>("doodle-pad-canvas");
            var settingsButton = this.Q<Button>("image-reference-settings-button");
            var doodlePad = this.Q<DoodlePad>("image-reference-object-field__doodle-pad");
            var editButton = this.Q<Button>("edit-image-reference");
            var strengthSlider = this.Q<SliderInt>("image-reference-strength-slider");
            var removeButton = this.Q<Button>("unlabeled-remove-button");

            // Make the doodle read-only
            doodleBackground.pickingMode = PickingMode.Ignore;
            doodleCanvas.pickingMode = PickingMode.Ignore;
            doodlePad.backGroundImageOpacity = 0;
            editButton.SetShown(true);

            // UI events
            objectField.AddManipulator(new ScaleToFitObjectFieldImage());
            objectField.RegisterValueChangedCallback(async evt =>
            {
                var wasTempAsset = ExternalFileDragDrop.tempAssetDragged;
                var assetRef = Unity.AI.Generators.Asset.AssetReferenceExtensions.FromObject(evt.newValue);
                if (wasTempAsset)
                {
                    ExternalFileDragDrop.EndDragFromExternalPath();

                    byte[] data;
                    {
                        await using var stream = await assetRef.GetCompatibleImageStreamAsync();
                        data = await stream.ReadFullyAsync();
                    }
                    objectField.SetValueWithoutNotify(null);
                    this.Dispatch(GenerationSettingsActions.setUnlabeledImageReferenceAsset, (m_Index, (AssetReference)null));
                    this.Dispatch(GenerationSettingsActions.setUnlabeledImageReferenceMode, (m_Index, ImageReferenceMode.Doodle));
                    this.Dispatch(GenerationSettingsActions.setUnlabeledImageReferenceDoodle, (m_Index, data));

                    ExternalFileDragDrop.CleanupDragFromExternalPath();
                }
                else
                {
                    this.Dispatch(GenerationSettingsActions.setUnlabeledImageReferenceAsset, (m_Index, assetRef));
                    this.Dispatch(GenerationSettingsActions.setUnlabeledImageReferenceMode, (m_Index, ImageReferenceMode.Asset));
                    this.Dispatch(GenerationSettingsActions.setUnlabeledImageReferenceDoodle, (m_Index, (byte[])null));
                }
            });

            objectField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if ((evt.keyCode == KeyCode.Backspace && evt.actionKey) || evt.keyCode == KeyCode.Delete)
                    Clear();
            });

            strengthSlider?.RegisterValueChangedCallback(evt =>
            {
                this.Dispatch(GenerationSettingsActions.setUnlabeledImageReferenceStrength, (m_Index, evt.newValue / 100.0f));
            });

            removeButton.clicked += () =>
            {
                this.Dispatch(GenerationSettingsActions.removeUnlabeledImageReference, m_Index);
            };

            editButton.clicked += Edit;
            settingsButton.clicked += () => ShowMenu();
            imageReferenceObjectField.RegisterCallback<ContextClickEvent>(_ => ShowMenu(true));

            var wasDoodle = false;
            imageReferenceObjectField.RegisterCallback<DragEnterEvent>(_ =>
            {
                wasDoodle = this.GetState().SelectUnlabeledImageReferenceMode(this, m_Index) == ImageReferenceMode.Doodle;
                this.Dispatch(GenerationSettingsActions.setUnlabeledImageReferenceMode, (m_Index, ImageReferenceMode.Asset));
            });
            imageReferenceObjectField.RegisterCallback<DragLeaveEvent>(_ =>
                this.Dispatch(GenerationSettingsActions.setUnlabeledImageReferenceMode, (m_Index, wasDoodle ? ImageReferenceMode.Doodle : ImageReferenceMode.Asset)));

            // State changes
            this.Use(state => state.SelectUnlabeledImageReferenceAsset(this, m_Index), asset => objectField.SetValueWithoutNotify(asset.GetObject()));
            if (strengthSlider != null)
            {
                this.Use(state => state.SelectUnlabeledImageReferenceStrength(this, m_Index), strength =>
                {
                    strengthSlider.SetValueWithoutNotify(Mathf.RoundToInt(strength * 100));
                });
            }

            this.UseArray(state => state.SelectUnlabeledImageReferenceDoodle(this, m_Index), image => doodlePad.SetValueWithoutNotify(image as byte[]));
            this.Use(state => state.SelectUnlabeledImageReferenceMode(this, m_Index), mode =>
            {
                imageReferenceObjectField.RemoveFromClassList(ImageReferenceMode.Asset.ToString().ToLower());
                imageReferenceObjectField.RemoveFromClassList(ImageReferenceMode.Doodle.ToString().ToLower());
                imageReferenceObjectField.AddToClassList(mode.ToString().ToLower());

                if (mode == ImageReferenceMode.Doodle)
                    objectField.SetValueWithoutNotify(null);
            });

            var unsupportedModelOperationIconContainer = this.Q<VisualElement>("warning-icon-container");
            var unsupportedModelOperationBackground = this.Q<VisualElement>("warning-background");

            var iconWarning = unsupportedModelOperationIconContainer?.Q<UnityEngine.UIElements.Image>("warning-icon");
            if (EditorGUIUtility.isProSkin)
            {
                iconWarning?.AddToClassList("dark-warning-icon");
                unsupportedModelOperationBackground?.AddToClassList("dark-warning-background");
            }
            else
            {
                iconWarning?.AddToClassList("light-warning-icon");
                unsupportedModelOperationBackground?.AddToClassList("light-warning-background");
            }

            void UpdateUnlabeledWarningState()
            {
                var maxImages = this.GetState()?.SelectMaxReferenceImages(this) ?? 0;
                var promptIsActive = this.GetState()?.SelectImageReferenceIsActive(this, ImageReferenceType.PromptImage) ?? false;
                var effectiveMax = Mathf.Max(maxImages - (promptIsActive ? 1 : 0), 0);
                var isExcess = m_Index >= effectiveMax;

                unsupportedModelOperationIconContainer?.EnableInClassList("hidden", !isExcess);
                unsupportedModelOperationBackground?.EnableInClassList("hidden", !isExcess);
                if (isExcess && unsupportedModelOperationIconContainer != null)
                    unsupportedModelOperationIconContainer.tooltip = "Too many controls for this model. Remove some controls by pressing X or choose a compatible model.";
            }

            this.Use(state => state.SelectMaxReferenceImages(this), _ => UpdateUnlabeledWarningState());
            this.Use(state => state.SelectUnlabeledImageReferences(this), _ => UpdateUnlabeledWarningState());

            return;

            void ShowMenu(bool isContextClick = false)
            {
                var menu = new GenericMenu();

                if (isContextClick)
                {
                    menu.AddItem(new GUIContent("Edit"), false, Edit);
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Import from Project"), false, () => objectField.ShowObjectPicker());
                }
                menu.AddItem(new GUIContent("Import from disk"), false, () =>
                {
                    var path = EditorUtility.OpenFilePanel("Import image", "", "png,jpg,jpeg");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var data = FileIO.ReadAllBytes(path);
                        this.Dispatch(GenerationSettingsActions.setUnlabeledImageReferenceMode, (m_Index, ImageReferenceMode.Doodle));
                        this.Dispatch(GenerationSettingsActions.setUnlabeledImageReferenceDoodle, (m_Index, data));
                    }
                });
                menu.AddSeparator("");

                var isClear = IsReferenceClear();
                if (!isClear)
                    menu.AddItem(new GUIContent("Clear"), false, Clear);
                else
                    menu.AddDisabledItem(new GUIContent("Clear"));

                var canPaste = EditorGUIUtility.systemCopyBuffer.StartsWith("MetadataDoodleBytes:") ||
                    EditorGUIUtility.systemCopyBuffer.StartsWith("MetadataAssetRef:");
                if (canPaste)
                    menu.AddItem(new GUIContent("Paste"), false, TryPaste);
                else
                    menu.AddDisabledItem(new GUIContent("Paste"));

                if (isContextClick)
                    menu.ShowAsContext();
                else
                    menu.DropDown(settingsButton.worldBound);
            }

            void Clear()
            {
                doodlePad.value = null;
                this.Dispatch(GenerationSettingsActions.setUnlabeledImageReferenceMode, (m_Index, ImageReferenceMode.Asset));
                this.Dispatch(GenerationSettingsActions.setUnlabeledImageReferenceDoodle, (m_Index, (byte[])null));
                this.Dispatch(GenerationSettingsActions.setUnlabeledImageReferenceAsset, (m_Index, (AssetReference)null));
                objectField.value = null;
            }

            bool IsReferenceClear()
            {
                var asset = this.GetState().SelectUnlabeledImageReferenceAsset(this, m_Index);
                var doodle = this.GetState().SelectUnlabeledImageReferenceDoodle(this, m_Index);
                return !asset.IsValid() && doodle is not { Length: not 0 };
            }

            void TryPaste()
            {
                try
                {
                    var buffer = EditorGUIUtility.systemCopyBuffer;
                    const string doodlePrefix = "MetadataDoodleBytes:";
                    const string assetPrefix = "MetadataAssetRef:";

                    if (buffer.StartsWith(doodlePrefix))
                    {
                        var imageString = buffer.Substring(doodlePrefix.Length);
                        var bytes = Convert.FromBase64String(imageString);
                        this.Dispatch(GenerationSettingsActions.setUnlabeledImageReferenceMode, (m_Index, ImageReferenceMode.Doodle));
                        this.Dispatch(GenerationSettingsActions.setUnlabeledImageReferenceDoodle, (m_Index, bytes));
                    }
                    else if (buffer.StartsWith(assetPrefix))
                    {
                        var imageGuid = buffer.Substring(assetPrefix.Length);
                        var assetPath = AssetDatabase.GUIDToAssetPath(imageGuid);
                        var assetRef = Unity.AI.Generators.Asset.AssetReferenceExtensions.FromPath(assetPath);

                        this.Dispatch(GenerationSettingsActions.setUnlabeledImageReferenceMode, (m_Index, ImageReferenceMode.Asset));
                        this.Dispatch(GenerationSettingsActions.setUnlabeledImageReferenceAsset, (m_Index, assetRef));
                    }
                }
                catch
                {
                    Debug.LogError("Invalid data");
                }
            }

            async void Edit()
            {
                var state = this.GetState();
                var imgRefAsset = state.SelectUnlabeledImageReferenceAsset(this, m_Index);
                var imgRefDoodle = state.SelectUnlabeledImageReferenceDoodle(this, m_Index);
                var mode = state.SelectUnlabeledImageReferenceMode(this, m_Index);
                byte[] data;
                if (mode == ImageReferenceMode.Doodle)
                    data = imgRefDoodle;
                else if (imgRefAsset.IsValid())
                {
                    await using var stream = await imgRefAsset.GetCompatibleImageStreamAsync();
                    data = await stream.ReadFullyAsync();
                }
                else
                    data = null;

                var screenScaleFactor = this.GetContext<ScreenScaleFactor>()?.value ?? 1f;
                var doodleSize = doodlePad.GetDoodleSize();
                var tex = new Texture2D(1, 1);
                if (data is { Length: > 0 })
                    tex.LoadImage(data);
                var windowArgs = new DoodleWindowArgs(this.GetAsset(), ImageReferenceType.PromptImage, data, doodleSize, false);
                if (DoodleWindow.Open(windowArgs, m_Index) is { hasUnsavedChanges: false })
                {
                    this.Dispatch(GenerationSettingsActions.setUnlabeledImageReferenceMode, (m_Index, ImageReferenceMode.Doodle));
                    this.Dispatch(GenerationSettingsActions.setUnlabeledImageReferenceDoodle, (m_Index, data));
                }
                tex.SafeDestroy();
            }
        }
    }
}
