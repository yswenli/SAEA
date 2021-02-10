using SAEA.Audio.NAudio.Utils;
using System;

namespace SAEA.Audio.NAudio.Wave
{
    public class WaveFloatTo16Provider : IWaveProvider
	{
		private readonly IWaveProvider sourceProvider;

		private readonly WaveFormat waveFormat;

		private volatile float volume;

		private byte[] sourceBuffer;

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

		public WaveFloatTo16Provider(IWaveProvider sourceProvider)
		{
			if (sourceProvider.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
			{
				throw new ArgumentException("Input wave provider must be IEEE float", "sourceProvider");
			}
			if (sourceProvider.WaveFormat.BitsPerSample != 32)
			{
				throw new ArgumentException("Input wave provider must be 32 bit", "sourceProvider");
			}
			this.waveFormat = new WaveFormat(sourceProvider.WaveFormat.SampleRate, 16, sourceProvider.WaveFormat.Channels);
			this.sourceProvider = sourceProvider;
			this.volume = 1f;
		}

		public int Read(byte[] destBuffer, int offset, int numBytes)
		{
			int num = numBytes * 2;
			this.sourceBuffer = BufferHelpers.Ensure(this.sourceBuffer, num);
			int arg_3D_0 = this.sourceProvider.Read(this.sourceBuffer, 0, num);
			WaveBuffer waveBuffer = new WaveBuffer(this.sourceBuffer);
			WaveBuffer waveBuffer2 = new WaveBuffer(destBuffer);
			int num2 = arg_3D_0 / 4;
			int num3 = offset / 2;
			for (int i = 0; i < num2; i++)
			{
				float num4 = waveBuffer.FloatBuffer[i] * this.volume;
				if (num4 > 1f)
				{
					num4 = 1f;
				}
				if (num4 < -1f)
				{
					num4 = -1f;
				}
				waveBuffer2.ShortBuffer[num3++] = (short)(num4 * 32767f);
			}
			return num2 * 2;
		}
	}
}
