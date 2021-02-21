using SAEA.Audio.Base.NAudio.Utils;
using System;

namespace SAEA.Audio.Base.NAudio.Wave.SampleProviders
{
	public abstract class SampleProviderConverterBase : ISampleProvider
	{
		protected IWaveProvider source;

		private readonly WaveFormat waveFormat;

		protected byte[] sourceBuffer;

		public WaveFormat WaveFormat
		{
			get
			{
				return this.waveFormat;
			}
		}

		public SampleProviderConverterBase(IWaveProvider source)
		{
			this.source = source;
			this.waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, source.WaveFormat.Channels);
		}

		public abstract int Read(float[] buffer, int offset, int count);

		protected void EnsureSourceBuffer(int sourceBytesRequired)
		{
			this.sourceBuffer = BufferHelpers.Ensure(this.sourceBuffer, sourceBytesRequired);
		}
	}
}
