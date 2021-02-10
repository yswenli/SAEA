using SAEA.Audio.NAudio.Utils;
using System;

namespace SAEA.Audio.NAudio.Wave.SampleProviders
{
	internal class Stereo16SampleChunkConverter : ISampleChunkConverter
	{
		private int sourceSample;

		private byte[] sourceBuffer;

		private WaveBuffer sourceWaveBuffer;

		private int sourceSamples;

		public bool Supports(WaveFormat waveFormat)
		{
			return waveFormat.Encoding == WaveFormatEncoding.Pcm && waveFormat.BitsPerSample == 16 && waveFormat.Channels == 2;
		}

		public void LoadNextChunk(IWaveProvider source, int samplePairsRequired)
		{
			int num = samplePairsRequired * 4;
			this.sourceBuffer = BufferHelpers.Ensure(this.sourceBuffer, num);
			this.sourceWaveBuffer = new WaveBuffer(this.sourceBuffer);
			this.sourceSamples = source.Read(this.sourceBuffer, 0, num) / 2;
			this.sourceSample = 0;
		}

		public bool GetNextSample(out float sampleLeft, out float sampleRight)
		{
			if (this.sourceSample < this.sourceSamples)
			{
				short[] arg_2B_0 = this.sourceWaveBuffer.ShortBuffer;
				int num = this.sourceSample;
				this.sourceSample = num + 1;
				sampleLeft = arg_2B_0[num] / 32768f;
				short[] arg_51_0 = this.sourceWaveBuffer.ShortBuffer;
				num = this.sourceSample;
				this.sourceSample = num + 1;
				sampleRight = arg_51_0[num] / 32768f;
				return true;
			}
			sampleLeft = 0f;
			sampleRight = 0f;
			return false;
		}
	}
}
