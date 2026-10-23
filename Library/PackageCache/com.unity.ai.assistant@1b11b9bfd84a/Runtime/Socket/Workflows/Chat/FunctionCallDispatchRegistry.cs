using System;
using System.Collections.Generic;
using Unity.AI.Assistant.Backend;

namespace Unity.AI.Assistant.Socket.Workflows.Chat
{
    /// <summary>
    /// Records which <c>FUNCTION_CALL_REQUEST_V1</c> call ids have already been handed to a function
    /// caller, so exactly one of the possible delivery paths executes each call, and remembers the
    /// settled result of one whose response could not be delivered so a redelivery can answer it.
    /// <para>
    /// The relay can deliver the same cached request more than once: its auto-recovery safety net
    /// replays pending calls when the client never asks for recovery, and a late
    /// <c>RELAY_RECOVER_MESSAGES</c> replays them again. A call leaves the relay's pending set only
    /// when its response arrives, so one that is still executing is a candidate for every path.
    /// </para>
    /// <para>
    /// The claim has to be shared rather than per-workflow because the consumers are not all
    /// workflows and not all long-lived. A <see cref="BaseChatWorkflow"/> claims a call when it
    /// dispatches one; the relay's standalone fallback claims a redelivered call that no workflow
    /// consumed (muse-editor#2705). A per-instance guard cannot stop those two from running the same
    /// tool twice, nor stop a freshly created recovery workflow — whose own guard starts empty —
    /// from re-running a call the fallback already answered.
    /// </para>
    /// <para>
    /// The first claimer wins; a call that no path ever claimed (e.g. it arrived before any workflow
    /// was subscribed to the transport) stays unclaimed, so a later replay of it still runs. That is
    /// what makes the relay's resend safe rather than merely redundant.
    /// </para>
    /// <para>
    /// A claim is never released, which is correct — a call that ran but whose response was dropped
    /// (its workflow closed mid-flight, or the send was rejected) stays pending on the relay, and
    /// un-claiming it would let a redelivery run the tool a second time. To make such a call
    /// answerable without re-running it, the settled result is recorded alongside the claim by the
    /// send path when a delivery fails (<see cref="RecordResult"/>); the standalone fallback then
    /// re-sends the cached result on redelivery (<see cref="TryGetResult"/>). A result is recorded
    /// only when a send was dropped, so the healthy path — where the response is delivered — leaves
    /// none, and nothing is ever re-sent for a call that was already answered.
    /// </para>
    /// </summary>
    static class FunctionCallDispatchRegistry
    {
        /// <summary>
        /// Upper bound on remembered call ids. A conversation answers each call as it goes, so the
        /// live set is tiny; the cap only exists so a very long session cannot grow this without
        /// limit. Evicting the oldest entry is safe: a call old enough to fall out has long since
        /// been answered, and the relay only ever replays calls that are still pending.
        /// </summary>
        internal const int MaxTrackedCallIds = 256;

        static readonly object s_Lock = new();

        // Value is null while the call is still executing, and the settled result once one has been
        // recorded for a call whose response could not be delivered.
        static readonly Dictionary<Guid, FunctionCallResult?> s_Claimed = new();
        static readonly Queue<Guid> s_ClaimOrder = new();

        /// <summary>
        /// Takes ownership of dispatching <paramref name="callId"/>, if nobody else has.
        /// </summary>
        /// <param name="callId">The call id from the request being dispatched.</param>
        /// <returns>
        /// True when the caller now owns this call and must execute it; false when another path
        /// already claimed it and this copy must be dropped.
        /// </returns>
        /// <remarks>
        /// <see cref="Guid.Empty"/> is never recorded. It is not a call id the backend can produce
        /// (<c>call_id</c> is required on the wire), so treating it as one would make unrelated
        /// calls collide with each other. It is always granted, leaving such a request to whatever
        /// path received it.
        /// </remarks>
        internal static bool TryClaim(Guid callId)
        {
            if (callId == Guid.Empty)
                return true;

            lock (s_Lock)
            {
                if (s_Claimed.ContainsKey(callId))
                    return false;

                s_Claimed[callId] = null;
                s_ClaimOrder.Enqueue(callId);
                while (s_ClaimOrder.Count > MaxTrackedCallIds)
                    s_Claimed.Remove(s_ClaimOrder.Dequeue());

                return true;
            }
        }

        /// <summary>
        /// True when some path has already claimed <paramref name="callId"/>. Used by the relay
        /// fallback to stay out of the way of a workflow that took the call.
        /// </summary>
        internal static bool IsClaimed(Guid callId)
        {
            if (callId == Guid.Empty)
                return false;

            lock (s_Lock)
                return s_Claimed.ContainsKey(callId);
        }

        /// <summary>
        /// Records the settled <paramref name="result"/> of an already-claimed call whose response
        /// could not be delivered, so a redelivery of the still-pending call can be answered by
        /// re-sending it instead of running the tool again.
        /// <para>
        /// A no-op for a call that is not currently tracked — <see cref="Guid.Empty"/>, or one that
        /// has already been evicted. This must only be called for a send that failed to reach the
        /// relay; recording a delivered response would make the fallback re-send a call that was
        /// already answered.
        /// </para>
        /// </summary>
        internal static void RecordResult(Guid callId, FunctionCallResult result)
        {
            if (callId == Guid.Empty)
                return;

            lock (s_Lock)
            {
                if (s_Claimed.ContainsKey(callId))
                    s_Claimed[callId] = result;
            }
        }

        /// <summary>
        /// Returns the settled result recorded for a claimed call, if any. False while the call is
        /// still executing (claimed, no result yet), when its response was delivered normally (no
        /// result recorded), or when it was never claimed.
        /// </summary>
        internal static bool TryGetResult(Guid callId, out FunctionCallResult result)
        {
            result = default;
            if (callId == Guid.Empty)
                return false;

            lock (s_Lock)
            {
                if (s_Claimed.TryGetValue(callId, out var stored) && stored.HasValue)
                {
                    result = stored.Value;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Forgets every claim and recorded result, so a test starts from a known state.
        /// <para>
        /// Nothing in the editor calls this, and nothing should: call ids are GUIDs, so a stale
        /// entry can never collide with a new call, and <see cref="MaxTrackedCallIds"/> already caps
        /// the set. Releasing a claim is the one way this type can produce a wrong answer — a call
        /// that has run but could not be answered stays pending on the relay, and un-claiming it
        /// would let the redelivery run the tool again.
        /// </para>
        /// </summary>
        internal static void Clear()
        {
            lock (s_Lock)
            {
                s_Claimed.Clear();
                s_ClaimOrder.Clear();
            }
        }
    }
}
