using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.Wave
{
	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public class TrueSpeechWaveFormat : WaveFormat
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		private short[] unknown;

		public TrueSpeechWaveFormat()
		{
			this.waveFormatTag = WaveFormatEncoding.DspGroupTrueSpeech;
			this.channels = 1;
			this.averageBytesPerSecond = 1067;
			this.bitsPerSample = 1;
			this.blockAlign = 32;
			this.sampleRate = 8000;
			this.extraSize = 32;
			this.unknown = new short[16];
			this.unknown[0] = 1;
			this.unknown[1] = 240;
		}

		public override void Serialize(BinaryWriter writer)
		{
			base.Serialize(writer);
			short[] array = this.unknown;
			for (int i = 0; i < array.Length; i++)
			{
				short value = array[i];
				writer.Write(value);
			}
		}
	}
}
