using System;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.AI.Tracing;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.AI.Assistant.Utils.Perf
{
    // Temporary diagnostic instrumentation for UUM-146077.
    static class AssistantPerfRecorder
    {
        const string k_FileName = "assistant-perf.jsonl";
        const int k_LinesPerFlush = 32;

        internal const double FlushIntervalSeconds = 1.0;

        static readonly object k_Lock = new();
        static readonly StringBuilder k_EventBuilder = new(512);
        static readonly long k_FlushIntervalTicks = (long)(System.Diagnostics.Stopwatch.Frequency * FlushIntervalSeconds);

        static StreamWriter s_Writer;
        static int s_LinesSinceFlush;
        static long s_LastFlushTicks;
        static string s_DirectoryOverride;

        public static string FilePath => Path.Combine(LogDirectory, k_FileName);

        static string LogDirectory => s_DirectoryOverride ?? TraceLogDir.LogDir;

        // Test seam: keeps a suite's own writes out of the log an investigator reads.
        internal static void SetDirectoryOverride(string directory)
        {
            lock (k_Lock)
            {
                Close();
                s_DirectoryOverride = directory;
            }
        }

        public static void Open()
        {
            lock (k_Lock)
            {
                if (s_Writer != null)
                    return;

                try
                {
                    Directory.CreateDirectory(LogDirectory);
                    var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.Read, 65536);
                    s_Writer = new StreamWriter(stream) { AutoFlush = false };
                    s_LastFlushTicks = AssistantPerf.Now;
                }
                catch (Exception exception)
                {
                    Debug.LogError("[AssistantPerf] Could not open " + FilePath + ": " + exception.Message);
                    s_Writer = null;
                    return;
                }
            }

            WriteSessionHeader();
        }

        public static void Close()
        {
            lock (k_Lock)
            {
                if (s_Writer == null)
                    return;

                try
                {
                    s_Writer.Flush();
                    s_Writer.Dispose();
                }
                catch (Exception exception)
                {
                    Debug.LogError("[AssistantPerf] Could not close " + FilePath + ": " + exception.Message);
                }

                s_Writer = null;
                s_LinesSinceFlush = 0;
            }
        }

        public static void WriteLine(string line)
        {
            lock (k_Lock)
            {
                if (s_Writer == null)
                    return;

                try
                {
                    s_Writer.WriteLine(line);
                    s_LinesSinceFlush++;

                    var now = AssistantPerf.Now;

                    // A hang investigation ends with the Editor killed, so a buffered line is a lost line.
                    if (s_LinesSinceFlush < k_LinesPerFlush && now - s_LastFlushTicks < k_FlushIntervalTicks)
                        return;

                    s_Writer.Flush();
                    s_LinesSinceFlush = 0;
                    s_LastFlushTicks = now;
                }
                catch (Exception exception)
                {
                    // Per-item markers write hundreds of events a second, so a writer that failed once is
                    // dropped rather than retried: a full disk costs one report, not one per event.
                    Debug.LogError("[AssistantPerf] Write failed, recording stopped: " + exception.Message);
                    Close();
                }
            }
        }

        public static void Flush()
        {
            lock (k_Lock)
            {
                if (s_Writer == null)
                    return;

                try
                {
                    s_Writer.Flush();
                    s_LinesSinceFlush = 0;
                    s_LastFlushTicks = AssistantPerf.Now;
                }
                catch (Exception exception)
                {
                    Debug.LogError("[AssistantPerf] Flush failed: " + exception.Message);
                }
            }
        }

        public static void WriteEvent(string name, string detail, double elapsedMs)
        {
            string line;
            lock (k_EventBuilder)
            {
                k_EventBuilder.Clear();
                k_EventBuilder.Append("{\"kind\":\"event\",\"ts\":\"").Append(Timestamp()).Append('"');

#if UNITY_EDITOR
                k_EventBuilder.Append(",\"frame\":").Append(AssistantPerfSampler.LastFrame.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"playing\":").Append(AssistantPerfSampler.LastPlaying ? "true" : "false");
#endif

                k_EventBuilder.Append(",\"name\":\"").Append(Escape(name)).Append('"');

                if (detail != null)
                {
                    k_EventBuilder.Append(",\"detail\":\"").Append(Escape(detail)).Append('"');
                }

                if (elapsedMs >= 0)
                {
                    k_EventBuilder.Append(",\"ms\":").Append(elapsedMs.ToString("F3", CultureInfo.InvariantCulture));
                }

                k_EventBuilder.Append('}');
                line = k_EventBuilder.ToString();
            }

            WriteLine(line);
        }

        public static string Timestamp() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        public static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            var escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
            for (var i = 0; i < escaped.Length; i++)
            {
                if (escaped[i] >= ' ')
                    continue;

                return EscapeRemainingControlChars(escaped);
            }

            return escaped;
        }

        static string EscapeRemainingControlChars(string value)
        {
            var builder = new StringBuilder(value.Length + 8);
            foreach (var c in value)
            {
                if (c >= ' ')
                    builder.Append(c);
                else
                    builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        static void WriteSessionHeader()
        {
            var builder = new StringBuilder(512);
            builder.Append("{\"kind\":\"session\",\"ts\":\"").Append(Timestamp()).Append('"')
                .Append(",\"session\":\"").Append(Guid.NewGuid().ToString("N")).Append('"')
                .Append(",\"unity\":\"").Append(Escape(Application.unityVersion)).Append('"')
                .Append(",\"stopwatchFrequency\":").Append(System.Diagnostics.Stopwatch.Frequency.ToString(CultureInfo.InvariantCulture));

#if ASSISTANT_INTERNAL
            builder.Append(",\"assistantInternal\":true");
#else
            builder.Append(",\"assistantInternal\":false");
#endif

#if UNITY_EDITOR
            builder.Append(",\"enterPlayModeOptionsEnabled\":").Append(EditorSettings.enterPlayModeOptionsEnabled ? "true" : "false")
                .Append(",\"reloadDomainDisabled\":")
                .Append((EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0 ? "true" : "false");
#endif

            builder.Append('}');
            WriteLine(builder.ToString());
            Flush();
        }
    }
}
