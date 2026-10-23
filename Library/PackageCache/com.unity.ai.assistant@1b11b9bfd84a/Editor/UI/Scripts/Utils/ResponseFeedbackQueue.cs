using System;
using System.Collections.Generic;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Utils.Perf;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Utils
{
    internal class ResponseFeedbackQueue
    {
        struct QueueEntry
        {
            public AssistantMessageId Id;
        }

        readonly AssistantUIContext m_Context;
        readonly Queue<QueueEntry> m_RefreshQueue = new();

        double m_LastUpdateTime;

        public event Action<AssistantMessageId, FeedbackData?> LoadedFeedback;

        public ResponseFeedbackQueue(AssistantUIContext context)
        {
            m_Context = context;
            m_LastUpdateTime = Time.realtimeSinceStartup;
        }

        public void QueueRefresh(AssistantMessageId id)
        {
            if (m_RefreshQueue.Count == 0)
            {
                EditorApplication.update += Update;
            }

            m_RefreshQueue.Enqueue(new QueueEntry
            {
                Id = id
            });

            AssistantPerf.SetGauge(PerfGauge.FeedbackQueueDepth, m_RefreshQueue.Count);
        }

        public void Clear()
        {
            m_RefreshQueue.Clear();
            m_LastUpdateTime = Time.realtimeSinceStartup;

            AssistantPerf.SetGauge(PerfGauge.FeedbackQueueDepth, m_RefreshQueue.Count);
        }

        void Update()
        {
            if (m_RefreshQueue.Count == 0)
            {
                EditorApplication.update -= Update;
                return;
            }

            var currentTime = Time.realtimeSinceStartup;
            if (currentTime - m_LastUpdateTime < 0.2f)
                return;

            m_LastUpdateTime = currentTime;

            var entry = m_RefreshQueue.Dequeue();
            AssistantPerf.SetGauge(PerfGauge.FeedbackQueueDepth, m_RefreshQueue.Count);

            m_Context.API.FeedbackLoaded += OnFeedbackLoaded;

            using (AssistantPerf.Measure(PerfProbe.FeedbackLoadDispatch))
            {
                m_Context.API.LoadFeedback(entry.Id);
            }

            if (AssistantPerf.Enabled)
                AssistantPerf.SetGauge(PerfGauge.FeedbackSubscribers, m_Context.API.FeedbackLoadedSubscriberCount);

            void OnFeedbackLoaded(AssistantMessageId id, FeedbackData? data)
            {
                if (id == entry.Id)
                {
                    LoadedFeedback?.Invoke(id, data);
                    m_Context.API.FeedbackLoaded -= OnFeedbackLoaded;
                }
            }
        }
    }
}
