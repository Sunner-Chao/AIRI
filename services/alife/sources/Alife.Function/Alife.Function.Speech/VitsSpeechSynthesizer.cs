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

public class VitsSpeechSynthesizer : SpeechSynthesizer
{
    public int SpeakerId { get; set; }
    public float NoiseScale { get; set; }
    public float NoiseScaleW { get; set; }
    public float LengthScale { get; set; }

    public VitsSpeechSynthesizer(
        float noiseScale = 0.6f,
        float noiseScaleW = 0.668f,
        float lengthScale = 1.2f,
        int speakerId = 551)
    {
        SpeakerId = speakerId;
        NoiseScale = noiseScale;
        NoiseScaleW = noiseScaleW;
        LengthScale = lengthScale;

        string baseDir = Path.GetDirectoryName(typeof(VitsSpeechSynthesizer).Assembly.Location) ?? AppDomain.CurrentDomain.BaseDirectory;
        string vitsDir = Path.Combine(AlifePath.RuntimeFolderPath, "VITS");

        AlifePlatform.Command("python", $"-m pip install -r \"{Path.Combine(vitsDir, "requirements.txt").Replace(Path.DirectorySeparatorChar, '/')}\\\"");

        ProcessStartInfo psi = new() {
            FileName = "python",
            Arguments = $"\"{Path.Combine(baseDir, "vits_bridge.py")}\" \"{vitsDir}\"",
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
                      ?? throw new InvalidOperationException("Failed to start Python VITS bridge.");

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
                            Console.WriteLine($"[VITS Python] {line}");
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

            //等待模型加载
            while (true)
            {
                string? line = Task.Run(async () => {
                    using CancellationTokenSource cts = new(TimeSpan.FromSeconds(120));
                    return await stdout.ReadLineAsync(cts.Token);
                }).Result;

                if (line == null)
                    throw new InvalidOperationException("Failed to read from VITS bridge stdout.");

                if (line.StartsWith("{"))// JSON error message
                {
                    JsonNode? err = JsonNode.Parse(line);
                    throw new InvalidOperationException($"VITS bridge startup error: {err?["message"]}");
                }

                if (line == "READY")
                    return;

                Console.WriteLine($"[VITS Startup] {line}");
            }
        }
        catch (Exception ex)
        {
            isFallback = true;
            AlifeTerminal.LogWarning($"VITS-TTS initialization failed:\n{ex}");
        }
    }
    public override void Dispose()
    {
        base.Dispose();
        try
        {
            process?.Kill();
            process?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        syncLock.Dispose();
        GC.SuppressFinalize(this);
    }

    public override async Task<string?> GenerateSpeechFileAsync(string text, CancellationToken cancellationToken = default)
    {
        if (isFallback)
        {
            AlifeTerminal.LogWarning("VITS-TTS is in fallback mode. Synthesis skipped.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(text))
            return null;

        string md5Hash;
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(text));
            md5Hash = Convert.ToHexString(hashBytes);
        }

        string safeFileName = $"vits_{SpeakerId}_{md5Hash}.wav";
        string outputPath = Path.Combine(AlifePath.TempFolderPath, safeFileName);

        if (File.Exists(outputPath))
            return outputPath;

        await syncLock.WaitAsync(cancellationToken);
        try
        {
            string requestJson = JsonSerializer.Serialize(new {
                text,
                output_path = outputPath,
                noise_scale = NoiseScale,
                noise_scale_w = NoiseScaleW,
                length_scale = LengthScale,
                speaker_id = SpeakerId
            });
            await stdin!.WriteLineAsync(requestJson.AsMemory(), cancellationToken);
            await stdin.FlushAsync(cancellationToken);

            // Keep reading until we get a JSON line (starts with '{').
            // Non-JSON lines are PyTorch/VITS info messages that leaked through stdout;
            // we log them and discard so they don't corrupt the JSON parse.
            string? response;
            while (true)
            {
                response = await stdout!.ReadLineAsync();//读取是禁止取消的，否则会导致旧音频消息残留
                if (response == null)
                    throw new InvalidOperationException("VITS Python bridge closed unexpectedly.");
                if (response.StartsWith('{'))
                    break;
                Console.WriteLine($"[VITS stdout] {response}");
            }

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
            AlifeTerminal.LogWarning($"VITS synthesis failed: {message}");
        }
        catch (Exception ex)
        {
            AlifeTerminal.LogWarning($"VITS synthesis execution failed: {ex.Message}");
        }
        finally
        {
            syncLock.Release();
        }

        return null;
    }

    readonly Process? process;
    readonly StreamWriter? stdin;
    readonly StreamReader? stdout;
    readonly SemaphoreSlim syncLock = new(1, 1);
    readonly bool isFallback;
}
