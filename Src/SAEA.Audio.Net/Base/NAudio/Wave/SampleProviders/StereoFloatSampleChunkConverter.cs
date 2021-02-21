using SAEA.Audio.Base.NAudio.Utils;
using System;

namespace SAEA.Audio.Base.NAudio.Wave.SampleProviders
{
	internal class StereoFloatSampleChunkConverter : ISampleChunkConverter
	{
		private int sourceSample;

		private byte[] sourceBuffer;

		private WaveBuffer sourceWaveBuffer;

		private int sourceSamples;

		public bool Supports(WaveFormat waveFormat)
		{
			return waveFormat.Encoding == WaveFormatEncoding.IeeeFloat && waveFormat.Channels == 2;
		}

		public void LoadNextChunk(IWaveProvider source, int samplePairsRequired)
		{
			int num = samplePairsRequired * 8;
			this.sourceBuffer = BufferHelpers.Ensure(this.sourceBuffer, num);
			this.sourceWaveBuffer = new WaveBuffer(this.sourceBuffer);
			this.sourceSamples = source.Read(this.sourceBuffer, 0, num) / 4;
			this.sourceSample = 0;
		}

		public bool GetNextSample(out float sampleLeft, out float sampleRight)
		{
			if (this.sourceSample < this.sourceSamples)
			{
				float[] arg_2B_0 = this.sourceWaveBuffer.FloatBuffer;
				int num = this.sourceSample;
				this.sourceSample = num + 1;
				sampleLeft = arg_2B_0[num];
				float[] arg_4A_0 = this.sourceWaveBuffer.FloatBuffer;
				num = this.sourceSample;
				this.sourceSample = num + 1;
				sampleRight = arg_4A_0[num];
				return true;
			}
			sampleLeft = 0f;
			sampleRight = 0f;
			return false;
		}
	}
}
