namespace SmartPowerScheduler.Core.Services;

using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Handles rich, anti-aliased audio feedback for countdown ticks, toggle testing, and timer completion.
/// Uses SoundPlayer with in-memory PCM WAV streams on background worker threads.
/// Supports user volume slider (10% - 100%) and sound schemes (Modern, Classic, Soft).
/// </summary>
public static class SoundService
{
    public static void PlayTick()
    {
        if (!App.Settings.SoundEnabled) return;
        PlaySchemeSound(ToneType.Tick);
    }

    public static void PlayToggleTest(int? volumeOverride = null, string? schemeOverride = null)
    {
        PlaySchemeSound(ToneType.Toggle, volumeOverride, schemeOverride);
    }

    public static void PlayCompletion()
    {
        if (!App.Settings.SoundEnabled) return;
        PlaySchemeSound(ToneType.Completion);
    }

    public static void PlayBlockerAlert()
    {
        PlaySchemeSound(ToneType.BlockerAlert);
    }

    private enum ToneType { Tick, Toggle, Completion, BlockerAlert }

    private static void PlaySchemeSound(ToneType type, int? volumeOverride = null, string? schemeOverride = null)
    {
        double rawVolume = (volumeOverride ?? App.Settings.SoundVolume) / 100.0;
        rawVolume = Math.Clamp(rawVolume, 0.05, 1.0);
        // Perceptual acoustic loudness curve: mild power curve for clear, comfortable sound
        double volume = Math.Pow(rawVolume, 1.4) * 0.75;

        string scheme = schemeOverride ?? App.Settings.SoundScheme ?? "Modern";

        var tones = GetTonesForScheme(scheme, type);
        byte[] wav = GenerateWav(tones, volume);

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                using var ms = new MemoryStream(wav);
                using var player = new SoundPlayer(ms);
                player.PlaySync();
            }
            catch
            {
                try { SystemSounds.Asterisk.Play(); } catch { }
            }
        });
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
        (_, ToneType.BlockerAlert) => new[] { (587.3, 110), (440.0, 90), (880.0, 180) },
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
