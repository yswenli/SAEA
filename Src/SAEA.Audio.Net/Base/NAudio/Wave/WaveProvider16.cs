using System;

namespace SAEA.Audio.NAudio.Wave
{
	public abstract class WaveProvider16 : IWaveProvider
	{
		private WaveFormat waveFormat;

		public WaveFormat WaveFormat
		{
			get
			{
				return this.waveFormat;
			}
		}

		public WaveProvider16() : this(44100, 1)
		{
		}

		public WaveProvider16(int sampleRate, int channels)
		{
			this.SetWaveFormat(sampleRate, channels);
		}

		public void SetWaveFormat(int sampleRate, int channels)
		{
			this.waveFormat = new WaveFormat(sampleRate, 16, channels);
		}

		public int Read(byte[] buffer, int offset, int count)
		{
			WaveBuffer waveBuffer = new WaveBuffer(buffer);
			int sampleCount = count / 2;
			return this.Read(waveBuffer.ShortBuffer, offset / 2, sampleCount) * 2;
		}

		public abstract int Read(short[] buffer, int offset, int sampleCount);
	}
}
