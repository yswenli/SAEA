using System;

namespace SAEA.Audio.NAudio.Wave.SampleProviders
{
	public class VolumeSampleProvider : ISampleProvider
	{
		private readonly ISampleProvider source;

		private float volume;

		public WaveFormat WaveFormat
		{
			get
			{
				return this.source.WaveFormat;
			}
		}

		public float Volume
		{
			get
			{
				return this.volume;
			}
			set
			{
				this.volume = value;
			}
		}

		public VolumeSampleProvider(ISampleProvider source)
		{
			this.source = source;
			this.volume = 1f;
		}

		public int Read(float[] buffer, int offset, int sampleCount)
		{
			int result = this.source.Read(buffer, offset, sampleCount);
			if (this.volume != 1f)
			{
				for (int i = 0; i < sampleCount; i++)
				{
					buffer[offset + i] *= this.volume;
				}
			}
			return result;
		}
	}
}
