using System;

namespace SAEA.Audio.NAudio.Wave.SampleProviders
{
	public class WaveToSampleProvider64 : SampleProviderConverterBase
	{
		public WaveToSampleProvider64(IWaveProvider source) : base(source)
		{
			if (source.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
			{
				throw new ArgumentException("Must be already floating point");
			}
		}

		public override int Read(float[] buffer, int offset, int count)
		{
			int num = count * 8;
			base.EnsureSourceBuffer(num);
			int num2 = this.source.Read(this.sourceBuffer, 0, num);
			int result = num2 / 8;
			int num3 = offset;
			for (int i = 0; i < num2; i += 8)
			{
				long value = BitConverter.ToInt64(this.sourceBuffer, i);
				buffer[num3++] = (float)BitConverter.Int64BitsToDouble(value);
			}
			return result;
		}
	}
}
