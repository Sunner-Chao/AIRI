using System;
using Alife.Basic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Alife.Function.Speech;

public class GenieSpeechSynthesizer : SpeechSynthesizer
{
    readonly Process? process;
    readonly StreamWriter? stdin;
    readonly StreamReader? stdout;
    readonly SemaphoreSlim syncLock = new(1, 1);
    readonly bool isFallback;

    public GenieSpeechSynthesizer()
    {
        AlifePlatform.Command("python", "-m pip install genie-tts pypinyin g2pM jieba");

        string modelDir = Path.Combine(AlifePath.RuntimeFolderPath, "Genie");
        if (!Directory.Exists(modelDir))
        {
            Directory.CreateDirectory(modelDir);
        }

        string baseDir = Path.GetDirectoryName(typeof(GenieSpeechSynthesizer).Assembly.Location) ?? AppDomain.CurrentDomain.BaseDirectory;
        string script = Path.Combine(baseDir, "genie_bridge.py");
        string arguments = $"\"{script}\" --model_dir \"{modelDir}\"";

        ProcessStartInfo psi = new() {
            FileName = "python",
            Arguments = arguments,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
            Environment = {
                ["PYTHONIOENCODING"] = "utf-8",
                ["PYTHONUTF8"] = "1"
            }
        };

        try
        {
            process = Process.Start(psi)
                      ?? throw new InvalidOperationException("Failed to start Python Genie-TTS bridge.");

            // Listen to standard error in background
            _ = Task.Run(async () => {
                try
                {
                    StreamReader stderr = process.StandardError;
                    while (!process.HasExited)
                    {
                        string? line = await stderr.ReadLineAsync();
                        if (line != null)
                        {
                            Console.WriteLine($"[Genie Python] {line}");
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            });

            stdin = process.StandardInput;
            stdout = process.StandardOutput;

            // Wait for "READY" signal
            while (true)
            {
                string? line = Task.Run(async () => {
                    using CancellationTokenSource cts = new(TimeSpan.FromSeconds(120));
                    return await stdout.ReadLineAsync(cts.Token);
                }).Result;

                if (line == null)
                    throw new InvalidOperationException("Failed to read from Genie-TTS bridge stdout.");

                if (line.StartsWith("{")) // JSON error message
                {
                    JsonNode? err = JsonNode.Parse(line);
                    throw new InvalidOperationException($"Genie-TTS bridge startup error: {err?["message"]}");
                }

                if (line == "READY")
                    return;

                Console.WriteLine($"[Genie Startup] {line}");
            }
        }
        catch (Exception ex)
        {
            isFallback = true;
            AlifeTerminal.LogWarning($"Genie-TTS initialization failed:\n{ex}");
        }
    }

    private static string SanitizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Replace unsupported pause marks with commas
        text = text
            .Replace("~", "，")
            .Replace("～", "，")
            .Replace("…", "，")
            .Replace("—", "，");

        // Keep Chinese, Japanese (Hiragana/Katakana), letters, digits, and standard punctuations to avoid OOV warnings.
        // Quotes (' " “ ” ‘ ’) are intentionally stripped by not including them in the allowed list.
        var sb = new System.Text.StringBuilder();
        foreach (char c in text)
        {
            if ((c >= 0x4E00 && c <= 0x9FFF) || // CJK Unified Ideographs
                (c >= 0x3040 && c <= 0x309F) || // Hiragana
                (c >= 0x30A0 && c <= 0x30FF))   // Katakana
            {
                sb.Append(c);
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                continue;
            }

            if (c == '.' || c == ',' || c == '?' || c == '!' || c == ':' || c == ';' || c == '-' ||
                c == '。' || c == '，' || c == '？' || c == '！' || c == '：' || c == '；' || c == '、' ||
                c == ' ' || c == '\t')
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    public override async Task<string?> GenerateSpeechFileAsync(string text, CancellationToken cancellationToken = default)
    {
        if (isFallback)
        {
            AlifeTerminal.LogWarning("Genie-TTS is in fallback mode. Synthesis skipped.");
            return null;
        }

        text = SanitizeText(text);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        string md5Hash;
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(text));
            md5Hash = Convert.ToHexString(hashBytes);
        }

        string safeFileName = $"genie_{md5Hash}.wav";
        string outputPath = Path.Combine(AlifePath.TempFolderPath, safeFileName);

        if (File.Exists(outputPath))
            return outputPath;

        await syncLock.WaitAsync(cancellationToken);
        try
        {
            string requestJson = JsonSerializer.Serialize(new { text = text, output_path = outputPath });
            await stdin!.WriteLineAsync(requestJson.AsMemory(), cancellationToken);
            await stdin.FlushAsync(cancellationToken);

            string? response = await stdout!.ReadLineAsync(cancellationToken);
            if (response == null)
                throw new InvalidOperationException("Genie-TTS Python bridge closed unexpectedly.");

            JsonNode? node = JsonNode.Parse(response);
            string? status = node?["status"]?.GetValue<string>();

            if (status == "ok")
            {
                string resultPath = node!["result"]?.GetValue<string>() ?? string.Empty;
                if (File.Exists(resultPath))
                {
                    return resultPath;
                }
            }

            string message = node?["message"]?.GetValue<string>() ?? "Unknown error";
            AlifeTerminal.LogWarning($"Genie-TTS synthesis failed: {message}");
        }
        catch (Exception ex)
        {
            AlifeTerminal.LogWarning($"Genie-TTS synthesis execution failed: {ex.Message}");
        }
        finally
        {
            syncLock.Release();
        }

        return null;
    }

    public override void Dispose()
    {
        base.Dispose();
        try
        {
            process?.Kill();
            process?.Dispose();
        }
        catch {}
        syncLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
