using System;

namespace SAEA.Audio.Base.NAudio.Wave.SampleProviders
{
	public class SampleToWaveProvider : IWaveProvider
	{
		private readonly ISampleProvider source;

		public WaveFormat WaveFormat
		{
			get
			{
				return this.source.WaveFormat;
			}
		}

		public SampleToWaveProvider(ISampleProvider source)
		{
			if (source.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
			{
				throw new ArgumentException("Must be already floating point");
			}
			this.source = source;
		}

		public int Read(byte[] buffer, int offset, int count)
		{
			int count2 = count / 4;
			WaveBuffer waveBuffer = new WaveBuffer(buffer);
			return this.source.Read(waveBuffer.FloatBuffer, offset / 4, count2) * 4;
		}
	}
}
