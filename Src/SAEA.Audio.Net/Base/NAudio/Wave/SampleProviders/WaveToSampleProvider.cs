using System;

namespace SAEA.Audio.Base.NAudio.Wave.SampleProviders
{
	public class WaveToSampleProvider : SampleProviderConverterBase
	{
		public WaveToSampleProvider(IWaveProvider source) : base(source)
		{
			if (source.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
			{
				throw new ArgumentException("Must be already floating point");
			}
		}

		public override int Read(float[] buffer, int offset, int count)
		{
			int num = count * 4;
			base.EnsureSourceBuffer(num);
			int num2 = this.source.Read(this.sourceBuffer, 0, num);
			int result = num2 / 4;
			int num3 = offset;
			for (int i = 0; i < num2; i += 4)
			{
				buffer[num3++] = BitConverter.ToSingle(this.sourceBuffer, i);
			}
			return result;
		}
	}
}
