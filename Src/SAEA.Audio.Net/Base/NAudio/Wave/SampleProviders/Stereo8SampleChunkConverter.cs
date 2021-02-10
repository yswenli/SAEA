using SAEA.Audio.NAudio.Utils;
using System;

namespace SAEA.Audio.NAudio.Wave.SampleProviders
{
	internal class Stereo8SampleChunkConverter : ISampleChunkConverter
	{
		private int offset;

		private byte[] sourceBuffer;

		private int sourceBytes;

		public bool Supports(WaveFormat waveFormat)
		{
			return waveFormat.Encoding == WaveFormatEncoding.Pcm && waveFormat.BitsPerSample == 8 && waveFormat.Channels == 2;
		}

		public void LoadNextChunk(IWaveProvider source, int samplePairsRequired)
		{
			int num = samplePairsRequired * 2;
			this.sourceBuffer = BufferHelpers.Ensure(this.sourceBuffer, num);
			this.sourceBytes = source.Read(this.sourceBuffer, 0, num);
			this.offset = 0;
		}

		public bool GetNextSample(out float sampleLeft, out float sampleRight)
		{
			if (this.offset < this.sourceBytes)
			{
				byte[] arg_26_0 = this.sourceBuffer;
				int num = this.offset;
				this.offset = num + 1;
				sampleLeft = arg_26_0[num] / 256f;
				byte[] arg_47_0 = this.sourceBuffer;
				num = this.offset;
				this.offset = num + 1;
				sampleRight = arg_47_0[num] / 256f;
				return true;
			}
			sampleLeft = 0f;
			sampleRight = 0f;
			return false;
		}
	}
}
