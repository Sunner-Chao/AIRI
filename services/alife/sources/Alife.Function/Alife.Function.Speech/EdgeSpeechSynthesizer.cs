using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alife.Basic;

namespace Alife.Function.Speech;

public class EdgeSpeechSynthesizer : SpeechSynthesizer
{
    public string VoiceTone { get; set; }

    public EdgeSpeechSynthesizer(string voiceTone)
    {
        AlifePlatform.Command("python", "-m pip install --upgrade edge-tts");
        VoiceTone = voiceTone;
    }

    public override async Task<string?> GenerateSpeechFileAsync(string text, CancellationToken cancellationToken = default)
    {
        //计算输出位置
        string fileSafeText = string.Concat(text.Where(ch => invalidChars.Contains(ch) == false));
        if (string.IsNullOrWhiteSpace(fileSafeText))
            return null;
        string outputPath = Path.Combine(AlifePath.TempFolderPath, fileSafeText + ".mp3");
        if (File.Exists(outputPath))
            return outputPath;

        ProcessStartInfo psi = new() {
            FileName = "python",
            Arguments = $"-m edge_tts --text \"{fileSafeText}\" --voice {VoiceTone} --write-media \"{outputPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using Process? process = Process.Start(psi);
        if (process == null)
            return null;

        try
        {
            await Task.WhenAny(
            process.WaitForExitAsync(cancellationToken),
            Task.Delay(5000, cancellationToken)
            );
            if (process.HasExited == false)
                throw new TimeoutException();
            if (process.ExitCode != 0)
                throw new Exception(
                $"{outputPath}\n{await process.StandardOutput.ReadToEndAsync(cancellationToken)}\n{await process.StandardError.ReadToEndAsync(cancellationToken)}"
                );
            if (File.Exists(outputPath) == false)
                throw new Exception($"语音文件未生成：{outputPath}");

            return outputPath;
        }
        catch (TimeoutException)
        {
            return null;
        }
        finally
        {
            if (process.HasExited == false)
                process.Kill();
        }
    }

    readonly char[] invalidChars = Path.GetInvalidFileNameChars();
}
