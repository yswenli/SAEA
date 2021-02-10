using System;

namespace SAEA.Audio.NAudio.Wave.SampleProviders
{
	internal static class SampleProviderConverters
	{
		public static ISampleProvider ConvertWaveProviderIntoSampleProvider(IWaveProvider waveProvider)
		{
			ISampleProvider result;
			if (waveProvider.WaveFormat.Encoding == WaveFormatEncoding.Pcm)
			{
				if (waveProvider.WaveFormat.BitsPerSample == 8)
				{
					result = new Pcm8BitToSampleProvider(waveProvider);
				}
				else if (waveProvider.WaveFormat.BitsPerSample == 16)
				{
					result = new Pcm16BitToSampleProvider(waveProvider);
				}
				else if (waveProvider.WaveFormat.BitsPerSample == 24)
				{
					result = new Pcm24BitToSampleProvider(waveProvider);
				}
				else
				{
					if (waveProvider.WaveFormat.BitsPerSample != 32)
					{
						throw new InvalidOperationException("Unsupported bit depth");
					}
					result = new Pcm32BitToSampleProvider(waveProvider);
				}
			}
			else
			{
				if (waveProvider.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
				{
					throw new ArgumentException("Unsupported source encoding");
				}
				if (waveProvider.WaveFormat.BitsPerSample == 64)
				{
					result = new WaveToSampleProvider64(waveProvider);
				}
				else
				{
					result = new WaveToSampleProvider(waveProvider);
				}
			}
			return result;
		}
	}
}
