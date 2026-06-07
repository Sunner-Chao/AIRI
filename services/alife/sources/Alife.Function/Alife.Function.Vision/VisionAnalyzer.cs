using Alife.Basic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Alife.Function.Vision;

/// <summary>
/// 使用 InternVL2.5-1B 进行图像理解。
/// 内部维护一个长驻 Python 子进程，模型只加载一次。
/// </summary>
public class VisionAnalyzer : IDisposable
{
    /// <summary>
    /// 模型生成的最大字长限制（Token 数量）。
    /// </summary>
    public int MaxResponseTokens { get; set; } = 256;

    public VisionAnalyzer()
    {
        AlifePlatform.Command("python", "-m pip install torch==2.5.1+cu121 torchvision==0.20.1+cu121 --find-links https://mirrors.aliyun.com/pytorch-wheels/cu121/");
        AlifePlatform.Command("python", "-m pip install Pillow transformers qwen-vl-utils bitsandbytes accelerate sentencepiece tiktoken -i https://mirrors.aliyun.com/pypi/simple/");

        const string ModelId = "Qwen/Qwen2.5-VL-3B-Instruct";
        string modelPath = AlifeModel.EnsureModelExisting(ModelId);
        string script = Path.Combine(AppContext.BaseDirectory, "vision_bridge.py");
        string arguments = $"\"{script}\" --model_path \"{modelPath}\"";

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
                      ?? throw new InvalidOperationException("Failed to start Python vision bridge.");

            //监听管道异常信息
            _ = Task.Run(async () => {
                try
                {
                    StreamReader stderr = process.StandardError;
                    while (process.HasExited == false)
                    {
                        Console.WriteLine(await stderr.ReadLineAsync());
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            });

            stdin = process.StandardInput;
            stdout = process.StandardOutput;


            // 等待 "READY" 信号

            while (true)
            {
                string? line = Task.Run(async () => {
                    using CancellationTokenSource cts = new(TimeSpan.FromSeconds(120));
                    return await stdout.ReadLineAsync(cts.Token);
                }).Result;
                if (line == null)
                    throw new InvalidOperationException("无法获取到管道输入");
                if (line.StartsWith("{"))// 可能是错误信息
                {
                    JsonNode? err = JsonNode.Parse(line);
                    throw new InvalidOperationException($"Vision bridge startup error: {err?["message"]}");
                }
                if (line == "READY")
                    return;
                Console.WriteLine(line);
            }
        }
        catch (Exception ex)
        {
            isFallback = true;
            Console.WriteLine($"深度视觉初始化失败：\n{ex}");
        }
    }
    public void Dispose()
    {
        process?.Kill();
        process?.Dispose();
        syncLock.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 视觉问答：用中文提问，获得中文回答。
    /// </summary>
    public async Task<string> QueryAsync(string imagePath, string question, int? maxResponseTokens = 64,
        CancellationToken cancellationToken = default)
    {
        // 2. AI 深度视觉分析 (CUDA 增强)
        if (isFallback)
        {
            return "深度视觉初始化失败，无法使用神经网络分析。";
        }

        try
        {
            var aiResult = await SendRequestAsync(
            new { action = "query", image_path = imagePath, question, max_new_tokens = maxResponseTokens }, cancellationToken);

            return aiResult;
        }
        catch (Exception ex)
        {
            return $"调用失败：{ex.Message}";
        }
    }

    async Task<string> SendRequestAsync(object request, CancellationToken ct)
    {
        await syncLock.WaitAsync(ct);
        try
        {
            // 如果请求对象中没有指定 max_new_tokens，则使用默认配置
            string requestJson = JsonSerializer.Serialize(request);
            JsonNode? requestNode = JsonNode.Parse(requestJson);
            if (requestNode != null && requestNode["max_new_tokens"] == null)
            {
                requestNode["max_new_tokens"] = MaxResponseTokens;
            }

            string json = requestNode?.ToJsonString() ?? requestJson;
            //不要取消写入读取，尤其是写入后必须读取，否则会导致上传消息的残留
            stdin!.WriteLine(json.AsMemory());
            string? response = stdout!.ReadLine();

            if (response == null)
                throw new InvalidOperationException("Python bridge closed unexpectedly.");

            JsonNode? node = JsonNode.Parse(response);
            string? status = node?["status"]?.GetValue<string>();

            if (status == "ok")
                return node!["result"]?.GetValue<string>() ?? string.Empty;

            string message = node?["message"]?.GetValue<string>() ?? "Unknown error";
            throw new InvalidOperationException($"Vision bridge error: {message}");
        }
        finally
        {
            syncLock.Release();
        }
    }

    readonly Process? process;
    readonly StreamWriter? stdin;
    readonly StreamReader? stdout;
    readonly SemaphoreSlim syncLock = new(1, 1);
    readonly bool isFallback;//深度模型初始化失败
}
