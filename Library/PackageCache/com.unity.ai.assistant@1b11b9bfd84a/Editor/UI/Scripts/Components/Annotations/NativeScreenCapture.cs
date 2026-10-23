using System;
using Unity.AI.Assistant.Utils;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    /// <summary>
    /// Platform-agnostic abstraction for native screen capture.
    /// Routes calls to platform-specific implementations.
    /// </summary>
    static class NativeCapture
    {
        public static byte[] CaptureDesktopRGBA(out int width, out int height)
        {
#if UNITY_EDITOR_WIN
            return WindowsGdiCapture.CaptureMainMonitorRGBA(out width, out height);
#elif UNITY_EDITOR_OSX
            return MacCgCapture.CaptureMainMonitorRGBA(out width, out height);
#else
            width = height = 0;
            throw new PlatformNotSupportedException("Only Windows/macOS editor supported.");
#endif
        }

        /// <summary>
        /// Capture every visible OS-level Unity window and composite a single image of just
        /// those windows. Strategy: take one full-desktop OS framebuffer capture, then crop
        /// each Unity window's visual rect (DWM extended frame bounds on Win, kCGWindowBounds
        /// on Mac — both exclude drop-shadow padding) into the output canvas at the right
        /// offset. Z-order, chrome, and rounded-corner blending against whatever's behind
        /// come for free because the OS already composited those pixels.
        ///
        /// Gaps between Unity windows in the bounding rect (where no Unity window sits)
        /// remain alpha=0 so the viewer's background shows through. Other applications are
        /// excluded by virtue of only copying pixels inside Unity window visual bounds.
        ///
        /// Returns null if the platform is unsupported (e.g. Linux) or capture fails;
        /// callers should fall back to the per-EditorWindow stitching path on Linux.
        /// </summary>
        public static byte[] CaptureUnityCompositeRGBA(out int width, out int height)
        {
#if UNITY_EDITOR_WIN
            return CaptureUnityCompositeRGBAWin(out width, out height);
#elif UNITY_EDITOR_OSX
            return CaptureUnityCompositeRGBAMac(out width, out height);
#else
            width = height = 0;
            return null;
#endif
        }

#if UNITY_EDITOR_WIN
        static byte[] CaptureUnityCompositeRGBAWin(out int width, out int height)
        {
            var windows = WindowsGdiCapture.EnumerateUnityWindowsBackToFront();
            if (windows.Count == 0)
            {
                width = height = 0;
                InternalLog.LogError("[EditScreenCapture] No Unity windows found to capture");
                return null;
            }

            byte[] screen;
            int screenLeft, screenTop, screenW, screenH;
            try
            {
                screen = WindowsGdiCapture.CaptureVirtualScreenWithOrigin(out screenLeft, out screenTop, out screenW, out screenH);
            }
            catch (Exception e)
            {
                width = height = 0;
                InternalLog.LogError($"[EditScreenCapture] Virtual screen capture failed: {e.Message}");
                return null;
            }

            int boundsLeft = int.MaxValue, boundsTop = int.MaxValue;
            int boundsRight = int.MinValue, boundsBottom = int.MinValue;
            foreach (var entry in windows)
            {
                if (entry.rect.Left   < boundsLeft)   boundsLeft   = entry.rect.Left;
                if (entry.rect.Top    < boundsTop)    boundsTop    = entry.rect.Top;
                if (entry.rect.Right  > boundsRight)  boundsRight  = entry.rect.Right;
                if (entry.rect.Bottom > boundsBottom) boundsBottom = entry.rect.Bottom;
            }
            width  = Math.Max(1, boundsRight  - boundsLeft);
            height = Math.Max(1, boundsBottom - boundsTop);

            // Transparent canvas — gaps in the bounding rect remain alpha=0 in the PNG so
            // the EditScreenCaptureWindow background shows through naturally instead of a
            // hard fill colour.
            byte[] canvas = new byte[width * height * 4];

            // DWM extended frame bounds reports the rect 1px larger than the actual painted
            // window edge, so a straight copy leaves a 1px outline of whatever was behind
            // (transparent canvas / desktop) ringing every window. Inset by 1px on all sides
            // to crop that leak.
            const int inset = 1;
            foreach (var entry in windows)
            {
                int winW = entry.rect.Right  - entry.rect.Left - 2 * inset;
                int winH = entry.rect.Bottom - entry.rect.Top  - 2 * inset;
                if (winW <= 0 || winH <= 0)
                    continue;

                int srcX        = entry.rect.Left - screenLeft + inset;
                int srcYTopDown = entry.rect.Top  - screenTop  + inset;
                int dstX        = entry.rect.Left - boundsLeft + inset;
                int dstYTopDown = entry.rect.Top  - boundsTop  + inset;

                CopyRegionBottomUp(
                    screen, screenW, screenH, srcX, srcYTopDown, winW, winH,
                    canvas, width,   height,  dstX, dstYTopDown);
            }

            return canvas;
        }
#endif

#if UNITY_EDITOR_OSX
        static byte[] CaptureUnityCompositeRGBAMac(out int width, out int height)
        {
            var windows = MacCgCapture.EnumerateUnityWindowsBackToFront();
            if (windows.Count == 0)
            {
                width = height = 0;
                InternalLog.LogError("[EditScreenCapture] No Unity windows found to capture");
                return null;
            }

            byte[] screen;
            double screenOriginPtX, screenOriginPtY, screenWidthPt, screenHeightPt;
            int screenWPx, screenHPx;
            try
            {
                screen = MacCgCapture.CaptureDesktopWithOriginRGBA(
                    out screenOriginPtX, out screenOriginPtY,
                    out screenWidthPt,   out screenHeightPt,
                    out screenWPx,       out screenHPx);
            }
            catch (Exception e)
            {
                width = height = 0;
                InternalLog.LogError($"[EditScreenCapture] Desktop capture failed: {e.Message}");
                return null;
            }

            // Pixel-to-point ratio (retina = 2.0, non-retina = 1.0). Uniform across displays.
            // TODO: mixed retina + non-retina setups produce a fractional ratio applied
            // uniformly, which scales every Unity window incorrectly. Per-display capture
            // (CaptureWindowByIdRGBA per window with per-window pixel size) would handle it.
            double scale = screenWidthPt > 0 ? screenWPx / screenWidthPt : 1.0;

            double boundsLeft = double.MaxValue, boundsTop = double.MaxValue;
            double boundsRight = double.MinValue, boundsBottom = double.MinValue;
            foreach (var info in windows)
            {
                if (info.X < boundsLeft) boundsLeft = info.X;
                if (info.Y < boundsTop)  boundsTop  = info.Y;
                if (info.X + info.Width  > boundsRight)  boundsRight  = info.X + info.Width;
                if (info.Y + info.Height > boundsBottom) boundsBottom = info.Y + info.Height;
            }

            width  = Math.Max(1, (int)Math.Round((boundsRight  - boundsLeft) * scale));
            height = Math.Max(1, (int)Math.Round((boundsBottom - boundsTop)  * scale));

            // Transparent canvas — gaps in the bounding rect remain alpha=0 in the PNG so
            // the EditScreenCaptureWindow background shows through naturally instead of a
            // hard fill colour.
            byte[] canvas = new byte[width * height * 4];

            foreach (var info in windows)
            {
                int srcX        = (int)Math.Round((info.X - screenOriginPtX) * scale);
                int srcYTopDown = (int)Math.Round((info.Y - screenOriginPtY) * scale);
                int winW        = (int)Math.Round(info.Width  * scale);
                int winH        = (int)Math.Round(info.Height * scale);
                int dstX        = (int)Math.Round((info.X - boundsLeft) * scale);
                int dstYTopDown = (int)Math.Round((info.Y - boundsTop)  * scale);

                CopyRegionBottomUp(
                    screen, screenWPx, screenHPx, srcX, srcYTopDown, winW, winH,
                    canvas, width,     height,    dstX, dstYTopDown);
            }

            return canvas;
        }
#endif

        /// <summary>
        /// Copy a rectangle of pixels from a bottom-up RGBA src buffer to a bottom-up RGBA
        /// dst buffer. srcX/srcYTopDown and dstX/dstYTopDown are top-down coordinates of
        /// the rectangle's top-left in their respective buffers. Clips to both buffers.
        /// </summary>
        static void CopyRegionBottomUp(
            byte[] src, int srcW, int srcH, int srcX, int srcYTopDown, int copyW, int copyH,
            byte[] dst, int dstW, int dstH, int dstX, int dstYTopDown)
        {
            // Clip the rectangle independently against each buffer's bounds. Each step only
            // ever shrinks copyW/copyH (Math.Min) so the order of clip steps doesn't matter.
            ClipAxis(ref srcX,        ref dstX,        ref copyW, srcW, dstW);
            ClipAxis(ref srcYTopDown, ref dstYTopDown, ref copyH, srcH, dstH);

            if (copyW <= 0 || copyH <= 0)
                return;

            int srcStride = srcW * 4;
            int dstStride = dstW * 4;
            int copyBytes = copyW * 4;

            for (int row = 0; row < copyH; row++)
            {
                // Bottom-up row index for a given top-down y is (height - 1 - y).
                int srcRow = srcH - 1 - (srcYTopDown + row);
                int dstRow = dstH - 1 - (dstYTopDown + row);
                int srcOffset = srcRow * srcStride + srcX * 4;
                int dstOffset = dstRow * dstStride + dstX * 4;
                Buffer.BlockCopy(src, srcOffset, dst, dstOffset, copyBytes);
            }
        }

        /// <summary>
        /// One-axis clip helper: shrink copyLen and adjust src/dst offsets so the rectangle
        /// fits inside both src[0..srcLen] and dst[0..dstLen]. Pure shrinks (Math.Min) — safe
        /// to call in any order across axes.
        /// </summary>
        static void ClipAxis(ref int srcPos, ref int dstPos, ref int copyLen, int srcLen, int dstLen)
        {
            if (srcPos < 0) { copyLen += srcPos; dstPos -= srcPos; srcPos = 0; }
            if (dstPos < 0) { copyLen += dstPos; srcPos -= dstPos; dstPos = 0; }
            copyLen = Math.Min(copyLen, srcLen - srcPos);
            copyLen = Math.Min(copyLen, dstLen - dstPos);
        }
    }
}
