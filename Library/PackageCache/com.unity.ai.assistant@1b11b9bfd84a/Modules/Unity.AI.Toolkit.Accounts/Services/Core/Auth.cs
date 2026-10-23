using System;
using System.Threading.Tasks;
using AiEditorToolsSdk.Domain.Abstractions.Services;
using AiEditorToolsSdk.Domain.Core.Results;
using Unity.AI.Toolkit.Connect;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Toolkit.Accounts.Services.Core
{
    class Auth : IUnityAuthenticationTokenProvider
    {
        const string k_UnityHubUriScheme = "unityhub://";
        const string k_UnityHubLoginDomain = "login";

        // Track the last time the URL was opened
        static DateTime s_LastUrlOpenTime = DateTime.MinValue;
        static readonly TimeSpan k_URLOpenCooldown = TimeSpan.FromMinutes(1);
        static bool s_LastStatus = true;

        // AccountApi builds a new Auth() per request and retries up to 4x per
        // call, so any per-request log here would repeat under a brownout.
        // These, like s_LastStatus, gate the two new messages so they emit on
        // transition instead of once per call.
        static bool s_LastBatchTokenAvailable = true;
        static bool s_LastRefreshAvailable = true;

        readonly int m_MainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

        string m_Token = UnityConnectProvider.accessToken;

        // A refresh result must never report success with an empty bearer — an
        // empty Ok() reaches the SDK as an authenticated request carrying no
        // token. Every non-timeout exit routes through here so a null or empty
        // token always fails instead. internal so the invariant is unit-tested
        // directly (see AuthTests) without a live Unity Connect session.
        internal static Result<string> ResultForToken(string token) =>
            string.IsNullOrEmpty(token) ? Result<string>.Fail() : Result<string>.Ok(token);

        // Prefer a freshly-read token, but only when it is usable: a null or empty
        // live read — transient right after a domain reload, or during an auth-
        // service brownout — must never overwrite a good cached token, or the
        // cached-token fallback in ForceRefreshToken (which keys off a non-empty
        // m_Token) can never fire. Both the batchmode injected-token guard and the
        // refresh path route through here so the invariant lives in one place.
        // internal so it is unit-tested directly (see AuthTests).
        internal static string PreferUsableToken(string cached, string live) =>
            string.IsNullOrEmpty(live) ? cached : live;

        // Read Application.isBatchMode defensively. The getter throws UnityException
        // in some editor states and off the main thread; an unreadable value must
        // resolve to batch/headless-safe (true), never interactive, so a headless
        // refresh does not pop unityhub://login and stall for the full timeout.
        // internal + Func seam so the throwing-read case is unit-tested directly
        // (see AuthTests) without a live editor.
        internal static bool IsBatchModeOrHeadlessSafe(Func<bool> readIsBatchMode)
        {
            try { return readIsBatchMode(); }
            catch { return true; }
        }

        public async Task<Result<string>> ForceRefreshToken()
        {
            // Application.isBatchMode throws UnityException in some editor states
            // (e.g. while serializing) and off the main thread — and this method is
            // reachable from a background thread, which is why the refresh below is
            // marshalled through EditorTask.RunOnMainThread. An unreadable value
            // resolves to batch/headless-safe (see IsBatchModeOrHeadlessSafe), never
            // interactive. This diverges deliberately from EditorTask.Yield's
            // identical guard, which defaults to false on throw: its fallback is a
            // harmless main-thread Delay, whereas the interactive path below pops
            // unityhub://login on a headless machine and stalls each refresh for the
            // full 30s timeout before the cached-token fallback returns the token.
            var batchMode = IsBatchModeOrHeadlessSafe(() => Application.isBatchMode);

            // In batchmode there is no Unity Hub to refresh through — the token
            // was injected at startup (benchmark/CI). Short-circuit to it:
            // attempting the refresh can only time out, and would pop
            // unityhub://login on a headless machine. This hardens the batchmode
            // guard the Generators SDK AuthenticationTokenProvider already has —
            // that one reads isBatchMode unguarded, never re-reads the injected
            // token, and can return Ok("") — so do not "simplify" this back to
            // its shape.
            if (batchMode)
            {
                // UnityConnectProvider.accessToken is transiently empty (or null)
                // right after a domain reload, before its cache is repopulated, and
                // its getter can throw — a UnityException while the editor is
                // serializing, or a non-UnityException out of the CloudProjectSettings
                // reads in MergeWithLatestPartialData, which UpdateCache does not
                // catch. Guard the whole read (not just UnityException) so nothing
                // escapes to the SDK, and take the live value only when it is usable
                // so a good cached token is never overwritten with an empty one.
                try
                {
                    m_Token = PreferUsableToken(m_Token, UnityConnectProvider.accessToken);
                }
                catch { /* fall through to the empty-check-then-Fail() below */ }

                // Batchmode short-circuits before the interactive path's timeout
                // warnings, so without this a token-less session fails to
                // authenticate with no log at all — and in a headless editor the
                // log is the only diagnostic surface an operator has.
                var batchTokenAvailable = !string.IsNullOrEmpty(m_Token);
                if (!batchTokenAvailable && s_LastBatchTokenAvailable)
                    Debug.LogError("[Auth] Batch mode: no access token available (no token was injected at startup and the Unity Connect cache is empty). The session cannot authenticate.");
                s_LastBatchTokenAvailable = batchTokenAvailable;

                return ResultForToken(m_Token);
            }

            try
            {
                // A single refresh attempt only. The caller (AccountApi.Request)
                // already owns retry — it builds a fresh Auth() per attempt and
                // walks a 2/4/8s (4/8/16/32s on reconnect) ladder — and it cannot
                // cancel a refresh already in flight, so a second inner retry loop
                // here would stack up to 8 un-cancellable 30s refreshes per user
                // action. Keep only the cached-token fallback below.
                var result = await EditorTask.RunOnMainThread(async () => await ForceRefreshTokenInternal());
                if (result.IsSuccessful)
                {
                    s_LastRefreshAvailable = true;
                    return result;
                }

                // Refresh failed or timed out (auth-service brownout). A present-but-
                // stale token beats Fail(): downstream keeps working until real
                // expiry, and an expired token surfaces as a visible 401 instead
                // of a silently wedged session (2026-08-17: refresh-timeout
                // storms after domain reloads left editors deaf mid-case).
                if (!string.IsNullOrEmpty(m_Token))
                {
                    if (s_LastRefreshAvailable)
                        Debug.LogWarning("[Auth] Token refresh unavailable; continuing with the cached access token.");
                    s_LastRefreshAvailable = false;
                }
                return ResultForToken(m_Token);
            }
            catch
            {
                return ResultForToken(m_Token);
            }
        }

        async Task<Result<string>> ForceRefreshTokenInternal()
        {
            try
            {
                if (System.Threading.Thread.CurrentThread.ManagedThreadId != m_MainThreadId)
                    throw new InvalidOperationException("ForceRefreshTokenInternal must be called from the main thread.");

                var tcs = new TaskCompletionSource<bool>();
                CloudProjectSettings.RefreshAccessToken(callbackStatus => tcs.TrySetResult(callbackStatus));

                // Check if enough time has passed since the last URL open
                var currentTime = DateTime.Now;
                if (currentTime - s_LastUrlOpenTime >= k_URLOpenCooldown)
                {
                    // Open URL and update the timestamp
                    Application.OpenURL($"{k_UnityHubUriScheme}{k_UnityHubLoginDomain}");
                    s_LastUrlOpenTime = currentTime;
                }

                const int timeoutSeconds = 30;
                var completedTask = await Task.WhenAny(tcs.Task, EditorTask.Delay((int)TimeSpan.FromSeconds(timeoutSeconds).TotalMilliseconds));
                if (completedTask == tcs.Task)
                {
                    // Take the refreshed value only when it is usable. During an
                    // auth-service brownout the callback can complete with
                    // status:false and an empty (or null) accessToken; overwriting
                    // m_Token unconditionally here would wipe a good cached token,
                    // and the caller's cached-token fallback keys off a non-empty
                    // m_Token, so the fallback would never fire. PreferUsableToken
                    // keeps the cached value; ResultForToken below fails the empty case.
                    m_Token = PreferUsableToken(m_Token, UnityConnectProvider.accessToken);

                    var status = await tcs.Task;
                    if (status)
                        Debug.Log("Access token refreshed successfully.");
                    else if (s_LastStatus)
                        Debug.LogError("Token refresh failed or was not needed.");

                    s_LastStatus = status;
                    // A callback status of true with an empty token is still an
                    // empty bearer — ResultForToken fails it rather than reporting
                    // a successful refresh with no authorization value.
                    return ResultForToken(m_Token);
                }

                Debug.LogWarning($"Token refresh timed out after {timeoutSeconds} seconds.");
                return Result<string>.Fail();
            }
            catch
            {
                return ResultForToken(m_Token);
            }
        }

        public async Task<Result<string>> GetToken()
        {
            try
            {
                return await EditorTask.RunOnMainThread(
                    () => Task.FromResult(Result<string>.Ok(m_Token = UnityConnectProvider.accessToken)));
            }
            catch
            {
                return Result<string>.Ok(m_Token);
            }
        }
    }
}
