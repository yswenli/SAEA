using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Wave
{
	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public class ImaAdpcmWaveFormat : WaveFormat
	{
		private short samplesPerBlock;

		private ImaAdpcmWaveFormat()
		{
		}

		public ImaAdpcmWaveFormat(int sampleRate, int channels, int bitsPerSample)
		{
			this.waveFormatTag = WaveFormatEncoding.DviAdpcm;
			this.sampleRate = sampleRate;
			this.channels = (short)channels;
			this.bitsPerSample = (short)bitsPerSample;
			this.extraSize = 2;
			this.blockAlign = 0;
			this.averageBytesPerSecond = 0;
			this.samplesPerBlock = 0;
		}
	}
}
