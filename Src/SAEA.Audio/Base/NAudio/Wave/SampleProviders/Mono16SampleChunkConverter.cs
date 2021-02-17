using SAEA.Audio.Base.NAudio.Utils;
using System;

namespace SAEA.Audio.Base.NAudio.Wave.SampleProviders
{
	internal class Mono16SampleChunkConverter : ISampleChunkConverter
	{
		private int sourceSample;

		private byte[] sourceBuffer;

		private WaveBuffer sourceWaveBuffer;

		private int sourceSamples;

		public bool Supports(WaveFormat waveFormat)
		{
			return waveFormat.Encoding == WaveFormatEncoding.Pcm && waveFormat.BitsPerSample == 16 && waveFormat.Channels == 1;
		}

		public void LoadNextChunk(IWaveProvider source, int samplePairsRequired)
		{
			int num = samplePairsRequired * 2;
			this.sourceSample = 0;
			this.sourceBuffer = BufferHelpers.Ensure(this.sourceBuffer, num);
			this.sourceWaveBuffer = new WaveBuffer(this.sourceBuffer);
			this.sourceSamples = source.Read(this.sourceBuffer, 0, num) / 2;
		}

		public bool GetNextSample(out float sampleLeft, out float sampleRight)
		{
			if (this.sourceSample < this.sourceSamples)
			{
				short[] arg_2B_0 = this.sourceWaveBuffer.ShortBuffer;
				int num = this.sourceSample;
				this.sourceSample = num + 1;
				sampleLeft = arg_2B_0[num] / 32768f;
				sampleRight = sampleLeft;
				return true;
			}
			sampleLeft = 0f;
			sampleRight = 0f;
			return false;
		}
	}
}
