namespace SmartSleepScheduler.Core.Services;

using System.IO;
using System.Media;
using System.Text;

/// <summary>
/// Handles crisp audio feedback for countdown ticks and timer completion.
/// Plays anti-aliased in-memory PCM WAV streams via Windows SoundPlayer (WaveOut) on a background thread.
/// Guaranteed fresh stream position on every playback; silent by default.
/// </summary>
public static class SoundService
{
    private static readonly byte[] _tickWav = GenerateWav(new[] { (1000.0, 50) }, 0.45);
    private static readonly byte[] _toggleWav = GenerateWav(new[] { (1150.0, 75) }, 0.55);
    private static readonly byte[] _completionWav = GenerateWav(new[] { (784.0, 100), (1046.5, 180) }, 0.55);

    public static void PlayTick()
    {
        if (!App.Settings.SoundEnabled) return;
        PlayWavBytes(_tickWav);
    }

    public static void PlayToggleTest()
    {
        PlayWavBytes(_toggleWav);
    }

    public static void PlayCompletion()
    {
        if (!App.Settings.SoundEnabled) return;
        PlayWavBytes(_completionWav);
    }

    private static void PlayWavBytes(byte[] wav)
    {
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
        bw.Write(sampleRate * 2); // Byte rate (SampleRate * NumChannels * BitsPerSample/8)
        bw.Write((short)2); // Block align (NumChannels * BitsPerSample/8)
        bw.Write((short)16); // Bits per sample

        // Data chunk
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(dataChunkSize);

        // Generate audio with 5ms envelope to avoid pop/clicks
        foreach (var (freq, msDuration) in toneList)
        {
            int samples = (int)(sampleRate * (msDuration / 1000.0));
            int fadeSamples = Math.Min(samples / 4, (int)(sampleRate * 0.005));

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
