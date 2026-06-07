using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Alife.Basic;

/// <summary>
/// 全局鼠标追踪器：监听系统级鼠标移动事件。
/// </summary>
public class MouseTracker
{
    public event Action<int, int>? MouseMoved;

    public void Start()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) == false) return;

        proc = MouseProc;
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule curModule = curProcess.MainModule!)
        {
            hookId = WindowsNative.SetWindowsHookEx(WindowsNative.WhMouseLl, proc, WindowsNative.GetModuleHandle(curModule.ModuleName), 0);
        }

        if (hookId == IntPtr.Zero)
            throw new Exception("[MouseTracker] 无法设置全局鼠标钩子");
    }

    public void Stop()
    {
        if (hookId != IntPtr.Zero)
        {
            WindowsNative.UnhookWindowsHookEx(hookId);
            hookId = IntPtr.Zero;
        }
    }

    IntPtr hookId = IntPtr.Zero;
    WindowsNative.LowLevelMouseProc? proc;

    IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (int)wParam == WindowsNative.WmMousemove)
        {
            WindowsNative.Msllhookstruct hookStruct = Marshal.PtrToStructure<WindowsNative.Msllhookstruct>(lParam);
            MouseMoved?.Invoke(hookStruct.pt.x, hookStruct.pt.y);
        }
        return WindowsNative.CallNextHookEx(hookId, nCode, wParam, lParam);
    }
}
