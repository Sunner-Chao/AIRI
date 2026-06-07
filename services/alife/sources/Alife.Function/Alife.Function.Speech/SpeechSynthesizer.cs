using Alife.Basic;
using NAudio.Wave;

namespace Alife.Function.Speech;

public abstract class SpeechSynthesizer : IDisposable
{
    public bool IsSpeaking => audioPlayTask is { IsCompleted: false };
    public Task LastSpeaking => audioPlayTask;

    public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        // 收到新的语音播报任务，先进行语音合成
        audioSynthesizingTask = GenerateSpeechFileAsync(text, cancellationToken);
        // 如果当前有音频在播放，则等待占用结束
        if (IsSpeaking)
        {
            try
            {
                await LastSpeaking;
            }
            catch (OperationCanceledException)
            {
                return;// 语音被打断，那么后续语音显然也不用播放了
            }
        }

        // 可以播放音频
        string? audioFile = null;
        try
        {
            audioFile = await audioSynthesizingTask;// 等待合成任务完成
        }
        catch (Exception e)
        {
            AlifeTerminal.LogWarning(e.ToString());
        }

        if (audioFile == null)
            return;// 没有可朗读的文本

        // 不等待播放任务，继续接收下一次函数调用，从而实现预加载
        _ = SpeakFromFileAsync(audioFile, cancellationToken).ContinueWith(t => {
            if (t is { IsFaulted: true, Exception: not null })
            {
                AlifeTerminal.LogWarning($"SpeakFromFileAsync failed: {t.Exception.Flatten()}");
            }
            try
            {
                // 播放完成后，尝试删除语音
                if (File.Exists(audioFile))
                {
                    File.Delete(audioFile);
                }
            }
            catch (Exception e)
            {
                AlifeTerminal.LogWarning(e.ToString());
            }
        }, cancellationToken);
    }

    public abstract Task<string?> GenerateSpeechFileAsync(string text, CancellationToken cancellationToken = default);

    public virtual void Dispose()
    {
        if (speakCancellation != null)
        {
            speakCancellation.Cancel();
            speakCancellation.Dispose();
        }
    }

    // 裁剪开头和结尾静音
    class SilenceTrimmer : ISampleProvider
    {
        public WaveFormat WaveFormat { get; }
        readonly float[] samples;
        int position;

        public SilenceTrimmer(ISampleProvider source, float threshold = 0.01f)
        {
            WaveFormat = source.WaveFormat;
            var allSamples = new List<float>();
            float[] tempBuffer = new float[WaveFormat.SampleRate];
            int read;
            while ((read = source.Read(tempBuffer, 0, tempBuffer.Length)) > 0)
            {
                for (int i = 0; i < read; i++)
                    allSamples.Add(tempBuffer[i]);
            }

            int start = 0;
            while (start < allSamples.Count && Math.Abs(allSamples[start]) <= threshold)
                start++;

            int end = allSamples.Count - 1;
            while (end > start && Math.Abs(allSamples[end]) <= threshold)
                end--;

            if (start <= end)
            {
                int length = end - start + 1;
                samples = new float[length];
                allSamples.CopyTo(start, samples, 0, length);
            }
            else
            {
                samples = Array.Empty<float>();
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int available = samples.Length - position;
            int toCopy = Math.Min(available, count);
            if (toCopy > 0)
            {
                samples.AsSpan(position, toCopy).CopyTo(buffer.AsSpan(offset, toCopy));
                position += toCopy;
            }
            return toCopy;
        }
    }

    Task<string?> audioSynthesizingTask = Task.FromResult<string?>(null);
    Task audioPlayTask = Task.CompletedTask;
    CancellationTokenSource? speakCancellation;

    async Task SpeakFromFileAsync(string file, CancellationToken cancellationToken = default)
    {
        if (IsSpeaking)
            await StopSpeakAsync();

        speakCancellation = new CancellationTokenSource();
        using CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, speakCancellation.Token);
        audioPlayTask = PlayAudioAsync(file, cancellationTokenSource.Token);

        await audioPlayTask;

        async Task PlayAudioAsync(string filePath, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource tcs = new();

            await using AudioFileReader reader = new(filePath);//音频读取
            SilenceTrimmer silenceTrimmer = new(reader);//音频预处理
            using WaveOutEvent speaker = new();
            speaker.Init(silenceTrimmer);
            speaker.PlaybackStopped += OnPlaybackStopped;
            speaker.Play();

            await using CancellationTokenRegistration registration = cancellationToken.Register(() => speaker.Stop());
            await tcs.Task;//等待播放完毕

            void OnPlaybackStopped(object? _, StoppedEventArgs e)
            {
                if (e.Exception != null)
                    tcs.TrySetException(e.Exception);
                else
                    tcs.TrySetResult();
            }
        }
    }

    Task StopSpeakAsync()
    {
        if (IsSpeaking == false)
            throw new InvalidOperationException("当前没有语音中。");

        return speakCancellation!.CancelAsync();
    }
}
