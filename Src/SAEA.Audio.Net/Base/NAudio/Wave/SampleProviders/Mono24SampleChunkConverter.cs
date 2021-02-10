using SAEA.Audio.NAudio.Utils;
using System;

namespace SAEA.Audio.NAudio.Wave.SampleProviders
{
	internal class Mono24SampleChunkConverter : ISampleChunkConverter
	{
		private int offset;

		private byte[] sourceBuffer;

		private int sourceBytes;

		public bool Supports(WaveFormat waveFormat)
		{
			return waveFormat.Encoding == WaveFormatEncoding.Pcm && waveFormat.BitsPerSample == 24 && waveFormat.Channels == 1;
		}

		public void LoadNextChunk(IWaveProvider source, int samplePairsRequired)
		{
			int num = samplePairsRequired * 3;
			this.sourceBuffer = BufferHelpers.Ensure(this.sourceBuffer, num);
			this.sourceBytes = source.Read(this.sourceBuffer, 0, num);
			this.offset = 0;
		}

		public bool GetNextSample(out float sampleLeft, out float sampleRight)
		{
			if (this.offset < this.sourceBytes)
			{
				sampleLeft = (float)((int)((sbyte)this.sourceBuffer[this.offset + 2]) << 16 | (int)this.sourceBuffer[this.offset + 1] << 8 | (int)this.sourceBuffer[this.offset]) / 8388608f;
				this.offset += 3;
				sampleRight = sampleLeft;
				return true;
			}
			sampleLeft = 0f;
			sampleRight = 0f;
			return false;
		}
	}
}
