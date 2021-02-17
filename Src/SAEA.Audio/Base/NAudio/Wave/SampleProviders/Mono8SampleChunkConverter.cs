using SAEA.Audio.Base.NAudio.Utils;
using System;

namespace SAEA.Audio.Base.NAudio.Wave.SampleProviders
{
	internal class Mono8SampleChunkConverter : ISampleChunkConverter
	{
		private int offset;

		private byte[] sourceBuffer;

		private int sourceBytes;

		public bool Supports(WaveFormat waveFormat)
		{
			return waveFormat.Encoding == WaveFormatEncoding.Pcm && waveFormat.BitsPerSample == 8 && waveFormat.Channels == 1;
		}

		public void LoadNextChunk(IWaveProvider source, int samplePairsRequired)
		{
			this.sourceBuffer = BufferHelpers.Ensure(this.sourceBuffer, samplePairsRequired);
			this.sourceBytes = source.Read(this.sourceBuffer, 0, samplePairsRequired);
			this.offset = 0;
		}

		public bool GetNextSample(out float sampleLeft, out float sampleRight)
		{
			if (this.offset < this.sourceBytes)
			{
				sampleLeft = (float)this.sourceBuffer[this.offset] / 256f;
				this.offset++;
				sampleRight = sampleLeft;
				return true;
			}
			sampleLeft = 0f;
			sampleRight = 0f;
			return false;
		}
	}
}
