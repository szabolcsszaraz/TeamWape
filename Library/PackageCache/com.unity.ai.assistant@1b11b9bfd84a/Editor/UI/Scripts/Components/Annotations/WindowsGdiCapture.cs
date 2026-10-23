using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Utils;
// ReSharper disable InconsistentNaming

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
#if UNITY_EDITOR_WIN
    /// <summary>
    /// Windows-specific screen capture implementation using GDI.
    /// </summary>
    static class WindowsGdiCapture
    {
        const uint k_DwmwaExtendedFrameBounds = 9;

        static readonly uint s_CurrentPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        // System metrics
        const int k_SmXVirtualScreen = 76;
        const int k_SmYVirtualScreen = 77;
        const int k_SmCxVirtualScreen = 78;
        const int k_SmCyVirtualScreen = 79;

        // Raster operation
        const int k_SrcCopy = 0x00CC0020;

        // Monitor constants
        const int k_MonitorDefaultToPrimary = 1;

        public static byte[] CaptureVirtualScreenRGBA(out int width, out int height)
        {
            var x = GetSystemMetrics(k_SmXVirtualScreen);
            var y = GetSystemMetrics(k_SmYVirtualScreen);
            width = GetSystemMetrics(k_SmCxVirtualScreen);
            height = GetSystemMetrics(k_SmCyVirtualScreen);
            return CaptureRegionFromScreenRGBA(x, y, width, height);
        }

        /// <summary>
        /// Capture the full virtual screen (all monitors) and report its origin so callers
        /// can map screen coordinates into the returned buffer.
        /// </summary>
        public static byte[] CaptureVirtualScreenWithOrigin(out int left, out int top, out int width, out int height)
        {
            left   = GetSystemMetrics(k_SmXVirtualScreen);
            top    = GetSystemMetrics(k_SmYVirtualScreen);
            width  = GetSystemMetrics(k_SmCxVirtualScreen);
            height = GetSystemMetrics(k_SmCyVirtualScreen);
            return CaptureRegionFromScreenRGBA(left, top, width, height);
        }

        public static byte[] CaptureMainMonitorRGBA(out int width, out int height)
        {
            // Get the Unity Editor main window
            IntPtr hwnd = FindMainWindowForCurrentProcess();
            if (hwnd == IntPtr.Zero)
            {
                // Fallback to capturing the entire virtual screen if we can't find the window
                InternalLog.LogWarning("[EditScreenCapture] Could not find Unity Editor window, falling back to full screen capture");
                return CaptureVirtualScreenRGBA(out width, out height);
            }

            // Get the monitor that contains the Unity Editor window
            IntPtr hMonitor = MonitorFromWindow(hwnd, k_MonitorDefaultToPrimary);
            if (hMonitor == IntPtr.Zero)
            {
                // Fallback if monitor detection fails
                InternalLog.LogWarning("[EditScreenCapture] Could not detect monitor, falling back to full screen capture");
                return CaptureVirtualScreenRGBA(out width, out height);
            }

            // Get the monitor info (bounds)
            var monitorInfo = new MonitorInfo();
            monitorInfo.cbSize = (uint)Marshal.SizeOf<MonitorInfo>();

            if (!GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                // Fallback if we can't get monitor info
                InternalLog.LogWarning("[EditScreenCapture] Could not get monitor info, falling back to full screen capture");
                return CaptureVirtualScreenRGBA(out width, out height);
            }

            // Use the monitor's work area bounds
            Rect bounds = monitorInfo.rcMonitor;
            int x = bounds.Left;
            int y = bounds.Top;
            width = Math.Max(1, bounds.Right - bounds.Left);
            height = Math.Max(1, bounds.Bottom - bounds.Top);

            return CaptureRegionFromScreenRGBA(x, y, width, height);
        }

        /// <summary>
        /// Enumerate all visible top-level windows owned by the current Unity Editor process,
        /// returned in back-to-front z-order (bottom-most first, topmost last).
        /// EnumWindows yields windows top-to-bottom; we collect then reverse.
        /// The returned rect is the DWM extended frame bounds — the window's visible rect
        /// excluding the drop-shadow padding that GetWindowRect includes on Win 10+.
        /// </summary>
        public static List<(IntPtr hwnd, Rect rect)> EnumerateUnityWindowsBackToFront()
        {
            var collected = new List<(IntPtr hwnd, Rect rect)>();

            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd)) return true;

                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid != s_CurrentPid) return true;

                if (GetWindowTextLength(hWnd) <= 0) return true;

                if (!TryGetVisualBounds(hWnd, out Rect r)) return true;

                int w = r.Right - r.Left;
                int h = r.Bottom - r.Top;
                if (w <= 1 || h <= 1) return true;

                collected.Add((hWnd, r));
                return true;
            }, IntPtr.Zero);

            collected.Reverse();
            return collected;
        }

        static bool TryGetVisualBounds(IntPtr hwnd, out Rect rect)
        {
            int hr = DwmGetWindowAttribute(hwnd, k_DwmwaExtendedFrameBounds, out rect, Marshal.SizeOf<Rect>());
            if (hr == 0)
                return true;
            // Fallback: legacy GetWindowRect (will include shadow padding on Win 10+).
            return GetWindowRect(hwnd, out rect);
        }

        static byte[] CaptureRegionFromScreenRGBA(int x, int y, int width, int height)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero) throw new Exception("GetDC failed.");

            IntPtr memDc = CreateCompatibleDC(screenDc);
            if (memDc == IntPtr.Zero)
            {
                ReleaseDC(IntPtr.Zero, screenDc);
                throw new Exception("CreateCompatibleDC failed.");
            }

            IntPtr hBitmap = CreateCompatibleBitmap(screenDc, width, height);
            if (hBitmap == IntPtr.Zero)
            {
                DeleteDC(memDc);
                ReleaseDC(IntPtr.Zero, screenDc);
                throw new Exception("CreateCompatibleBitmap failed.");
            }

            IntPtr oldObj = SelectObject(memDc, hBitmap);

            try
            {
                if (!BitBlt(memDc, 0, 0, width, height, screenDc, x, y, k_SrcCopy))
                    throw new Exception("BitBlt failed.");

                // 32bpp BGRA bottom-up DIB
                var bmi = new BitmapInfo();
                bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<BitmapInfoHeader>();
                bmi.bmiHeader.biWidth = width;
                bmi.bmiHeader.biHeight = height;
                bmi.bmiHeader.biPlanes = 1;
                bmi.bmiHeader.biBitCount = 32;
                bmi.bmiHeader.biCompression = 0; // BI_RGB

                int byteCount = width * height * 4;
                byte[] bgra = new byte[byteCount];

                int scanLines = GetDIBits(memDc, hBitmap, 0, (uint)height, bgra, ref bmi, 0);
                if (scanLines == 0)
                    throw new Exception("GetDIBits failed.");

                // Convert BGRA -> RGBA and force alpha=255
                BgraToRgbaInPlace(bgra);
                ForceAlphaOpaque(bgra);
                return bgra;
            }
            finally
            {
                SelectObject(memDc, oldObj);
                DeleteObject(hBitmap);
                DeleteDC(memDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        static void BgraToRgbaInPlace(byte[] data)
        {
            for (int i = 0; i < data.Length; i += 4)
            {
                byte b = data[i + 0];
                byte r = data[i + 2];
                data[i + 0] = r;
                data[i + 2] = b;
            }
        }

        static void ForceAlphaOpaque(byte[] data)
        {
            for (int i = 0; i < data.Length; i += 4)
                data[i + 3] = 255;
        }

        static IntPtr FindMainWindowForCurrentProcess()
        {
            IntPtr best = IntPtr.Zero;
            int bestArea = 0;

            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd)) return true;

                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid != s_CurrentPid) return true;

                int len = GetWindowTextLength(hWnd);
                if (len <= 0) return true;

                if (GetWindowRect(hWnd, out Rect r))
                {
                    int w = Math.Max(0, r.Right - r.Left);
                    int h = Math.Max(0, r.Bottom - r.Top);
                    int area = w * h;
                    if (area > bestArea)
                    {
                        bestArea = area;
                        best = hWnd;
                    }
                }

                return true;
            }, IntPtr.Zero);

            return best;
        }

        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MonitorInfo
        {
            public uint cbSize;
            public Rect rcMonitor;
            public Rect rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BitmapInfoHeader
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BitmapInfo
        {
            public BitmapInfoHeader bmiHeader;
            public uint bmiColors;
        }

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);

        [DllImport("gdi32.dll")]
        static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest, IntPtr hdcSrc, int xSrc, int ySrc, int rop);

        [DllImport("gdi32.dll")]
        static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
            [Out] byte[] lpvBits, ref BitmapInfo lpbmi, uint uUsage);

        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

        [DllImport("user32.dll")]
        static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

        [DllImport("dwmapi.dll")]
        static extern int DwmGetWindowAttribute(IntPtr hwnd, uint dwAttribute, out Rect pvAttribute, int cbAttribute);
    }
#endif
}
