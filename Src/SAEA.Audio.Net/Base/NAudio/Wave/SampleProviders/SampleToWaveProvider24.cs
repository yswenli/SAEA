using SAEA.Audio.Base.NAudio.Utils;
using System;

namespace SAEA.Audio.Base.NAudio.Wave.SampleProviders
{
	public class SampleToWaveProvider24 : IWaveProvider
	{
		private readonly ISampleProvider sourceProvider;

		private readonly WaveFormat waveFormat;

		private volatile float volume;

		private float[] sourceBuffer;

		public WaveFormat WaveFormat
		{
			get
			{
				return this.waveFormat;
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

		public SampleToWaveProvider24(ISampleProvider sourceProvider)
		{
			if (sourceProvider.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
			{
				throw new ArgumentException("Input source provider must be IEEE float", "sourceProvider");
			}
			if (sourceProvider.WaveFormat.BitsPerSample != 32)
			{
				throw new ArgumentException("Input source provider must be 32 bit", "sourceProvider");
			}
			this.waveFormat = new WaveFormat(sourceProvider.WaveFormat.SampleRate, 24, sourceProvider.WaveFormat.Channels);
			this.sourceProvider = sourceProvider;
			this.volume = 1f;
		}

		public int Read(byte[] destBuffer, int offset, int numBytes)
		{
			int num = numBytes / 3;
			this.sourceBuffer = BufferHelpers.Ensure(this.sourceBuffer, num);
			int num2 = this.sourceProvider.Read(this.sourceBuffer, 0, num);
			int num3 = offset;
			for (int i = 0; i < num2; i++)
			{
				float num4 = this.sourceBuffer[i] * this.volume;
				if (num4 > 1f)
				{
					num4 = 1f;
				}
				if (num4 < -1f)
				{
					num4 = -1f;
				}
				int num5 = (int)((double)num4 * 8388607.0);
				destBuffer[num3++] = (byte)num5;
				destBuffer[num3++] = (byte)(num5 >> 8);
				destBuffer[num3++] = (byte)(num5 >> 16);
			}
			return num2 * 3;
		}
	}
}
