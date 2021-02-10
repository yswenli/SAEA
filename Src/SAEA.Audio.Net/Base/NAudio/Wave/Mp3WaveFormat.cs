using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.Wave
{
	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public class Mp3WaveFormat : WaveFormat
	{
		public Mp3WaveFormatId id;

		public Mp3WaveFormatFlags flags;

		public ushort blockSize;

		public ushort framesPerBlock;

		public ushort codecDelay;

		private const short Mp3WaveFormatExtraBytes = 12;

		public Mp3WaveFormat(int sampleRate, int channels, int blockSize, int bitRate)
		{
			this.waveFormatTag = WaveFormatEncoding.MpegLayer3;
			this.channels = (short)channels;
			this.averageBytesPerSecond = bitRate / 8;
			this.bitsPerSample = 0;
			this.blockAlign = 1;
			this.sampleRate = sampleRate;
			this.extraSize = 12;
			this.id = Mp3WaveFormatId.Mpeg;
			this.flags = Mp3WaveFormatFlags.PaddingIso;
			this.blockSize = (ushort)blockSize;
			this.framesPerBlock = 1;
			this.codecDelay = 0;
		}
	}
}
