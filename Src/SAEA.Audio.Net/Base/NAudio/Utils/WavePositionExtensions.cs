using SAEA.Audio.NAudio.Wave;
using System;

namespace SAEA.Audio.NAudio.Utils
{
    public static class WavePositionExtensions
    {
        public static TimeSpan GetPositionTimeSpan(this IWavePosition @this)
        {
            return TimeSpan.FromMilliseconds((double)(@this.GetPosition() / (long)(@this.OutputWaveFormat.Channels * @this.OutputWaveFormat.BitsPerSample / 8)) * 1000.0 / (double)@this.OutputWaveFormat.SampleRate);
        }
    }
}
