using System;
using System.Runtime.InteropServices;
using MelonLoader;
using UnityEngine;
using YargAccess.Util;

namespace YargAccess.Accessibility
{
    public static class NvdaFocusFix
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        static WndProcDelegate _wndProcDelegate;
        static IntPtr _wndProcDelegatePtr;
        static IntPtr _originalWndProc;
        static IntPtr _windowHandle;
        static bool _installed;

        public static bool IsInstalled => _installed;
        public static IntPtr WindowHandle => _windowHandle;

        public static void Install()
        {
            if (_installed) return;

            var hwnd = Win32.FindWindow("UnityWndClass", Application.productName);
            if (hwnd == IntPtr.Zero) hwnd = Win32.GetActiveWindow();
            if (hwnd == IntPtr.Zero)
            {
                try { hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle; }
                catch { }
            }
            if (hwnd == IntPtr.Zero)
            {
                MelonLogger.Warning("NvdaFocusFix: could not resolve game window handle; not installed.");
                return;
            }

            _windowHandle = hwnd;
            _wndProcDelegate = CustomWndProc;
            _wndProcDelegatePtr = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);

            _originalWndProc = Win32.SetWindowLongPtr(hwnd, Win32.GWLP_WNDPROC, _wndProcDelegatePtr);
            if (_originalWndProc == IntPtr.Zero)
            {
                MelonLogger.Warning("NvdaFocusFix: SetWindowLongPtr failed; not installed.");
                _wndProcDelegate = null;
                _wndProcDelegatePtr = IntPtr.Zero;
                return;
            }

            _installed = true;
            MelonLogger.Msg("NvdaFocusFix: WndProc subclassed on hwnd " + hwnd);
        }

        public static void Uninstall()
        {
            if (!_installed) return;
            try { Win32.SetWindowLongPtr(_windowHandle, Win32.GWLP_WNDPROC, _originalWndProc); }
            catch { }
            _installed = false;
            _wndProcDelegate = null;
            _wndProcDelegatePtr = IntPtr.Zero;
            _originalWndProc = IntPtr.Zero;
        }

        static IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                switch (msg)
                {
                    case Win32.WM_GETOBJECT:
                    {
                        return Win32.DefWindowProc(hWnd, msg, wParam, lParam);
                    }

                    case Win32.WM_ACTIVATE:
                    {
                        var result = Win32.CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
                        int activation = wParam.ToInt32() & 0xFFFF;
                        if (activation != Win32.WA_INACTIVE)
                        {
                            Win32.NotifyWinEvent(Win32.EVENT_SYSTEM_FOREGROUND,
                                hWnd, Win32.OBJID_WINDOW, Win32.CHILDID_SELF);
                            Win32.NotifyWinEvent(Win32.EVENT_OBJECT_FOCUS,
                                hWnd, Win32.OBJID_CLIENT, Win32.CHILDID_SELF);
                        }
                        return result;
                    }

                    case Win32.WM_SETFOCUS:
                    {
                        var result = Win32.CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
                        Win32.NotifyWinEvent(Win32.EVENT_OBJECT_FOCUS,
                            hWnd, Win32.OBJID_CLIENT, Win32.CHILDID_SELF);
                        return result;
                    }

                    case Win32.WM_CLOSE:
                    {
                        var orig = _originalWndProc;
                        Uninstall();
                        return Win32.CallWindowProc(orig, hWnd, msg, wParam, lParam);
                    }
                }

                return Win32.CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
            }
            catch (Exception ex)
            {
                try { MelonLogger.Error("NvdaFocusFix WndProc: " + ex.Message); } catch { }
                try { return Win32.CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam); }
                catch { return IntPtr.Zero; }
            }
        }
    }
}
