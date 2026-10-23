using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor.Checkpoint.Events;
using Unity.AI.Assistant.Editor.Checkpoint.Git;
using Unity.AI.Assistant.Editor.Utils.Event;
using Unity.AI.Assistant.Utils;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Unity.AI.Assistant.Editor.Checkpoint
{
    sealed class CheckpointSystem : IDisposable
    {
        const string k_TagPrefix = "msg-";
        const string k_InitialCommitTag = "initial-commit";

        readonly string m_RepoPath;
        readonly string m_ProjectPath;
        readonly object m_CacheLock = new();
        readonly HashSet<string> m_CachedMessageTags = new();
        readonly Dictionary<string, string> m_PendingCheckpoints = new();

        IVcsAdapter m_Adapter;
        bool m_Disposed;
        bool m_InitialCommitVerified;
        readonly List<string> m_LastVerificationMissingFiles = new();

        public bool IsInitialized => (m_Adapter?.IsInitialized ?? false) && m_InitialCommitVerified;
        public string RepositoryPath => m_Adapter?.RepositoryPath ?? string.Empty;
        public IReadOnlyList<string> LastVerificationMissingFiles => m_LastVerificationMissingFiles;

        public CheckpointSystem(string repoPath, string projectPath)
        {
            if (string.IsNullOrEmpty(repoPath))
            {
                throw new ArgumentNullException(nameof(repoPath));
            }
            if (string.IsNullOrEmpty(projectPath))
            {
                throw new ArgumentNullException(nameof(projectPath));
            }

            m_RepoPath = repoPath;
            m_ProjectPath = projectPath;
        }

        public VcsRepositoryHealth GetRepositoryHealth()
        {
            if (m_Adapter == null)
            {
                return VcsRepositoryHealth.Missing();
            }

            return m_Adapter.CheckHealth();
        }

        // Coarse phase-based progress for the discovery banner (git gives no finer signal for a local commit).
        static void ReportInitProgress(float progress, string phase)
            => AssistantEvents.Send(new Events.EventCheckpointInitializationProgress(progress, phase));

        public async Task<CheckpointResult<bool>> InitializeAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (IsInitialized)
            {
                InternalLog.Log("Repository already initialized");
                return CheckpointResult<bool>.Ok(true, "Repository already initialized");
            }

            try
            {
                var repoExists = Directory.Exists(Path.Combine(m_RepoPath, ".git"));

                m_Adapter?.Dispose();
                m_Adapter = CreateAdapter();

                var health = m_Adapter.CheckHealth();
                if (health.Status == VcsRepositoryHealthStatus.Locked)
                {
                    InternalLog.Log("Cleaning up stale lock files");
                    if (!m_Adapter.TryUnlock())
                    {
                        InternalLog.LogError("Failed to unlock VCS for Checkpointing");
                    }
                }

                if (repoExists && health.Status == VcsRepositoryHealthStatus.Healthy)
                {
                    InternalLog.Log("Reconnected to existing repository");

                    var existingTagHash = await m_Adapter.GetCommitForTagAsync(k_InitialCommitTag, ct);
                    if (string.IsNullOrEmpty(existingTagHash))
                    {
                        try
                        {
                            var rootHash = await m_Adapter.GetRootCommitHashAsync(ct);
                            if (!string.IsNullOrEmpty(rootHash))
                            {
                                await m_Adapter.CreateTagAsync(k_InitialCommitTag, rootHash, ct);
                                InternalLog.Log($"Migrated initial-commit tag to root commit {rootHash}");
                            }
                            else
                            {
                                InternalLog.LogWarning("Could not find root commit for initial-commit tag migration; skipping");
                            }
                        }
                        catch (Exception migEx)
                        {
                            InternalLog.LogWarning($"initial-commit tag migration failed; continuing without tag: {migEx.Message}");
                        }
                    }

                    m_InitialCommitVerified = true;
                    AssistantEvents.Send(new Events.EventCheckpointEnableStateChanged(true));
                    await RefreshTagsCacheAsync(ct);
                    // Notify Checkpoint UI elements that may have been created before the cache was ready.
                    AssistantEvents.Send(new EventCheckpointsChanged());
                    return CheckpointResult<bool>.Ok(true, "Reconnected to existing repository");
                }

                if (repoExists && health.Status == VcsRepositoryHealthStatus.Corrupted)
                {
                    InternalLog.Log("Repository corrupted, reinitializing...");
                    await DeleteRepositoryAsync();
                }

                ReportInitProgress(0.05f, "Preparing repository");
                Directory.CreateDirectory(m_RepoPath);

                var initResult = await m_Adapter.InitializeRepositoryAsync(ct);
                if (!initResult.Success)
                {
                    await TryDeleteGitDirAsync();
                    var errorType = ClassifyVcsError(initResult);
                    return CheckpointResult<bool>.Fail(errorType, "Failed to initialize repository", initResult.Error);
                }

                ReportInitProgress(0.1f, "Repository initialized");

                // Allow CreateCheckpointInternalAsync to proceed before verification runs.
                // OnVerificationFailed will set this back to false if the post-init check fails.
                m_InitialCommitVerified = true;

                string initialHash;
                try
                {
                    initialHash = await CreateCheckpointInternalAsync("Initial checkpoint - Project snapshot", ct, ReportInitProgress);
                }
                catch (OperationCanceledException)
                {
                    await TryDeleteGitDirAsync();
                    return CheckpointResult<bool>.Fail(CheckpointErrorType.Cancelled,
                        "Checkpoint initialization cancelled. Save your scenes (or accept the save dialog) and try again.");
                }
                catch (Exception ex)
                {
                    await TryDeleteGitDirAsync();
                    return CheckpointResult<bool>.Fail(CheckpointErrorType.VcsCommandFailed, "Failed to create initial checkpoint", ex.Message);
                }

                if (string.IsNullOrEmpty(initialHash))
                {
                    await TryDeleteGitDirAsync();
                    return CheckpointResult<bool>.Fail(CheckpointErrorType.VcsCommandFailed, "Failed to create initial checkpoint");
                }

                ReportInitProgress(0.9f, "Finalizing setup");
                await m_Adapter.CreateTagAsync(k_InitialCommitTag, initialHash, ct);
                await RunInitialCommitVerificationAsync(initialHash, ct);

                ReportInitProgress(1f, "Done");

                if (m_InitialCommitVerified)
                {
                    AssistantEvents.Send(new Events.EventCheckpointEnableStateChanged(true));
                }

                InternalLog.Log("Repository initialized successfully");
                return CheckpointResult<bool>.Ok(true, "Repository initialized");
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"Failed to initialize: {ex.Message}");
                return CheckpointResult<bool>.Fail(CheckpointErrorType.VcsCommandFailed, "Failed to initialize checkpoints", ex.Message);
            }
        }

        public async Task<CheckpointResult<string>> CreateCheckpointAsync(string message, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            var hash = await CreateCheckpointInternalAsync(message, ct);
            if (string.IsNullOrEmpty(hash))
            {
                return CheckpointResult<string>.Fail(CheckpointErrorType.VcsCommandFailed, "Failed to create checkpoint");
            }
            return CheckpointResult<string>.Ok(hash);
        }

        async Task<string> CreateCheckpointInternalAsync(string message, CancellationToken ct, Action<float, string> reportProgress = null)
        {
            if (!IsInitialized)
            {
                InternalLog.LogError("Repository not initialized");
                return null;
            }

            try
            {
                reportProgress?.Invoke(0.15f, "Saving assets");
                var saveResult = await SaveAllAssetsAsync();
                if (saveResult == AssetSaveResult.UserCancelled)
                {
                    InternalLog.Log("Checkpoint cancelled: user dismissed the save dialog");
                    throw new OperationCanceledException("User cancelled the scene save dialog");
                }
                if (saveResult == AssetSaveResult.Failed)
                {
                    InternalLog.LogError("Checkpoint aborted: asset save failed");
                    return null;
                }

                reportProgress?.Invoke(0.25f, "Staging files");
                var stageResult = await m_Adapter.StageAllAsync(ct);
                if (!stageResult.Success)
                {
                    InternalLog.LogError($"Failed to stage files: {stageResult.Error}");
                    return null;
                }

                reportProgress?.Invoke(0.3f, "Creating snapshot");
                var hasChanges = await m_Adapter.HasStagedChangesAsync(ct);
                var commitResult = await m_Adapter.CommitAsync(message, allowEmpty: !hasChanges, ct);
                if (!commitResult.Success)
                {
                    InternalLog.LogError($"Failed to commit: {commitResult.Error}");
                    return null;
                }

                var hash = await m_Adapter.GetHeadCommitHashAsync(ct);
                if (string.IsNullOrEmpty(hash))
                {
                    InternalLog.LogError("Failed to get commit hash");
                    return null;
                }

                InternalLog.Log($"Created checkpoint: {hash} - {message}");
                AssistantEvents.Send(new EventCheckpointsChanged());
                return hash;
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"Failed to create checkpoint: {ex.Message}");
                return null;
            }
        }

        public async Task<List<CheckpointInfo>> GetCheckpointsAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();

            var checkpoints = new List<CheckpointInfo>();

            if (!IsInitialized)
            {
                return checkpoints;
            }

            try
            {
                var initialCommitHash = await m_Adapter.GetCommitForTagAsync(k_InitialCommitTag, ct);
                var tagToCommit = await m_Adapter.GetTagToCommitMapAsync(k_TagPrefix, ct);
                var commitToTag = new Dictionary<string, (string ConversationId, string FragmentId)>(tagToCommit.Count);
                foreach (var kv in tagToCommit)
                {
                    var parsed = ParseTag(kv.Key);
                    if (parsed.HasValue && !commitToTag.ContainsKey(kv.Value))
                    {
                        commitToTag[kv.Value] = parsed.Value;
                    }
                }

                var commits = await m_Adapter.GetCommitHistoryAsync(ct);
                foreach (var commit in commits)
                {
                    if (!string.IsNullOrEmpty(initialCommitHash) &&
                        string.Equals(commit.Hash, initialCommitHash, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    commitToTag.TryGetValue(commit.Hash, out var ids);
                    checkpoints.Add(new CheckpointInfo(
                        commit.Hash,
                        commit.Message,
                        commit.TimestampUnixSeconds * 1000,
                        ids.ConversationId,
                        ids.FragmentId));
                }
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"Failed to get checkpoints: {ex.Message}");
            }

            return checkpoints;
        }

        public async Task<CheckpointInfo?> GetInitialCommitInfoAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (!IsInitialized)
            {
                return null;
            }

            try
            {
                var hash = await m_Adapter.GetCommitForTagAsync(k_InitialCommitTag, ct);
                if (string.IsNullOrEmpty(hash))
                {
                    return null;
                }

                var commits = await m_Adapter.GetCommitHistoryAsync(ct);
                foreach (var commit in commits)
                {
                    if (string.Equals(commit.Hash, hash, StringComparison.Ordinal))
                    {
                        return new CheckpointInfo(commit.Hash, commit.Message, commit.TimestampUnixSeconds * 1000);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"Failed to get initial commit info: {ex.Message}");
                return null;
            }
        }

        public async Task<CheckpointResult<bool>> RestoreCheckpointAsync(string commitHash, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (!IsInitialized)
            {
                return CheckpointResult<bool>.Fail(CheckpointErrorType.RepositoryMissing, "Checkpoints not initialized");
            }

            if (string.IsNullOrEmpty(commitHash))
            {
                return CheckpointResult<bool>.Fail(CheckpointErrorType.VcsCommandFailed, "Invalid commit hash");
            }

            var snapshotHash = await m_Adapter.GetHeadCommitHashAsync(ct);
            if (string.IsNullOrEmpty(snapshotHash))
            {
                return CheckpointResult<bool>.Fail(CheckpointErrorType.VcsCommandFailed,
                    "Restore aborted: cannot snapshot current state for rollback safety. Project unchanged.");
            }

            try
            {
                var saveResult = await SaveAllAssetsAsync();
                if (saveResult == AssetSaveResult.UserCancelled)
                {
                    return CheckpointResult<bool>.Fail(CheckpointErrorType.Cancelled,
                        "Restore cancelled: scene save dialog was dismissed. Project unchanged.");
                }
                if (saveResult == AssetSaveResult.Failed)
                {
                    return CheckpointResult<bool>.Fail(CheckpointErrorType.VcsCommandFailed,
                        "Restore aborted: asset save failed. Project unchanged.");
                }

                var resetResult = await m_Adapter.ResetHardAsync("HEAD", ct);
                if (!resetResult.Success)
                {
                    return await AttemptRollbackAsync(snapshotHash, ClassifyVcsError(resetResult), resetResult.Error, ct);
                }

                var preCleanResult = await m_Adapter.CleanUntrackedAsync(ct);
                if (!preCleanResult.Success)
                {
                    return await AttemptRollbackAsync(snapshotHash, ClassifyVcsError(preCleanResult), preCleanResult.Error, ct);
                }

                var checkoutResult = await m_Adapter.CheckoutFilesAsync(commitHash, ct);
                if (!checkoutResult.Success)
                {
                    return await AttemptRollbackAsync(snapshotHash, ClassifyVcsError(checkoutResult), checkoutResult.Error, ct);
                }

                var postCleanResult = await m_Adapter.CleanUntrackedAsync(ct);
                if (!postCleanResult.Success)
                {
                    return await AttemptRollbackAsync(snapshotHash, ClassifyVcsError(postCleanResult), postCleanResult.Error, ct);
                }

                InternalLog.Log($"Restored to checkpoint: {commitHash}");
                MainThread.DispatchAndForget(() => AssetDatabase.Refresh());
                return CheckpointResult<bool>.Ok(true, "Restored to checkpoint");
            }
            catch (OperationCanceledException)
            {
                return await AttemptRollbackAsync(snapshotHash, CheckpointErrorType.Cancelled, "Restore was cancelled", ct);
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"Failed to restore: {ex.Message}");
                return await AttemptRollbackAsync(snapshotHash, CheckpointErrorType.VcsCommandFailed, ex.Message, ct);
            }
        }

        async Task<CheckpointResult<bool>> AttemptRollbackAsync(string snapshotHash, CheckpointErrorType errorType, string originalError, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(snapshotHash))
            {
                return CheckpointResult<bool>.Fail(errorType,
                    "Restore failed AND rollback failed. Project may be in an inconsistent state. Manual intervention required.",
                    originalError);
            }

            try
            {
                var rollbackResult = await m_Adapter.ResetHardAsync(snapshotHash, ct);
                if (rollbackResult.Success)
                {
                    InternalLog.LogWarning($"Restore failed; rolled back to snapshot {snapshotHash}");
                    return CheckpointResult<bool>.Fail(errorType,
                        "Restore failed; project has been returned to the state of the last successful checkpoint. Files that were untracked before the restore attempt could not be recovered.",
                        originalError);
                }
            }
            catch (Exception rollbackEx)
            {
                InternalLog.LogError($"Rollback failed: {rollbackEx.Message}");
            }

            return CheckpointResult<bool>.Fail(errorType,
                "Restore failed AND rollback failed. Project may be in an inconsistent state. Manual intervention required.",
                originalError);
        }

        public async Task<IReadOnlyList<CheckpointFileChange>> GetRestoreChangesAsync(string targetCommitHash, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (!IsInitialized || string.IsNullOrEmpty(targetCommitHash))
            {
                return Array.Empty<CheckpointFileChange>();
            }

            try
            {
                return await m_Adapter.GetRestoreChangesAsync(targetCommitHash, ct);
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"Failed to compute restore diff: {ex.Message}");
                return Array.Empty<CheckpointFileChange>();
            }
        }

        public Task InitializeAnywayAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();
            m_InitialCommitVerified = true;
            m_LastVerificationMissingFiles.Clear();
            AssistantEvents.Send(new Events.EventCheckpointEnableStateChanged(true));
            return Task.CompletedTask;
        }

        public async Task<CheckpointResult<int>> VerifyInitialCommitAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (!(m_Adapter?.IsInitialized ?? false))
            {
                return CheckpointResult<int>.Fail(CheckpointErrorType.RepositoryMissing, "Checkpoints not initialized");
            }

            try
            {
                var commitHash = await m_Adapter.GetCommitForTagAsync(k_InitialCommitTag, ct);
                if (string.IsNullOrEmpty(commitHash))
                {
                    return CheckpointResult<int>.Fail(CheckpointErrorType.VcsCommandFailed, "Initial commit tag not found");
                }

                var missing = await ComputeInitialCommitMissingFilesAsync(commitHash, ct);
                if (missing.Count > 0)
                {
                    OnVerificationFailed(missing);
                }
                else
                {
                    m_InitialCommitVerified = true;
                    m_LastVerificationMissingFiles.Clear();
                    AssistantEvents.Send(new Events.EventCheckpointEnableStateChanged(true));
                }

                return CheckpointResult<int>.Ok(missing.Count);
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning($"Initial commit verification failed with exception: {ex.Message}");
                return CheckpointResult<int>.Fail(CheckpointErrorType.VcsCommandFailed, "Verification failed", ex.Message);
            }
        }

        async Task RunInitialCommitVerificationAsync(string commitHash, CancellationToken ct)
        {
            try
            {
                var missing = await ComputeInitialCommitMissingFilesAsync(commitHash, ct);
                if (missing.Count > 0)
                {
                    OnVerificationFailed(missing);
                }
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning($"Post-init verification failed with exception: {ex.Message}");
            }
        }

        async Task<List<string>> ComputeInitialCommitMissingFilesAsync(string commitHash, CancellationToken ct)
        {
            var trackedInGit = await m_Adapter.GetTrackedFilesAtCommitAsync(commitHash, ct);
            var gitSet = new HashSet<string>(trackedInGit, StringComparer.OrdinalIgnoreCase);

            return await Task.Run(() =>
            {
                var missing = new List<string>();
                foreach (var folder in GitAdapter.k_TrackedPaths)
                {
                    var folderPath = Path.Combine(m_ProjectPath, folder);
                    if (!Directory.Exists(folderPath)) continue;

                    foreach (var file in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
                    {
                        ct.ThrowIfCancellationRequested();
                        var rel = file.Substring(m_ProjectPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        rel = rel.Replace('\\', '/');
                        if (!gitSet.Contains(rel))
                        {
                            missing.Add(rel);
                        }
                    }
                }
                return missing;
            }, ct);
        }

        void OnVerificationFailed(List<string> missing)
        {
            m_InitialCommitVerified = false;
            m_LastVerificationMissingFiles.Clear();
            m_LastVerificationMissingFiles.AddRange(missing);
            InternalLog.LogWarning($"Post-init verification: {missing.Count} file(s) missing from initial checkpoint.");
            AssistantEvents.Send(new Events.EventCheckpointEnableStateChanged(false));
            MainThread.DispatchAndForget(() => EditorUtility.DisplayDialog(
                "Checkpoint Initialization Failed",
                "Some project files were not included in the initial checkpoint. Open AI Assistant settings to retry or override.",
                "OK"));
        }

        async Task TryDeleteGitDirAsync()
        {
            var gitDir = Path.Combine(m_RepoPath, ".git");
            if (!Directory.Exists(gitDir)) return;
            try
            {
                await Task.Run(() => DeleteDirectoryRecursive(gitDir));
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning($"Failed to clean up .git dir after failed init: {ex.Message}");
            }
        }

        public async Task<CheckpointResult<bool>> DeleteCheckpointAsync(string commitHash, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (!IsInitialized)
            {
                return CheckpointResult<bool>.Fail(CheckpointErrorType.RepositoryMissing, "Checkpoints not initialized");
            }

            if (string.IsNullOrEmpty(commitHash))
            {
                return CheckpointResult<bool>.Fail(CheckpointErrorType.VcsCommandFailed, "Invalid commit hash");
            }

            try
            {
                var tags = await m_Adapter.GetTagsAtCommitAsync(commitHash, ct);
                foreach (var tag in tags)
                {
                    if (!tag.StartsWith(k_TagPrefix))
                    {
                        continue;
                    }

                    await m_Adapter.DeleteTagAsync(tag, ct);

                    var parsed = ParseTag(tag);
                    if (parsed.HasValue)
                    {
                        var conversationId = new AssistantConversationId(parsed.Value.ConversationId);
                        RemoveFromTagsCache(conversationId, parsed.Value.FragmentId);
                    }
                }

                InternalLog.Log($"Deleted checkpoint tag for: {commitHash}");
                AssistantEvents.Send(new EventCheckpointsChanged());
                return CheckpointResult<bool>.Ok(true, "Deleted checkpoint");
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"Failed to delete: {ex.Message}");
                return CheckpointResult<bool>.Fail(CheckpointErrorType.VcsCommandFailed, "Failed to delete checkpoint", ex.Message);
            }
        }

        async Task TagCheckpointAsync(string commitHash, AssistantConversationId conversationId, string fragmentId, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (!IsInitialized || string.IsNullOrEmpty(commitHash))
            {
                return;
            }

            try
            {
                var tagName = BuildTagName(conversationId, fragmentId);
                var result = await m_Adapter.CreateTagAsync(tagName, commitHash, ct);

                if (result.Success)
                {
                    AddToTagsCache(conversationId, fragmentId);
                }
                else if (result.Error.Contains("already exists"))
                {
                    InternalLog.Log($"Tag {tagName} already exists, skipping");
                }
                else
                {
                    InternalLog.LogWarning($"Failed to create tag: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning($"Failed to tag checkpoint: {ex.Message}");
            }
        }

        public async Task UpdateTagAsync(string commitHash, AssistantConversationId conversationId, string oldFragmentId, string newFragmentId, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (!IsInitialized || string.IsNullOrEmpty(commitHash))
            {
                return;
            }

            try
            {
                var oldTagName = BuildTagName(conversationId, oldFragmentId);
                var newTagName = BuildTagName(conversationId, newFragmentId);

                await m_Adapter.DeleteTagAsync(oldTagName, ct);
                await m_Adapter.CreateTagAsync(newTagName, commitHash, ct);

                RemoveFromTagsCache(conversationId, oldFragmentId);
                AddToTagsCache(conversationId, newFragmentId);
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning($"Failed to update tag: {ex.Message}");
            }
        }

        public async Task<string> GetCheckpointForMessageAsync(AssistantConversationId conversationId, string fragmentId, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (!IsInitialized || conversationId == AssistantConversationId.Invalid || string.IsNullOrEmpty(fragmentId))
            {
                return null;
            }

            try
            {
                var tagName = BuildTagName(conversationId, fragmentId);
                return await m_Adapter.GetCommitForTagAsync(tagName, ct);
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"Failed to find checkpoint for message: {ex.Message}");
                return null;
            }
        }

        public async Task<CheckpointInfo?> GetCheckpointInfoForMessageAsync(AssistantConversationId conversationId, string fragmentId, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (!IsInitialized || conversationId == AssistantConversationId.Invalid || string.IsNullOrEmpty(fragmentId))
            {
                return null;
            }

            try
            {
                var tagName = BuildTagName(conversationId, fragmentId);
                var hash = await m_Adapter.GetCommitForTagAsync(tagName, ct);
                if (string.IsNullOrEmpty(hash))
                {
                    return null;
                }

                var commits = await m_Adapter.GetCommitHistoryAsync(ct);
                foreach (var commit in commits)
                {
                    if (string.Equals(commit.Hash, hash, StringComparison.Ordinal))
                    {
                        return new CheckpointInfo(commit.Hash, commit.Message, commit.TimestampUnixSeconds * 1000, conversationId.ToString(), fragmentId);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"Failed to get checkpoint info for message: {ex.Message}");
                return null;
            }
        }

        public bool HasCheckpointForMessage(AssistantConversationId conversationId, string fragmentId)
        {
            if (!IsInitialized || conversationId == AssistantConversationId.Invalid || string.IsNullOrEmpty(fragmentId))
            {
                return false;
            }

            var tagKey = BuildTagKey(conversationId, fragmentId);
            lock (m_CacheLock)
            {
                return m_CachedMessageTags.Contains(tagKey);
            }
        }

        public async Task RefreshTagsCacheAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (!IsInitialized)
            {
                return;
            }

            try
            {
                var tags = await m_Adapter.GetTagsWithPrefixAsync(k_TagPrefix, ct);

                lock (m_CacheLock)
                {
                    m_CachedMessageTags.Clear();

                    foreach (var tag in tags)
                    {
                        if (tag.StartsWith(k_TagPrefix))
                        {
                            var key = tag.Substring(k_TagPrefix.Length);
                            m_CachedMessageTags.Add(key);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning($"Failed to refresh tags cache: {ex.Message}");
            }
        }

        public void SetPendingCheckpoint(AssistantConversationId conversationId, string incompleteFragmentId, string checkpointHash)
        {
            var key = BuildTagKey(conversationId, incompleteFragmentId);
            lock (m_CacheLock)
            {
                m_PendingCheckpoints[key] = checkpointHash;
            }
        }

        public async Task CompletePendingCheckpointAsync(AssistantConversationId conversationId, string incompleteFragmentId, string realFragmentId, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            var key = BuildTagKey(conversationId, incompleteFragmentId);
            string checkpointHash;
            lock (m_CacheLock)
            {
                if (!m_PendingCheckpoints.TryGetValue(key, out checkpointHash))
                {
                    return;
                }
                m_PendingCheckpoints.Remove(key);
            }

            await TagCheckpointAsync(checkpointHash, conversationId, realFragmentId, ct);
            
            AssistantEvents.Send(new EventCheckpointsChanged());
        }

        public bool HasPendingCheckpoint(AssistantConversationId conversationId, string incompleteFragmentId)
        {
            var key = BuildTagKey(conversationId, incompleteFragmentId);
            lock (m_CacheLock)
            {
                return m_PendingCheckpoints.ContainsKey(key);
            }
        }

        public async Task<CheckpointResult<int>> DeleteOldCheckpointsAsync(int retentionWeeks, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (!IsInitialized)
            {
                return CheckpointResult<int>.Fail(CheckpointErrorType.RepositoryMissing, "Checkpoints not initialized");
            }

            if (retentionWeeks < 1)
            {
                return CheckpointResult<int>.Ok(0, "Invalid retention period");
            }

            try
            {
                var cutoffUnixSeconds = DateTimeOffset.UtcNow.AddDays(-retentionWeeks * 7).ToUnixTimeSeconds();

                var commits = await m_Adapter.GetCommitHistoryAsync(ct);
                var oldCommitHashes = commits
                    .Where(c => c.TimestampUnixSeconds < cutoffUnixSeconds)
                    .Select(c => c.Hash)
                    .ToHashSet();

                if (oldCommitHashes.Count == 0)
                {
                    return CheckpointResult<int>.Ok(0, "No old checkpoints");
                }

                var tagToCommit = await m_Adapter.GetTagToCommitMapAsync(k_TagPrefix, ct);
                var deletedCount = 0;

                foreach (var kv in tagToCommit)
                {
                    if (!oldCommitHashes.Contains(kv.Value))
                    {
                        continue;
                    }

                    await m_Adapter.DeleteTagAsync(kv.Key, ct);

                    var parsed = ParseTag(kv.Key);
                    if (parsed.HasValue)
                    {
                        var conversationId = new AssistantConversationId(parsed.Value.ConversationId);
                        RemoveFromTagsCache(conversationId, parsed.Value.FragmentId);
                    }

                    deletedCount++;
                }

                if (deletedCount > 0)
                {
                    InternalLog.Log($"Deleted {deletedCount} old checkpoint tags (older than {retentionWeeks} weeks)");

                    var pruneResult = await m_Adapter.PruneUnreferencedObjectsAsync(ct);
                    if (!pruneResult.Success)
                    {
                        InternalLog.LogWarning($"Failed to prune repository: {pruneResult.Error}");
                    }

                    AssistantEvents.Send(new EventCheckpointsChanged());
                }

                return CheckpointResult<int>.Ok(deletedCount, $"Deleted {deletedCount} old checkpoints");
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"Failed to delete old checkpoints: {ex.Message}");
                return CheckpointResult<int>.Fail(CheckpointErrorType.VcsCommandFailed, "Failed to delete old checkpoints", ex.Message);
            }
        }

        public async Task<CheckpointResult<bool>> ResetRepositoryAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();

            m_Adapter?.Dispose();
            m_Adapter = null;
            m_InitialCommitVerified = false;

            lock (m_CacheLock)
            {
                m_CachedMessageTags.Clear();
                m_PendingCheckpoints.Clear();
            }

            await DeleteRepositoryAsync();

            return await InitializeAsync(ct);
        }

        async Task DeleteRepositoryAsync()
        {
            if (!Directory.Exists(m_RepoPath))
            {
                return;
            }

            try
            {
                await Task.Run(() => DeleteDirectoryRecursive(m_RepoPath));
                InternalLog.Log("Repository directory deleted");
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"Failed to delete repository: {ex.Message}");
            }
        }

        static void DeleteDirectoryRecursive(string path)
        {
            var di = new DirectoryInfo(path);
            if (!di.Exists)
            {
                return;
            }

            foreach (var file in di.GetFiles("*", SearchOption.AllDirectories))
            {
                file.Attributes = FileAttributes.Normal;
            }

            di.Delete(true);
        }

        static (string ConversationId, string FragmentId)? ParseTag(string tag)
        {
            if (!tag.StartsWith(k_TagPrefix))
            {
                return null;
            }

            var content = tag.Substring(k_TagPrefix.Length);
            var parts = content.Split('-');

            if (parts.Length < 6)
            {
                return null;
            }

            // GUID has 5 parts (8-4-4-4-12 format)
            var convId = $"{parts[0]}-{parts[1]}-{parts[2]}-{parts[3]}-{parts[4]}";

            // FragmentId is everything after the GUID (may contain dashes)
            var fragId = string.Join("-", parts.Skip(5));
            return (convId, fragId);
        }

        static string BuildTagName(AssistantConversationId conversationId, string fragmentId)
        {
            return $"{k_TagPrefix}{conversationId}-{fragmentId}";
        }

        static string BuildTagKey(AssistantConversationId conversationId, string fragmentId)
        {
            return $"{conversationId}-{fragmentId}";
        }

        void AddToTagsCache(AssistantConversationId conversationId, string fragmentId)
        {
            var tagKey = BuildTagKey(conversationId, fragmentId);
            lock (m_CacheLock)
            {
                m_CachedMessageTags.Add(tagKey);
            }
        }

        void RemoveFromTagsCache(AssistantConversationId conversationId, string fragmentId)
        {
            var tagKey = BuildTagKey(conversationId, fragmentId);
            lock (m_CacheLock)
            {
                m_CachedMessageTags.Remove(tagKey);
            }
        }

        IVcsAdapter CreateAdapter()
        {
            return new GitAdapter(m_RepoPath, m_ProjectPath);
        }

        CheckpointErrorType ClassifyVcsError(VcsResult result)
        {
            return GitAdapter.ClassifyError(result);
        }

        public static async Task<AssetSaveResult> SaveAllAssetsAsync()
        {
            var tcs = new TaskCompletionSource<AssetSaveResult>();

            MainThread.DispatchAndForget(() =>
            {
                try
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        tcs.TrySetResult(AssetSaveResult.UserCancelled);
                        return;
                    }

                    AssetDatabase.SaveAssets();
                    tcs.TrySetResult(AssetSaveResult.Saved);
                }
                catch (Exception ex)
                {
                    InternalLog.LogWarning($"Failed to save assets: {ex.Message}");
                    tcs.TrySetResult(AssetSaveResult.Failed);
                }
            });

            return await tcs.Task;
        }

        void ThrowIfDisposed()
        {
            if (m_Disposed)
            {
                throw new ObjectDisposedException(nameof(CheckpointSystem));
            }
        }

        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;
            m_Adapter?.Dispose();
            m_Adapter = null;

            lock (m_CacheLock)
            {
                m_CachedMessageTags.Clear();
                m_PendingCheckpoints.Clear();
            }
        }
    }
}
