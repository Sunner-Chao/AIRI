using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Alife.Function.Speech;

public static class SpeechEnvironment
{
    public static bool HasMicrophone()
    {
        return WaveInEvent.DeviceCount != 0;
    }

    public static bool HasHeadphones()
    {
        MMDevice? device = Enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        return device.FriendlyName.Contains("耳机") ||
               device.FriendlyName.Contains("Headphones") ||
               device.FriendlyName.Contains("Headset") ||
               device.FriendlyName.Contains("Earphone");
    }

    static readonly MMDeviceEnumerator Enumerator = new();
}