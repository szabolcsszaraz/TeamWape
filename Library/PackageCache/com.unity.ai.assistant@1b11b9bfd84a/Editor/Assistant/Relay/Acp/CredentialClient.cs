using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Utils;

namespace Unity.Relay.Editor
{
    /// <summary>
    /// Client for secure credential storage through the Relay server.
    /// Uses platform-native secure storage (macOS Keychain, Windows Credential Manager, Linux libsecret).
    ///
    /// Supports revealing, storing, and deleting credentials via the relay.
    /// </summary>
    class CredentialClient
    {
        static CredentialClient s_Instance;

        /// <summary>
        /// Gets the singleton instance of the CredentialClient.
        /// </summary>
        public static CredentialClient Instance => s_Instance ??= new();

        /// <summary>
        /// Reveal a credential value by reading directly from keytar (bypasses relay cache).
        /// User may need to interact with the OS keychain dialog, so no timeout is applied.
        /// Cancellation only happens when the relay disconnects (bus cancels all pending calls).
        /// </summary>
        /// <param name="agentType">The agent type (e.g., "gemini").</param>
        /// <param name="name">The credential name (e.g., "GEMINI_API_KEY").</param>
        /// <returns>The relay response containing Success, Value, and Error fields.</returns>
        public async Task<CredentialRevealResponse> RevealAsync(string agentType, string name)
        {
            try
            {
                return await RelayService.Instance.Bus.CallAsync(
                    RelayChannels.CredentialReveal,
                    new CredentialRevealRequest(agentType, name),
                    Timeout.Infinite);
            }
            catch (Exception ex) when (ex is RelayDisconnectedException or OperationCanceledException)
            {
                return new CredentialRevealResponse(false, Error: "Relay disconnected");
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[CredentialClient] Error revealing credential: {ex.Message}");
                return new CredentialRevealResponse(false, Error: ex.Message);
            }
        }

        /// <summary>
        /// Store a Credential in the OS keychain via the Relay.
        /// </summary>
        /// <param name="agentType"> The credential namespace (e.g., "figma"). </param>
        /// <param name="name"> The credential name (e.g., "FIGMA_API_TOKEN"). </param>
        /// <param name="value"> The credential value to store. </param>
        public async Task<CredentialSetResponse> SetAsync(string agentType, string name, string value)
        {
            try
            {
                return await RelayService.Instance.Bus.CallAsync(
                    RelayChannels.CredentialSet,
                    new CredentialSetRequest(agentType, name, value),
                    Timeout.Infinite);
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[CredentialClient] Error setting credential: {ex.Message}");
                return new CredentialSetResponse(false, Error: ex.Message);
            }
        }

        /// <summary>
        /// Delete a credential from the OS keychain via the Relay.
        /// </summary>
        /// <param name="agentType">The credential namespace (e.g., "figma"). </param>
        /// <param name="name">The credential name (e.g., "FIGMA_API_TOKEN").</param>
        public async Task<CredentialSetResponse> DeleteAsync(string agentType, string name)
        {
            return await SetAsync(agentType, name, string.Empty);
        }
    }
}
