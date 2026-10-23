using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor.Checkpoint.Events;
using Unity.AI.Assistant.Editor.Checkpoint.Git;
using Unity.AI.Assistant.Editor.Utils.Event;
using Unity.AI.Assistant.Utils;
using Unity.AI.Toolkit;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.Editor.Checkpoint
{
    [InitializeOnLoad]
    static class AssistantCheckpoints
    {
        const string k_LibraryFolderName = "Library";
        const string k_RepoFolderName = "AssistantCheckpoints";
        const string k_SessionStateInitializingKey = "AssistantCheckpointsInitializing";

        // Shared with the developer-tools debug menu so the key isn't duplicated as a literal.
        internal const string DiscoveryInitializationFailedSessionKey = "CheckpointDiscoveryBannerInitializationFailed";

        // Banner SessionState keys kept here too so the debug-menu reset can clear them statically.
        internal const string DiscoveryProgressSessionKey = "CheckpointDiscoveryBannerProgress";
        internal const string DiscoverySyncStartTimeSessionKey = "CheckpointDiscoverySyncStartTime";

        static readonly string k_ProjectPath = Path.GetDirectoryName(Application.dataPath);
        static readonly string k_LibraryPath = Path.Combine(k_ProjectPath, k_LibraryFolderName);
        static readonly string k_RepoPath = Path.Combine(k_LibraryPath, k_RepoFolderName);

        static readonly CheckpointSystem s_System = new(k_RepoPath, k_ProjectPath);
        static readonly SemaphoreSlim s_GitLock = new(1, 1);
        static Task<CheckpointResult<bool>> s_InitTask;

        static AssistantCheckpoints()
        {
            EditorTask.delayCall += OnDomainReloadDelayed;
            AssistantEvents.Subscribe<EventCheckpointRestoreRequested>(OnRestoreRequested);
            AssistantEvents.Subscribe<EventCheckpointDeleteRequested>(OnDeleteRequested);

            // A domain reload can kill the init before its finally resets the flag; clear it here so
            // the banner can't get stuck on "Syncing".
            AssemblyReloadEvents.beforeAssemblyReload += () =>
                SessionState.SetBool(k_SessionStateInitializingKey, false);
        }

        public static bool IsInitialized => s_System.IsInitialized;

        public static bool IsInitializing
        {
            get
            {
                var task = s_InitTask;
                return task != null && !task.IsCompleted;
            }
        }

        // True while initializing. SessionState-backed so it survives a domain reload mid-init.
        public static bool IsInitializingSession => SessionState.GetBool(k_SessionStateInitializingKey, false);

        public static string RepositoryPath => s_System.RepositoryPath;
        public static IReadOnlyList<string> LastVerificationMissingFiles => s_System.LastVerificationMissingFiles;

        static async void OnDomainReloadDelayed()
        {
            if (!AssistantProjectPreferences.CheckpointEnabled)
            {
                return;
            }

            try
            {
                // Check repository health - reinitialize if missing, corrupted, or deleted
                var health = s_System.GetRepositoryHealth();
                if (health.Status == VcsRepositoryHealthStatus.Missing)
                {
                    // Folder was deleted or never existed - reinitialize
                    InternalLog.Log("Checkpoint repository missing, reinitializing...");
                }
                else if (health.Status == VcsRepositoryHealthStatus.Corrupted)
                {
                    InternalLog.Log("Checkpoint repository corrupted, reinitializing...");
                }

                var result = await InitializeWithSessionFlagAsync();

                if (!result.Success)
                {
                    InternalLog.LogWarning($"Failed to reconnect after domain reload: {result.InternalMessage}");
                    return;
                }

                // Skip cleanup if a checkpoint is being created; next domain reload picks it up.
                if (await s_GitLock.WaitAsync(0))
                {
                    try
                    {
                        await s_System.DeleteOldCheckpointsAsync(AssistantProjectPreferences.CheckpointRetentionWeeks);
                    }
                    finally
                    {
                        s_GitLock.Release();
                    }
                }
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"Error reconnecting after domain reload: {ex.Message}");
            }
        }

        /// <summary>
        /// Called from CheckpointDiscoveryBanner.Refresh() to kick off the first-time auto-init if it
        /// has not already started or completed. Fire-and-forget; callers do not need to await it.
        /// On completion it surfaces success or failure to the banner via EventCheckpointEnableStateChanged.
        /// </summary>
        public static async void EnsureInitializingAsync()
        {
            // Skip if initialization is already in progress or complete
            if (IsInitializing || IsInitializingSession || IsInitialized)
                return;

            // Skip if user explicitly disabled via the discovery banner
            if (AssistantProjectPreferences.CheckpointDiscoveryUserDisabled)
                return;

            // Mark that the auto-init flow ran, so the banner's terminal states only show to users who
            // went through discovery (not those who already had checkpoints enabled).
            AssistantProjectPreferences.CheckpointDiscoveryFlowStarted = true;

            try
            {
                var result = await InitializeWithSessionFlagAsync();

                // Honor a Disable clicked while the init was finishing: leave checkpoints off and fire
                // the disabled event so the Preferences page unlocks and the banner settles on Disabled.
                if (AssistantProjectPreferences.CheckpointDiscoveryUserDisabled)
                {
                    AssistantEvents.Send(new EventCheckpointEnableStateChanged(false));
                    return;
                }

                if (result.Success)
                {
                    SessionState.SetBool(DiscoveryInitializationFailedSessionKey, false);
                    AssistantProjectPreferences.CheckpointEnabled = true;
                    AssistantEvents.Send(new EventCheckpointEnableStateChanged(true));
                }
                else
                {
                    InternalLog.LogWarning($"Failed to initialize checkpoints on window open: {result.InternalMessage}");
                    SessionState.SetBool(DiscoveryInitializationFailedSessionKey, true);
                    AssistantEvents.Send(new EventCheckpointEnableStateChanged(false));
                }
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"Error initializing checkpoints on window open: {ex.Message}");
                SessionState.SetBool(DiscoveryInitializationFailedSessionKey, true);
                AssistantEvents.Send(new EventCheckpointEnableStateChanged(false));
            }
        }

        /// <summary>
        /// Runs <see cref="InitializeAsync"/> while holding the SessionState "initializing" flag, so the
        /// discovery banner can reflect in-progress state (including across a domain reload).
        /// </summary>
        static async Task<CheckpointResult<bool>> InitializeWithSessionFlagAsync(CancellationToken ct = default)
        {
            SessionState.SetBool(k_SessionStateInitializingKey, true);
            try
            {
                return await InitializeAsync(ct);
            }
            finally
            {
                SessionState.SetBool(k_SessionStateInitializingKey, false);
            }
        }

        /// <summary>
        /// Clears the SessionState tracking an in-progress/failed initialization. Used by developer tools
        /// when resetting, so a stale flag can't leave the banner stuck on "Syncing" on the next open.
        /// </summary>
        public static void ClearInitializationState()
        {
            SessionState.SetBool(k_SessionStateInitializingKey, false);
            SessionState.SetBool(DiscoveryInitializationFailedSessionKey, false);
            SessionState.SetFloat(DiscoveryProgressSessionKey, 0f);
            SessionState.EraseFloat(DiscoverySyncStartTimeSessionKey);
        }

        public static VcsRepositoryHealth GetRepositoryHealth()
        {
            return s_System.GetRepositoryHealth();
        }

        public static GitValidationResult ValidateGitInstance()
        {
            var config = new GitInstanceConfig(
                AssistantProjectPreferences.GitInstanceType,
                AssistantProjectPreferences.CustomGitPath);
            return GitInstanceResolver.ValidateConfig(config);
        }

        public static async Task<CheckpointResult<bool>> InitializeAsync(CancellationToken ct = default)
        {
            var existingTask = s_InitTask;
            if (existingTask != null && !existingTask.IsCompleted)
            {
                return await existingTask;
            }

            await s_GitLock.WaitAsync(ct);
            try
            {
                if (s_System.IsInitialized)
                {
                    return CheckpointResult<bool>.Ok(true, "Repository already initialized");
                }

                s_InitTask = InitializeCoreAsync(ct);
                return await s_InitTask;
            }
            finally
            {
                s_GitLock.Release();
            }
        }

        static async Task<CheckpointResult<bool>> InitializeCoreAsync(CancellationToken ct)
        {
            // Fire before validation so the banner enters the Syncing state for every attempt; a fast
            // validation failure then transitions Syncing -> Failed instead of leaving no feedback.
            AssistantEvents.Send(new EventCheckpointInitializationStarted());

            var validation = ValidateGitInstance();

            if (!validation.IsValid)
            {
                var fallback = GitInstanceResolver.FindFirstValidInstance(AssistantProjectPreferences.CustomGitPath);
                if (fallback.HasValue)
                {
                    AssistantProjectPreferences.GitInstanceType = fallback.Value;
                    validation = ValidateGitInstance();
                }
            }

            if (!validation.IsValid)
            {
                var errorType = validation.GitFound
                    ? CheckpointErrorType.VcsExtensionMissing
                    : CheckpointErrorType.VcsNotFound;
                return CheckpointResult<bool>.Fail(errorType, validation.ErrorMessage, validation.ErrorMessage);
            }

            return await s_System.InitializeAsync(ct);
        }

        public static async Task<CheckpointResult<string>> CreateCheckpointAsync(string message, CancellationToken ct = default)
        {
            await s_GitLock.WaitAsync(ct);
            try
            {
                return await s_System.CreateCheckpointAsync(message, ct);
            }
            finally
            {
                s_GitLock.Release();
            }
        }

        public static async Task<List<CheckpointInfo>> GetCheckpointsAsync(CancellationToken ct = default)
        {
            await s_GitLock.WaitAsync(ct);
            try
            {
                return await s_System.GetCheckpointsAsync(ct);
            }
            finally
            {
                s_GitLock.Release();
            }
        }

        public static async Task<CheckpointInfo?> GetInitialCommitInfoAsync(CancellationToken ct = default)
        {
            await s_GitLock.WaitAsync(ct);
            try
            {
                return await s_System.GetInitialCommitInfoAsync(ct);
            }
            finally
            {
                s_GitLock.Release();
            }
        }

        static async Task<CheckpointResult<bool>> RestoreCheckpointAsync(string commitHash, CancellationToken ct = default)
        {
            await s_GitLock.WaitAsync(ct);
            try
            {
                return await s_System.RestoreCheckpointAsync(commitHash, ct);
            }
            finally
            {
                s_GitLock.Release();
            }
        }

        static async Task<CheckpointResult<bool>> DeleteCheckpointAsync(string commitHash, CancellationToken ct = default)
        {
            await s_GitLock.WaitAsync(ct);
            try
            {
                return await s_System.DeleteCheckpointAsync(commitHash, ct);
            }
            finally
            {
                s_GitLock.Release();
            }
        }

        public static async Task UpdateTagAsync(string commitHash, AssistantConversationId conversationId, string oldFragmentId, string newFragmentId, CancellationToken ct = default)
        {
            await s_System.UpdateTagAsync(commitHash, conversationId, oldFragmentId, newFragmentId, ct);
        }

        public static async Task<string> GetCheckpointForMessageAsync(AssistantConversationId conversationId, string fragmentId, CancellationToken ct = default)
        {
            return await s_System.GetCheckpointForMessageAsync(conversationId, fragmentId, ct);
        }

        public static async Task<CheckpointInfo?> GetCheckpointInfoForMessageAsync(AssistantConversationId conversationId, string fragmentId, CancellationToken ct = default)
        {
            return await s_System.GetCheckpointInfoForMessageAsync(conversationId, fragmentId, ct);
        }

        public static bool HasCheckpointForMessage(AssistantConversationId conversationId, string fragmentId)
        {
            return s_System.HasCheckpointForMessage(conversationId, fragmentId);
        }

        public static async Task RefreshTagsCacheAsync(CancellationToken ct = default)
        {
            await s_System.RefreshTagsCacheAsync(ct);
        }

        public static void SetPendingCheckpoint(AssistantConversationId conversationId, string incompleteFragmentId, string checkpointHash)
        {
            s_System.SetPendingCheckpoint(conversationId, incompleteFragmentId, checkpointHash);
        }

        public static async Task CompletePendingCheckpointAsync(AssistantConversationId conversationId, string incompleteFragmentId, string realFragmentId, CancellationToken ct = default)
        {
            await s_System.CompletePendingCheckpointAsync(conversationId, incompleteFragmentId, realFragmentId, ct);
        }

        public static bool HasPendingCheckpoint(AssistantConversationId conversationId, string incompleteFragmentId)
        {
            return s_System.HasPendingCheckpoint(conversationId, incompleteFragmentId);
        }

        public static async Task<CheckpointResult<bool>> ResetRepositoryAsync(CancellationToken ct = default)
        {
            if (!AssistantProjectPreferences.CheckpointEnabled)
            {
                return CheckpointResult<bool>.Ok(true, "Repository reset (checkpoints disabled)");
            }

            return await s_System.ResetRepositoryAsync(ct);
        }

        public static Task InitializeAnywayAsync(CancellationToken ct = default)
        {
            return s_System.InitializeAnywayAsync(ct);
        }

        public static async Task<CheckpointResult<int>> VerifyInitialCommitAsync(CancellationToken ct = default)
        {
            await s_GitLock.WaitAsync(ct);
            try
            {
                return await s_System.VerifyInitialCommitAsync(ct);
            }
            finally
            {
                s_GitLock.Release();
            }
        }

        public static async Task<IReadOnlyList<CheckpointFileChange>> GetRestoreChangesAsync(string targetCommitHash, CancellationToken ct = default)
        {
            await s_GitLock.WaitAsync(ct);
            try
            {
                return await s_System.GetRestoreChangesAsync(targetCommitHash, ct);
            }
            finally
            {
                s_GitLock.Release();
            }
        }

        public static Task<AssetSaveResult> SaveModifiedAssetsAsync()
        {
            return CheckpointSystem.SaveAllAssetsAsync();
        }

        static async void OnRestoreRequested(EventCheckpointRestoreRequested evt)
        {
            var messageId = evt.MessageId;

            try
            {
                if (!IsInitialized)
                {
                    AssistantEvents.Send(new EventCheckpointRestoreCompleted(messageId, false, "Checkpoints not initialized"));
                    return;
                }

                if (!messageId.ConversationId.IsValid || string.IsNullOrEmpty(messageId.FragmentId))
                {
                    AssistantEvents.Send(new EventCheckpointRestoreCompleted(messageId, false, "Invalid message ID"));
                    return;
                }

                var hash = await GetCheckpointForMessageAsync(messageId.ConversationId, messageId.FragmentId);
                if (string.IsNullOrEmpty(hash))
                {
                    AssistantEvents.Send(new EventCheckpointRestoreCompleted(messageId, false, "Checkpoint not found"));
                    return;
                }

                var result = await RestoreCheckpointAsync(hash);
                var message = result.Success ? "Reverted to checkpoint" : result.PublicMessage;
                AssistantEvents.Send(new EventCheckpointRestoreCompleted(messageId, result.Success, message));
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"Restore failed: {ex.Message}");
                AssistantEvents.Send(new EventCheckpointRestoreCompleted(messageId, false, "Internal error"));
            }
        }

        static async void OnDeleteRequested(EventCheckpointDeleteRequested evt)
        {
            var messageId = evt.MessageId;

            try
            {
                if (!IsInitialized)
                {
                    AssistantEvents.Send(new EventCheckpointDeleteCompleted(messageId, false, "Checkpoints not initialized"));
                    return;
                }

                if (!messageId.ConversationId.IsValid || string.IsNullOrEmpty(messageId.FragmentId))
                {
                    AssistantEvents.Send(new EventCheckpointDeleteCompleted(messageId, false, "Invalid message ID"));
                    return;
                }

                var hash = await GetCheckpointForMessageAsync(messageId.ConversationId, messageId.FragmentId);
                if (string.IsNullOrEmpty(hash))
                {
                    AssistantEvents.Send(new EventCheckpointDeleteCompleted(messageId, false, "Checkpoint not found"));
                    return;
                }

                var result = await DeleteCheckpointAsync(hash);
                var message = result.Success ? "Deleted checkpoint" : result.PublicMessage;
                AssistantEvents.Send(new EventCheckpointDeleteCompleted(messageId, result.Success, message));
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"Delete failed: {ex.Message}");
                AssistantEvents.Send(new EventCheckpointDeleteCompleted(messageId, false, "Internal error"));
            }
        }
    }
}
