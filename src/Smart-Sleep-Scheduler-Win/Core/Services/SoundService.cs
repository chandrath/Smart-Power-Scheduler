using System.Media;

namespace SmartSleepScheduler.Core.Services;

/// <summary>
/// Handles subtle audio feedback for countdown ticks and timer completion.
/// Silent by default; strictly respects user SoundEnabled preference.
/// </summary>
public static class SoundService
{
    public static void PlayTick()
    {
        if (!App.Settings.SoundEnabled) return;

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                SystemSounds.Beep.Play();
            }
            catch
            {
                // Ignore audio device errors
            }
        });
    }

    public static void PlayCompletion()
    {
        if (!App.Settings.SoundEnabled) return;

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                SystemSounds.Asterisk.Play();
            }
            catch
            {
                // Ignore audio device errors
            }
        });
    }
}
