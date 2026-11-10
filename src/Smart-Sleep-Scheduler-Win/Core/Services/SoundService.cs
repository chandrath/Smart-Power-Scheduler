namespace SmartSleepScheduler.Core.Services;

using System.IO;
using System.Media;
using System.Text;

/// <summary>
/// Handles subtle audio feedback for countdown ticks and timer completion.
/// Generates pristine PCM WAV audio in memory and plays via Windows SoundPlayer (WaveOut).
/// Completely independent of console beep drivers or Windows sound scheme settings.
/// Silent by default; strictly respects user SoundEnabled preference.
/// </summary>
public static class SoundService
{
    private static readonly SoundPlayer? _tickPlayer = CreatePlayer(1000, 50, 0.25);
    private static readonly SoundPlayer? _togglePlayer = CreatePlayer(1100, 75, 0.35);
    private static readonly SoundPlayer? _completionPlayer = CreateCompletionPlayer();

    public static void PlayTick()
    {
        if (!App.Settings.SoundEnabled) return;
        PlaySafe(_tickPlayer);
    }

    public static void PlayToggleTest()
    {
        PlaySafe(_togglePlayer);
    }

    public static void PlayCompletion()
    {
        if (!App.Settings.SoundEnabled) return;
        PlaySafe(_completionPlayer);
    }

    private static void PlaySafe(SoundPlayer? player)
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                if (player != null)
                {
                    player.Play();
                }
                else
                {
                    SystemSounds.Beep.Play();
                }
            }
            catch
            {
                try { SystemSounds.Beep.Play(); } catch { }
            }
        });
    }

    private static SoundPlayer? CreatePlayer(double frequency, int durationMs, double volume)
    {
        try
        {
            byte[] wavBytes = GenerateWav(new[] { (frequency, durationMs) }, volume);
            var ms = new MemoryStream(wavBytes);
            var player = new SoundPlayer(ms);
            player.Load();
            return player;
        }
        catch
        {
            return null;
        }
    }

    private static SoundPlayer? CreateCompletionPlayer()
    {
        try
        {
            byte[] wavBytes = GenerateWav(new[] { (784.0, 100), (1046.5, 180) }, 0.4);
            var ms = new MemoryStream(wavBytes);
            var player = new SoundPlayer(ms);
            player.Load();
            return player;
        }
        catch
        {
            return null;
        }
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
