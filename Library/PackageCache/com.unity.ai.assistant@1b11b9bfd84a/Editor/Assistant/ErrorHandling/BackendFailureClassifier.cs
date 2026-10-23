using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Unity.Ai.Assistant.Protocol.Client;
using Unity.AI.Assistant.Socket.ErrorHandling;

namespace Unity.AI.Assistant.Editor
{
    /// <summary>
    /// Distinguishes a transient transport failure (network down / timeout / socket closed —
    /// typical right after the machine resumes from sleep) from a real backend error. Transient
    /// failures recover on their own once connectivity returns, so callers can suppress them
    /// instead of surfacing them as console errors.
    /// </summary>
    static class BackendFailureClassifier
    {
        public static bool IsTransientTransportFailure(BackendResult result)
        {
            if (result == null)
                return false;

            if (result.Status != BackendResult.ResultStatus.FailOnException)
                return false;

            var ex = result.Exception;
            return ex is ConnectionException
                || ex is SocketException
                || ex is IOException
                || ex is TimeoutException
                || ex is WebException
                || ex is HttpRequestException;
        }
    }
}
