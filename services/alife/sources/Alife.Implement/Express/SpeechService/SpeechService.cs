using System.ComponentModel;
using Alife.Framework;
using Alife.Function.Interpreter;
using Alife.Function.Speech;
using Alife.Basic;
using Microsoft.SemanticKernel;
using System.Text;

namespace Alife.Implement;

public enum SpeechSynthesizerType
{
    Edge,
    Vits,
    Genie
}

public class SpeechConfig
{
    public SpeechSynthesizerType SynthesizerType { get; set; } = SpeechSynthesizerType.Edge;
    public bool AutoSpeakTextResponse { get; set; } = true;
    public string EdgeVoiceTone { get; set; } = "zh-CN-XiaoyiNeural";
    public int VitsSpeakerId { get; set; } = 551;
    public float VitsNoiseScale { get; set; } = 0.6f;
    public float VitsNoiseScaleW { get; set; } = 0.668f;
    public float VitsLengthScale { get; set; } = 1.2f;
}

public partial class SpeechService
{
    public static SpeechRecognizer? Recognizer { get; private set; }
    public static bool IsRecognizing => Recognizer is { IsRunning: true };
    public static bool ReceiveInput { get; set; } = true;

    static void TryInitializedAsync()
    {
        Recognizer ??= new SpeechRecognizer();
    }
}

[Plugin("语音对话", "为AI增加语音识别（基于本地模型）和语音转文字输出（基于edge-tts/VITS）的能力。", EditorUI = typeof(SpeechServiceUI))]
[Description("此服务让你获得能将文字以语音形式输出的能力。")]
public partial class SpeechService(FunctionService functionService)
    : InteractivePlugin<SpeechService>, IAsyncDisposable, IConfigurable<SpeechConfig>
{
    public SpeechSynthesizer? Synthesizer => synthesizer;

    [XmlFunction(FunctionMode.Content, order: -10)]
    [Description("将文本以语音方式输出。")]
    public async Task Speak(XmlExecutorContext context, CancellationToken cancellationToken)
    {
        try
        {
            switch (context.CallMode)
            {
                case CallMode.Opening:
                    try
                    {
                        if (synthesizer is { IsSpeaking: true })
                            await synthesizer.LastSpeaking;
                    }
                    catch (OperationCanceledException) {}
                    break;
                case CallMode.Closing:
                    break;
                case CallMode.Content:
                {
                    string content = context.Content.Trim();
                    if (string.IsNullOrWhiteSpace(content))
                        break;
                    if (synthesizer != null)
                        await synthesizer.SpeakAsync(content, cancellationToken);
                    break;
                }
            }
        }
        catch (OperationCanceledException) {}
    }

    public SpeechConfig? Configuration
    {
        get => configuration;
        set
        {
            configuration = value;
            if (configuration != null && synthesizer != null)
            {
                if (synthesizer is EdgeSpeechSynthesizer edge)
                {
                    edge.VoiceTone = configuration.EdgeVoiceTone;
                }
                else if (synthesizer is VitsSpeechSynthesizer vits)
                {
                    vits.SpeakerId = configuration.VitsSpeakerId;
                    vits.NoiseScale = configuration.VitsNoiseScale;
                    vits.NoiseScaleW = configuration.VitsNoiseScaleW;
                    vits.LengthScale = configuration.VitsLengthScale;
                }
            }
        }
    }

    public bool IsSynthesizing => synthesizer?.IsSpeaking ?? false;
    public bool IsReceiving
    {
        get => ReceiveInput;
        set => ReceiveInput = value;
    }

    protected override string ChatPrefixPrompt => "[语音识别的信息，请用Speak回复]";
    SpeechSynthesizer? synthesizer;
    SpeechConfig? configuration;
    readonly StringBuilder pendingSpeechText = new();

    public override async Task AwakeAsync(AwakeContext context)
    {
        await base.AwakeAsync(context);

        TryInitializedAsync();// 语音识别
        InitializeSynthesizer();// 语音合成

        functionService.RegisterHandler(this);

        void InitializeSynthesizer()
        {
            if (configuration == null)
                return;

            try
            {
                if (configuration.SynthesizerType == SpeechSynthesizerType.Vits)
                {
                    synthesizer = new VitsSpeechSynthesizer(
                    noiseScale: configuration.VitsNoiseScale,
                    noiseScaleW: configuration.VitsNoiseScaleW,
                    lengthScale: configuration.VitsLengthScale,
                    speakerId: configuration.VitsSpeakerId
                    );
                }
                else if (configuration.SynthesizerType == SpeechSynthesizerType.Genie)
                {
                    synthesizer = new GenieSpeechSynthesizer();
                }
                else
                {
                    synthesizer = new EdgeSpeechSynthesizer(configuration.EdgeVoiceTone);
                }
            }
            catch (Exception ex)
            {
                AlifeTerminal.LogWarning($"Failed to initialize speech synthesizer ({configuration.SynthesizerType}): {ex.Message}");
            }
        }
    }
    public override async Task StartAsync(Kernel kernel, ChatActivity chatActivity)
    {
        await base.StartAsync(kernel, chatActivity);

        if (Recognizer == null)
            throw new ArgumentNullException(nameof(Recognizer));
        try
        {
            await Recognizer.TryStartAsync();
            if (Recognizer.IsRunning)
                Recognizer.Recognized += OnRecognized;// 开始接收语音识别
        }
        catch (Exception ex)
        {
            AlifeTerminal.LogWarning($"Failed to start speech recognizer: {ex.Message}");
        }

        chatActivity.ChatBot.ChatReceived += OnChatReceived;
        chatActivity.ChatBot.ChatOver += OnChatOver;
    }
    public override async Task DestroyAsync()
    {
        if (Recognizer != null)
            Recognizer.Recognized -= OnRecognized;
        if (ChatActivity != null)
        {
            ChatActivity.ChatBot.ChatReceived -= OnChatReceived;
            ChatActivity.ChatBot.ChatOver -= OnChatOver;
        }
        await base.DestroyAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (synthesizer != null)
        {
            try
            {
                if (synthesizer.IsSpeaking)
                    await synthesizer.LastSpeaking;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            synthesizer.Dispose();
        }
    }

    void OnRecognized(string text)
    {
        if (ReceiveInput)
            Chat(text);
    }

    void OnChatReceived(string text)
    {
        if (configuration?.AutoSpeakTextResponse == true)
            pendingSpeechText.Append(text);
    }

    async void OnChatOver()
    {
        if (configuration?.AutoSpeakTextResponse != true || synthesizer == null)
            return;

        string text = pendingSpeechText.ToString().Trim();
        pendingSpeechText.Clear();

        if (string.IsNullOrWhiteSpace(text) || text.Contains('<') || text.Contains('>'))
            return;

        try
        {
            await synthesizer.SpeakAsync(text);
        }
        catch (Exception ex)
        {
            AlifeTerminal.LogWarning($"Auto text-to-speech failed: {ex.Message}");
        }
    }
}
