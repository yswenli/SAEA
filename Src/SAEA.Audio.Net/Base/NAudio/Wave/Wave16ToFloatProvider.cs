using SAEA.Audio.Base.NAudio.Utils;
using System;

namespace SAEA.Audio.Base.NAudio.Wave
{
	public class Wave16ToFloatProvider : IWaveProvider
	{
		private IWaveProvider sourceProvider;

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

		public Wave16ToFloatProvider(IWaveProvider sourceProvider)
		{
			if (sourceProvider.WaveFormat.Encoding != WaveFormatEncoding.Pcm)
			{
				throw new ArgumentException("Only PCM supported");
			}
			if (sourceProvider.WaveFormat.BitsPerSample != 16)
			{
				throw new ArgumentException("Only 16 bit audio supported");
			}
			this.waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sourceProvider.WaveFormat.SampleRate, sourceProvider.WaveFormat.Channels);
			this.sourceProvider = sourceProvider;
			this.volume = 1f;
		}

		public int Read(byte[] destBuffer, int offset, int numBytes)
		{
			int num = numBytes / 2;
			this.sourceBuffer = BufferHelpers.Ensure(this.sourceBuffer, num);
			int arg_3D_0 = this.sourceProvider.Read(this.sourceBuffer, offset, num);
			WaveBuffer waveBuffer = new WaveBuffer(this.sourceBuffer);
			WaveBuffer waveBuffer2 = new WaveBuffer(destBuffer);
			int num2 = arg_3D_0 / 2;
			int num3 = offset / 4;
			for (int i = 0; i < num2; i++)
			{
				waveBuffer2.FloatBuffer[num3++] = (float)waveBuffer.ShortBuffer[i] / 32768f * this.volume;
			}
			return num2 * 4;
		}
	}
}
