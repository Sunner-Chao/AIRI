using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Alife.Basic;

/// <summary>
/// 进程追踪器：利用 Windows Job Objects 确保子进程随父进程一同退出。
/// </summary>
public static class ProcessTracker
{
    /// <summary>
    /// 将指定进程加入追踪作业，使其生命周期与当前进程绑定。
    /// </summary>
    public static void Track(Process process)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) == false) return;
        if (JobHandle == IntPtr.Zero) return;

        WindowsNative.AssignProcessToJobObject(JobHandle, process.Handle);
    }

    static readonly IntPtr JobHandle;

    static ProcessTracker()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) == false) return;

        JobHandle = WindowsNative.CreateJobObject(IntPtr.Zero, null);

        WindowsNative.JobObjectBasicLimitInformation info = new() {
            LimitFlags = WindowsNative.JobObjectLimitKillOnJobClose
        };

        WindowsNative.JobObjectExtendedLimitInformation extendedInfo = new() {
            BasicLimitInformation = info
        };

        int length = Marshal.SizeOf(typeof(WindowsNative.JobObjectExtendedLimitInformation));
        IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);
            if (WindowsNative.SetInformationJobObject(JobHandle, 9, extendedInfoPtr, (uint)length) == false)
            {
                throw new Exception($"无法设置 Job Object 信息。错误代码: {Marshal.GetLastWin32Error()}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(extendedInfoPtr);
        }
    }
}
