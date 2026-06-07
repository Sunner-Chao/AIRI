using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace Alife.Basic;

/// <summary>
/// 跨平台抽象层：根据当前操作系统调度不同的底层实现。
/// </summary>
public static class AlifePlatform
{
    public static (int Width, int Height) GetResolution()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return WindowsPlatform.GetResolution();

        throw new PlatformNotSupportedException("当前平台不支持获取分辨率。");
    }

    /// <summary>
    /// 判断当前系统是否处于锁屏/息屏状态。
    /// </summary>
    public static bool IsLocking()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return WindowsPlatform.IsLocking();

        return false;
    }

    public static void Notice(string title, string message)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            WindowsPlatform.Notice(title, message);
            return;
        }

        AlifeTerminal.LogWarning($"[Notification] {title}: {message} (当前平台暂不支持弹出式通知)");
    }

    public static void Command(string fileName, string arguments)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            WindowsPlatform.Command(fileName, arguments);
            return;
        }

        // 未来可在此添加 Linux/macOS 的 Shell 调用实现 (如使用 /bin/sh)
        throw new PlatformNotSupportedException("当前平台暂不支持执行命令行。");
    }

    public static string Screenshot()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return WindowsPlatform.Screenshot();

        throw new PlatformNotSupportedException("当前平台暂不支持截屏。");
    }

    public static string GetRunningWindowTitles()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return WindowsPlatform.GetRunningWindowTitles();

        return "当前平台不支持窗口枚举";
    }

    public static string? PickFolder(string title = "请选择文件夹")
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return WindowsPlatform.PickFolder(title);

        throw new PlatformNotSupportedException("当前平台暂不支持目录选择对话框。");
    }

    public static async Task<string> OcrAsync(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return await WindowsPlatform.OcrAsync(path);

        return "当前平台不支持 OCR";
    }

    /// <summary>
    /// 通过 HttpClient 下载文件到本地
    /// </summary>
    public static async Task DownloadFileAsync(string url, string savePath)
    {
        //伪装用户
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        if (url.Contains("multimedia.nt.qq.com.cn") || url.Contains("qpic.cn"))
            request.Headers.Add("Referer", "https://q.qq.com/");

        //下载文件
        using var response = await SharedHttpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        byte[] data = await response.Content.ReadAsByteArrayAsync();

        string? dir = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllBytesAsync(savePath, data);
    }

    static readonly HttpClient SharedHttpClient = new();
}
