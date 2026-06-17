using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Frtal.LorebookReader {

    /// <summary>
    /// Snímek klientské oblasti okna GW2 (funguje pro windowed
    /// i windowed-fullscreen, na libovolném monitoru).
    /// </summary>
    public static class ScreenCapture {

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        /// <summary>
        /// Vrátí snímek + obdélník (ve screen souřadnicích), odkud byl pořízen.
        /// </summary>
        public static Bitmap Grab(IntPtr gw2WindowHandle, out Rectangle screenRect) {
            screenRect = GetGw2ClientRect(gw2WindowHandle)
                         ?? new Rectangle(0, 0,
                                          GetSystemMetrics(SM_CXSCREEN),
                                          GetSystemMetrics(SM_CYSCREEN));

            var bmp = new Bitmap(screenRect.Width, screenRect.Height,
                                 System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(bmp)) {
                g.CopyFromScreen(screenRect.Left, screenRect.Top, 0, 0,
                                 new Size(screenRect.Width, screenRect.Height));
            }
            return bmp;
        }

        private static Rectangle? GetGw2ClientRect(IntPtr hwnd) {
            if (hwnd == IntPtr.Zero) return null;
            if (!GetClientRect(hwnd, out RECT rc)) return null;
            int w = rc.Right - rc.Left, h = rc.Bottom - rc.Top;
            if (w < 200 || h < 200) return null;
            var origin = new POINT { X = 0, Y = 0 };
            if (!ClientToScreen(hwnd, ref origin)) return null;
            return new Rectangle(origin.X, origin.Y, w, h);
        }
    }
}
