namespace SmartSleepScheduler.Core.Services;

using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Handles rich, anti-aliased audio feedback for countdown ticks, toggle testing, and timer completion.
/// Uses native Win32 winmm PlaySound with fresh in-memory PCM WAV streams.
/// Supports user volume slider (10% - 100%) and sound schemes (Modern, Classic, Soft).
/// </summary>
public static class SoundService
{
    [DllImport("winmm.dll", SetLastError = true)]
    private static extern bool PlaySound(byte[] ptrToSound, IntPtr hmod, uint fdwSound);

    private const uint SND_ASYNC = 0x0001;
    private const uint SND_MEMORY = 0x0004;
    private const uint SND_NODEFAULT = 0x0002;

    public static void PlayTick()
    {
        if (!App.Settings.SoundEnabled) return;
        PlaySchemeSound(ToneType.Tick);
    }

    public static void PlayToggleTest()
    {
        PlaySchemeSound(ToneType.Toggle);
    }

    public static void PlayCompletion()
    {
        if (!App.Settings.SoundEnabled) return;
        PlaySchemeSound(ToneType.Completion);
    }

    private enum ToneType { Tick, Toggle, Completion }

    private static void PlaySchemeSound(ToneType type)
    {
        double volume = Math.Clamp(App.Settings.SoundVolume / 100.0, 0.05, 1.0);
        string scheme = App.Settings.SoundScheme ?? "Modern";

        var tones = GetTonesForScheme(scheme, type);
        byte[] wav = GenerateWav(tones, volume);

        try
        {
            PlaySound(wav, IntPtr.Zero, SND_MEMORY | SND_ASYNC | SND_NODEFAULT);
        }
        catch
        {
            try { SystemSounds.Asterisk.Play(); } catch { }
        }
    }

    private static (double freq, int ms)[] GetTonesForScheme(string scheme, ToneType type) => (scheme, type) switch
    {
        ("Classic", ToneType.Tick) => new[] { (900.0, 120) },
        ("Classic", ToneType.Toggle) => new[] { (1100.0, 140) },
        ("Classic", ToneType.Completion) => new[] { (850.0, 100), (1200.0, 180) },

        ("Soft", ToneType.Tick) => new[] { (520.0, 100) },
        ("Soft", ToneType.Toggle) => new[] { (650.0, 120) },
        ("Soft", ToneType.Completion) => new[] { (523.0, 120), (659.0, 180) },

        // Default: Modern
        (_, ToneType.Tick) => new[] { (1174.0, 140) },
        (_, ToneType.Toggle) => new[] { (1046.5, 90), (1318.5, 130) },
        (_, ToneType.Completion) => new[] { (784.0, 90), (987.7, 90), (1174.6, 180) },
        _ => new[] { (1174.0, 140) }
    };

    private static byte[] GenerateWav(IEnumerable<(double freq, int ms)> tones, double volume)
    {
        const int sampleRate = 44100;
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        var toneList = tones.ToList();
        int totalSamples = toneList.Sum(t => (int)(sampleRate * (t.ms / 1000.0)));
        int dataChunkSize = totalSamples * 2; // 16-bit mono

        // RIFF header
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataChunkSize);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));

        // Format chunk
        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16); // Subchunk1Size
        bw.Write((short)1); // PCM format
        bw.Write((short)1); // Mono channel
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2); // Byte rate
        bw.Write((short)2); // Block align
        bw.Write((short)16); // Bits per sample

        // Data chunk
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(dataChunkSize);

        // Generate audio with 6ms envelope to avoid pop/clicks
        foreach (var (freq, msDuration) in toneList)
        {
            int samples = (int)(sampleRate * (msDuration / 1000.0));
            int fadeSamples = Math.Min(samples / 4, (int)(sampleRate * 0.006));

            for (int i = 0; i < samples; i++)
            {
                double envelope = 1.0;
                if (i < fadeSamples)
                {
                    envelope = (double)i / fadeSamples;
                }
                else if (i > samples - fadeSamples)
                {
                    envelope = (double)(samples - i) / fadeSamples;
                }

                double t = (double)i / sampleRate;
                short sample = (short)(Math.Sin(2 * Math.PI * freq * t) * short.MaxValue * volume * envelope);
                bw.Write(sample);
            }
        }

        bw.Flush();
        return ms.ToArray();
    }
}
