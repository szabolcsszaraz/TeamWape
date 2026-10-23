using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Checkpoint;
using Unity.AI.Assistant.Editor.Checkpoint.Events;
using Unity.AI.Assistant.Editor.Checkpoint.Git;
using Unity.AI.Assistant.Editor.Utils.Event;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using Unity.AI.Assistant.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    [UxmlElement]
    partial class ConfigureCheckpointSettingsView : ManagedTemplate
    {
        const string k_ValidationSuccessClass = "checkpoint-validation-success";
        const string k_CustomText = "Custom";
        const string k_LoadingText = "Loading...";
        const string k_NotInitializedText = "Not initialized";
        const string k_VerificationOkClass = "checkpoint-verification-ok";
        const string k_StatusOkClass = "checkpoint-verification-status--ok";
        const string k_StatusFailedClass = "checkpoint-verification-status--failed";
        const string k_InitializeAnywayWarningHiddenClass = "checkpoint-initialize-anyway-warning--hidden";
        const int k_MissingFilesDisplayLimit = 20;

        static bool s_DetectionComplete;
        static string s_SystemVersion;
        static bool s_SystemFound;
        static string s_LastCustomPath;
        static readonly Dictionary<GitInstanceType, GitValidationResult> k_ValidationCache = new();

        Toggle m_EnableToggle;
        VisualElement m_InitIndicator;
        Label m_InitLabel;
        VisualElement m_DisclaimerContainer;
        LoadingSpinner m_Spinner;

        DropdownField m_GitDropdown;
        VisualElement m_CustomPathContainer;
        TextField m_CustomPathField;
        VisualElement m_ValidationContainer;
        Label m_ValidationLabel;
        VisualElement m_ErrorContainer;
        Label m_ErrorText;

        TextField m_RepoLocationField;
        Button m_OpenRepoButton;
        DropdownField m_RetentionDropdown;

        Button m_ResetBannerButton;

        VisualElement m_VerificationSection;
        Label m_VerificationStatusLabel;
        Label m_MissingFilesHeader;
        Label m_MissingFilesContent;
        Label m_MissingFilesOverflow;
        Button m_InitializeAnywayButton;
        VisualElement m_InitializeAnywayWarning;

        bool m_IsInitializing;
        bool m_IsLoading;
        bool m_InitializeAnywayConfirmPending;
        string m_ErrorMessage;
        List<GitInstanceType> m_DropdownTypes = new();

        BaseEventSubscriptionTicket m_CheckpointEnableStateChangedTicket;
        BaseEventSubscriptionTicket m_InitStartedTicket;

        public ConfigureCheckpointSettingsView() : base(AssistantUIConstants.UIModulePath)
        {
            RegisterAttachEvents(OnAttach, OnDetach);
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_EnableToggle = view.Q<Toggle>("checkpointEnabledToggle");
            m_EnableToggle.RegisterValueChangedCallback(OnEnableToggled);

            m_InitIndicator = view.Q<VisualElement>("checkpointInitializingIndicator");
            m_InitLabel = m_InitIndicator.Q<Label>("initializingLabel");
            m_DisclaimerContainer = view.Q<VisualElement>("checkpointDisclaimerContainer");
            m_Spinner = new LoadingSpinner();
            m_InitIndicator.Insert(0, m_Spinner);

            m_GitDropdown = view.Q<DropdownField>("gitInstanceDropdown");
            m_GitDropdown.RegisterValueChangedCallback(OnGitDropdownChanged);

            m_CustomPathContainer = view.Q<VisualElement>("customGitPathContainer");
            m_CustomPathField = view.Q<TextField>("customGitPathField");
            m_CustomPathField.RegisterCallback<FocusOutEvent>(OnCustomPathFocusOut);
            m_CustomPathField.RegisterCallback<KeyDownEvent>(OnCustomPathKeyDown);
            view.SetupButton("browseGitPathButton", OnBrowseGitPath);

            m_ValidationContainer = view.Q<VisualElement>("gitValidationContainer");
            m_ValidationLabel = view.Q<Label>("gitValidationLabel");
            m_ErrorContainer = view.Q<VisualElement>("gitValidationErrorContainer");
            m_ErrorText = view.Q<Label>("gitValidationErrorText");

            m_RepoLocationField = view.Q<TextField>("repositoryLocationField");
            m_RepoLocationField.isReadOnly = true;
            m_OpenRepoButton = view.SetupButton("openRepositoryButton", OnOpenRepo);

            m_RetentionDropdown = view.Q<DropdownField>("checkpointRetentionWeeksDropdown");
            m_RetentionDropdown.choices = new List<string> { "1", "2" };
            m_RetentionDropdown.SetValueWithoutNotify(AssistantProjectPreferences.CheckpointRetentionWeeks.ToString());
            m_RetentionDropdown.RegisterValueChangedCallback(e =>
            {
                if (int.TryParse(e.newValue, out var weeks))
                {
                    AssistantProjectPreferences.CheckpointRetentionWeeks = weeks;
                }
            });

            m_ResetBannerButton = view.SetupButton("showDiscoveryBannerButton", _ => OnShowDiscoveryBanner());

            m_VerificationSection = view.Q<VisualElement>("verificationSection");
            m_VerificationStatusLabel = view.Q<Label>("verificationStatusLabel");
            m_MissingFilesHeader = view.Q<Label>("missingFilesHeader");
            m_MissingFilesContent = view.Q<Label>("missingFilesContent");
            m_MissingFilesOverflow = view.Q<Label>("missingFilesOverflow");
            m_InitializeAnywayWarning = view.Q<VisualElement>("initializeAnywayWarning");

            view.SetupButton("retryVerificationButton", _ => OnRetryVerification());
            m_InitializeAnywayButton = view.SetupButton("initializeAnywayButton", _ => OnInitializeAnyway());
        }

        void OnAttach(AttachToPanelEvent evt)
        {
            AssistantProjectPreferences.CheckpointEnabledChanged += RefreshUI;
            AssistantProjectPreferences.GitInstanceTypeChanged += OnGitTypeChanged;
            m_CheckpointEnableStateChangedTicket = AssistantEvents.Subscribe<EventCheckpointEnableStateChanged>(OnCheckpointEnableStateChanged);
            // Re-derive (and lock) when a banner-driven init starts, so the page reflects/guards it.
            m_InitStartedTicket = AssistantEvents.Subscribe<EventCheckpointInitializationStarted>(_ => RefreshUI());

            k_ValidationCache.Clear();
            m_ErrorMessage = null;

            if (s_DetectionComplete)
            {
                EnsureValidSelection();
                RefreshUI();
            }
            else
            {
                m_IsLoading = true;
                RefreshUI();
                DetectGitAsync();
            }
        }

        void OnDetach(DetachFromPanelEvent evt)
        {
            AssistantProjectPreferences.CheckpointEnabledChanged -= RefreshUI;
            AssistantProjectPreferences.GitInstanceTypeChanged -= OnGitTypeChanged;
            AssistantEvents.Unsubscribe(ref m_CheckpointEnableStateChangedTicket);
            AssistantEvents.Unsubscribe(ref m_InitStartedTicket);
        }

        void RefreshUI()
        {
            var isLoading = m_IsLoading;
            // Lock the controls while ANY checkpoint init is in progress - including one started by the
            // discovery banner - so settings can't be changed against an incomplete git repository.
            var isInitializing = m_IsInitializing || AssistantCheckpoints.IsInitializing || AssistantCheckpoints.IsInitializingSession;
            var isBusy = isLoading || isInitializing;
            var checkpointEnabled = AssistantProjectPreferences.CheckpointEnabled;
            var gitType = AssistantProjectPreferences.GitInstanceType;
            var validation = isBusy ? default : GetValidation(useCache: true);

            // Loading/initializing indicator
            m_InitIndicator.SetDisplay(isBusy);
            if (isBusy)
            {
                m_Spinner.Show();
                if (isLoading)
                    m_InitLabel.text = "Detecting git...";
                else if (AssistantProjectPreferences.CheckpointDiscoveryUserDisabled)
                    m_InitLabel.text = "Cancelling...";
                else
                    m_InitLabel.text = "Initializing...";
            }
            else
            {
                m_Spinner.Hide();
            }

            // Git dropdown
            m_GitDropdown.SetEnabled(!isBusy);
            if (isLoading)
            {
                m_GitDropdown.choices = new List<string> { k_LoadingText };
                m_GitDropdown.SetValueWithoutNotify(k_LoadingText);
            }
            else
            {
                BuildDropdownChoices();
                var idx = m_DropdownTypes.IndexOf(gitType);
                if (idx >= 0)
                {
                    m_GitDropdown.SetValueWithoutNotify(m_GitDropdown.choices[idx]);
                }
            }

            // Custom path
            m_CustomPathContainer.SetDisplay(!isLoading && gitType == GitInstanceType.Custom);
            m_CustomPathField.SetValueWithoutNotify(AssistantProjectPreferences.CustomGitPath);

            // Validation display
            var hasError = !string.IsNullOrEmpty(m_ErrorMessage);
            var showValidation = !isBusy && !hasError && validation.IsValid;
            var showError = !isBusy && (hasError || !validation.IsValid);

            m_ValidationContainer.SetDisplay(showValidation);
            m_ErrorContainer.SetDisplay(showError);

            if (showValidation)
            {
                m_ValidationLabel.text = $"Git {validation.GitVersion}, LFS {validation.LfsVersion}";
                m_ValidationLabel.AddToClassList(k_ValidationSuccessClass);
            }
            else
            {
                m_ValidationLabel.RemoveFromClassList(k_ValidationSuccessClass);
            }

            if (showError)
            {
                m_ErrorText.text = hasError ? m_ErrorMessage : validation.ErrorMessage;
            }

            m_EnableToggle.SetValueWithoutNotify(isInitializing || checkpointEnabled);
            m_EnableToggle.SetEnabled(!isBusy && (validation.IsValid || checkpointEnabled));
            m_DisclaimerContainer.SetDisplay(checkpointEnabled);
            var repoInitialized = AssistantCheckpoints.IsInitialized;
            m_RepoLocationField.SetValueWithoutNotify(
                repoInitialized ? AssistantCheckpoints.RepositoryPath : k_NotInitializedText);
            m_RepoLocationField.SetEnabled(repoInitialized);
            m_OpenRepoButton.SetEnabled(repoInitialized);

            // While an init is in progress, lock the remaining editable settings too - they must not
            // change against an incomplete git repository.
            m_RetentionDropdown.SetEnabled(!isBusy);
            m_CustomPathContainer.SetEnabled(!isBusy);

            // Enable the reset only when the banner is actually dismissed - otherwise there's nothing to restore.
            m_ResetBannerButton.SetEnabled(AssistantProjectPreferences.CheckpointDiscoveryBannerDismissed);

            RefreshVerificationSection();
        }

        void RefreshVerificationSection()
        {
            var checkpointEnabled = AssistantProjectPreferences.CheckpointEnabled;
            var isInitialized = AssistantCheckpoints.IsInitialized;
            var missingFiles = AssistantCheckpoints.LastVerificationMissingFiles;
            var verificationFailed = !isInitialized && missingFiles.Count > 0;

            // Section only carries meaning once checkpoints are enabled and init has been attempted.
            var showSection = checkpointEnabled && (isInitialized || verificationFailed);
            m_VerificationSection.SetDisplay(showSection);
            m_VerificationSection.EnableInClassList(k_VerificationOkClass, isInitialized);

            if (!showSection)
            {
                return;
            }

            if (isInitialized)
            {
                m_VerificationStatusLabel.text = "Status: Initialized";
                m_VerificationStatusLabel.EnableInClassList(k_StatusOkClass, true);
                m_VerificationStatusLabel.EnableInClassList(k_StatusFailedClass, false);
                return;
            }

            m_VerificationStatusLabel.text = "Status: Verification failed";
            m_VerificationStatusLabel.EnableInClassList(k_StatusOkClass, false);
            m_VerificationStatusLabel.EnableInClassList(k_StatusFailedClass, true);

            m_MissingFilesHeader.text = "Files missing from initial checkpoint: " + missingFiles.Count;

            var displayCount = Math.Min(missingFiles.Count, k_MissingFilesDisplayLimit);
            var lines = new StringBuilder();
            for (var i = 0; i < displayCount; ++i)
            {
                if (i > 0) lines.Append('\n');
                lines.Append(missingFiles[i]);
            }
            m_MissingFilesContent.text = lines.ToString();

            var overflow = missingFiles.Count - displayCount;
            m_MissingFilesOverflow.SetDisplay(overflow > 0);
            if (overflow > 0)
            {
                m_MissingFilesOverflow.text = "... and " + overflow + " more";
            }

            if (!m_InitializeAnywayConfirmPending)
            {
                m_InitializeAnywayButton.text = "Initialize Anyway";
                m_InitializeAnywayWarning.AddToClassList(k_InitializeAnywayWarningHiddenClass);
            }
        }

        void BuildDropdownChoices()
        {
            var choices = new List<string>();
            m_DropdownTypes.Clear();

            if (s_SystemFound)
            {
                choices.Add(string.IsNullOrEmpty(s_SystemVersion) ? "System" : $"System (git {s_SystemVersion})");
                m_DropdownTypes.Add(GitInstanceType.System);
            }

            choices.Add(k_CustomText);
            m_DropdownTypes.Add(GitInstanceType.Custom);

            m_GitDropdown.choices = choices;
        }

        async void DetectGitAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    var sys = GitInstanceResolver.DetectSystemGit();
                    s_SystemFound = sys.found;
                    s_SystemVersion = sys.version;
                    s_DetectionComplete = true;
                });

                MainThread.DispatchAndForget(() =>
                {
                    if (panel == null) return;

                    m_IsLoading = false;
                    m_ErrorMessage = null;
                    EnsureValidSelection();
                    RefreshUI();
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Checkpoint] Failed to detect git: {ex.Message}");
                MainThread.DispatchAndForget(() =>
                {
                    if (panel == null) return;

                    m_IsLoading = false;
                    m_ErrorMessage = "Failed to detect git. Please specify a custom path.";
                    m_DropdownTypes = new List<GitInstanceType> { GitInstanceType.Custom };
                    AssistantProjectPreferences.GitInstanceType = GitInstanceType.Custom;
                    RefreshUI();
                });
            }
        }

        static void EnsureValidSelection()
        {
            var current = AssistantProjectPreferences.GitInstanceType;
            if (IsAvailable(current))
            {
                return;
            }

            AssistantProjectPreferences.GitInstanceType =
                s_SystemFound ? GitInstanceType.System :
                GitInstanceType.Custom;
        }

        static bool IsAvailable(GitInstanceType type) => type switch
        {
            GitInstanceType.System => s_SystemFound,
            _ => true
        };

        static GitValidationResult GetValidation(bool useCache)
        {
            var type = AssistantProjectPreferences.GitInstanceType;
            var customPath = AssistantProjectPreferences.CustomGitPath;

            if (type == GitInstanceType.Custom && s_LastCustomPath != customPath)
            {
                k_ValidationCache.Remove(GitInstanceType.Custom);
                s_LastCustomPath = customPath;
            }

            if (useCache && k_ValidationCache.TryGetValue(type, out var cached))
            {
                return cached;
            }

            var result = AssistantCheckpoints.ValidateGitInstance();
            k_ValidationCache[type] = result;
            s_LastCustomPath = customPath;
            return result;
        }

        async void OnEnableToggled(ChangeEvent<bool> evt)
        {
            if (m_IsInitializing)
            {
                return;
            }

            if (evt.newValue)
            {
                var validation = GetValidation(useCache: false);
                if (!validation.IsValid)
                {
                    RefreshUI();
                    return;
                }

                // Explicit enable clears any prior opt-out. If the user clicks Disable in the banner
                // while the init below runs, this flips back to true and we honor it on completion.
                AssistantProjectPreferences.CheckpointDiscoveryUserDisabled = false;

                m_IsInitializing = true;
                RefreshUI();

                try
                {
                    var result = await AssistantCheckpoints.InitializeAsync();
                    if (result.Success)
                    {
                        if (AssistantProjectPreferences.CheckpointDiscoveryUserDisabled)
                        {
                            // User opted out via the banner while this init ran - leave checkpoints off.
                            AssistantEvents.Send(new EventCheckpointEnableStateChanged(false));
                        }
                        else
                        {
                            AssistantProjectPreferences.CheckpointEnabled = true;
                            AssistantEvents.Send(new EventCheckpointEnableStateChanged(true));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Checkpoint] Failed: {ex.Message}");
                }
                finally
                {
                    m_IsInitializing = false;
                    RefreshUI();
                }
            }
            else
            {
                AssistantProjectPreferences.CheckpointEnabled = false;
                AssistantProjectPreferences.CheckpointDiscoveryUserDisabled = true;
                RefreshUI();
                AssistantEvents.Send(new EventCheckpointEnableStateChanged(false));
            }
        }

        void OnGitTypeChanged()
        {
            k_ValidationCache.Remove(AssistantProjectPreferences.GitInstanceType);
            m_ErrorMessage = null;
            RefreshUI();
        }

        void OnGitDropdownChanged(ChangeEvent<string> evt)
        {
            var idx = m_GitDropdown.choices.IndexOf(evt.newValue);
            if (idx < 0 || idx >= m_DropdownTypes.Count)
            {
                return;
            }

            var type = m_DropdownTypes[idx];
            k_ValidationCache.Remove(type);
            m_ErrorMessage = null;
            AssistantProjectPreferences.GitInstanceType = type;
            RefreshUI();
        }

        void OnCustomPathFocusOut(FocusOutEvent evt)
        {
            ApplyCustomPath();
        }

        void OnCustomPathKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                ApplyCustomPath();
                evt.StopPropagation();
            }
        }

        void ApplyCustomPath()
        {
            var newPath = m_CustomPathField.value;
            if (AssistantProjectPreferences.CustomGitPath == newPath)
            {
                return;
            }

            AssistantProjectPreferences.CustomGitPath = newPath;
            k_ValidationCache.Remove(GitInstanceType.Custom);
            m_ErrorMessage = null;
            RefreshUI();
        }

        void OnBrowseGitPath(PointerUpEvent evt)
        {
            var currentPath = AssistantProjectPreferences.CustomGitPath;
            var directory = string.IsNullOrEmpty(currentPath) ? "" : Path.GetDirectoryName(currentPath);

#if UNITY_EDITOR_WIN
            var extension = "exe";
#else
            var extension = "";
#endif

            var selectedPath = EditorUtility.OpenFilePanel("Select Git Executable", directory, extension);
            if (!string.IsNullOrEmpty(selectedPath))
            {
                m_CustomPathField.value = selectedPath;
            }
        }

        static void OnOpenRepo(PointerUpEvent evt)
        {
            if (AssistantCheckpoints.IsInitialized)
            {
                EditorUtility.RevealInFinder(AssistantCheckpoints.RepositoryPath);
            }
        }

        async void OnRetryVerification()
        {
            m_IsInitializing = true;
            RefreshUI();

            try
            {
                await AssistantCheckpoints.VerifyInitialCommitAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Checkpoint] Verification failed: {ex.Message}");
            }
            finally
            {
                m_IsInitializing = false;
                RefreshUI();
                if (AssistantCheckpoints.IsInitialized)
                    AssistantEvents.Send(new EventCheckpointEnableStateChanged(true));
            }
        }

        async void OnInitializeAnyway()
        {
            if (!m_InitializeAnywayConfirmPending)
            {
                m_InitializeAnywayConfirmPending = true;
                m_InitializeAnywayButton.text = "Confirm — Initialize Anyway";
                m_InitializeAnywayWarning.RemoveFromClassList(k_InitializeAnywayWarningHiddenClass);
                return;
            }

            m_InitializeAnywayConfirmPending = false;
            m_IsInitializing = true;
            RefreshUI();

            try
            {
                await AssistantCheckpoints.InitializeAnywayAsync();
                AssistantEvents.Send(new EventCheckpointEnableStateChanged(true));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Checkpoint] Initialize anyway failed: {ex.Message}");
            }
            finally
            {
                m_IsInitializing = false;
                RefreshUI();
            }
        }

        void OnShowDiscoveryBanner()
        {
            // Clear the persisted dismissal and ask any open banner to re-evaluate its state immediately.
            AssistantProjectPreferences.CheckpointDiscoveryBannerDismissed = false;
            AssistantEvents.Send(new EventCheckpointDiscoveryBannerRefreshRequested());
            RefreshUI();
        }

        void OnCheckpointEnableStateChanged(EventCheckpointEnableStateChanged evt)
        {
            // Full refresh: the enable/disable may have been driven from the discovery banner, so the
            // toggle and every dependent control - not just the verification section - must re-derive.
            RefreshUI();
        }
    }
}
