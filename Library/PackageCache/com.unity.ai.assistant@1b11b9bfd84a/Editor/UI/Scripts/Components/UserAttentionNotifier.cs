using System;
using System.Runtime.InteropServices;
using Unity.AI.Assistant.Editor.Utils.Event;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.UserInteraction;
using Unity.AI.Assistant.UI.Editor.Scripts.Events;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    static class UserAttentionNotifier
    {
        static UserInteractionQueue s_Queue;
        static BaseEventSubscriptionTicket s_Subscription;

        public static void Register(UserInteractionQueue queue)
        {
            if (queue == null)
                return;

            Unregister();

            s_Queue = queue;
            s_Subscription = AssistantEvents.Subscribe<EventInteractionQueueChanged>(OnQueueChanged);
        }

        public static void Unregister()
        {
            AssistantEvents.Unsubscribe(ref s_Subscription);
            s_Queue = null;
        }

        static void OnQueueChanged(EventInteractionQueueChanged evt)
        {
            if (s_Queue == null || !s_Queue.HasPending)
                return;

            RequestAttention();
        }

        static void RequestAttention()
        {
#if UNITY_EDITOR_WIN
            RequestAttentionWindows();
#elif UNITY_EDITOR_OSX
            RequestAttentionMac();
#endif
        }

#if UNITY_EDITOR_WIN
        const uint k_FlashwTray = 2;

        [StructLayout(LayoutKind.Sequential)]
        struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        [DllImport("user32.dll")]
        static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        static void RequestAttentionWindows()
        {
            try
            {
                var hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                if (hwnd == IntPtr.Zero)
                    return;

                var info = new FLASHWINFO
                {
                    cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
                    hwnd = hwnd,
                    dwFlags = k_FlashwTray,
                    uCount = 3,
                    dwTimeout = 0
                };
                FlashWindowEx(ref info);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UserAttentionNotifier] Windows flash failed: {ex.Message}");
            }
        }
#endif

#if UNITY_EDITOR_OSX
        const string k_ObjcLib = "/usr/lib/libobjc.dylib";
        const long k_NSCriticalRequest = 0;

        [DllImport(k_ObjcLib)]
        static extern IntPtr objc_getClass(string className);

        [DllImport(k_ObjcLib)]
        static extern IntPtr sel_registerName(string selectorName);

        [DllImport(k_ObjcLib, EntryPoint = "objc_msgSend")]
        static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

        [DllImport(k_ObjcLib, EntryPoint = "objc_msgSend")]
        static extern IntPtr objc_msgSend_Int64(IntPtr receiver, IntPtr selector, long arg);

        static void RequestAttentionMac()
        {
            try
            {
                var nsApp = objc_getClass("NSApplication");
                if (nsApp == IntPtr.Zero)
                    return;

                var sharedApp = objc_msgSend(nsApp, sel_registerName("sharedApplication"));
                if (sharedApp == IntPtr.Zero)
                    return;

                objc_msgSend_Int64(sharedApp, sel_registerName("requestUserAttention:"), k_NSCriticalRequest);
            }
            catch
            {
                // Best-effort — do not disrupt the editor if native call fails.
            }
        }
#endif
    }
}
