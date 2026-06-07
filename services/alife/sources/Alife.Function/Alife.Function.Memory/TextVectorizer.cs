using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Alife.Basic;

namespace Alife.Function.Memory;

/// <summary>
/// 纯粹且独立的文本向量化器。
/// 内部自行加载并管理基于 ONNX 的 BERT 模型，通过最新的 Microsoft.Extensions.AI 提供嵌入支持。
/// </summary>
public class TextVectorizer
{
    public TextVectorizer()
    {
        string modelDir = AlifeModel.EnsureModelExisting("BAAI/bge-small-zh-v1.5");
        string modelPath = Path.Combine(modelDir, "model.onnx");
        string vocabPath = Path.Combine(modelDir, "vocab.txt");

        if (File.Exists(modelPath) == false && File.Exists(Path.Combine(modelDir, "model.safetensors")))
            AlifeModel.ConvertSafetensorsToOnnx(modelDir);

        if (!File.Exists(modelPath))
            throw new FileNotFoundException($"ONNX 模型转换失败或未找到：{modelPath}");
        if (!File.Exists(vocabPath))
            throw new FileNotFoundException($"找不到词表文件：{vocabPath}");

        IKernelBuilder builder = Kernel.CreateBuilder();
        builder.AddBertOnnxEmbeddingGenerator(modelPath, vocabPath);
        Kernel kernel = builder.Build();

        generator = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
    }

    /// <summary>
    /// 将文本转换为向量。
    /// </summary>
    public async Task<float[]> VectorizeAsync(string text)
    {
        var result = await generator.GenerateAsync([text]);
        return result.First().Vector.ToArray();
    }

    readonly IEmbeddingGenerator<string, Embedding<float>> generator;
}
