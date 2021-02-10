using System;

namespace SAEA.Audio.NAudio.Wave.SampleProviders
{
	internal interface ISampleChunkConverter
	{
		bool Supports(WaveFormat format);

		void LoadNextChunk(IWaveProvider sourceProvider, int samplePairsRequired);

		bool GetNextSample(out float sampleLeft, out float sampleRight);
	}
}
