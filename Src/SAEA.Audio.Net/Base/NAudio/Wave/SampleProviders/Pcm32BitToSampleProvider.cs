using System;

namespace SAEA.Audio.Base.NAudio.Wave.SampleProviders
{
	public class Pcm32BitToSampleProvider : SampleProviderConverterBase
	{
		public Pcm32BitToSampleProvider(IWaveProvider source) : base(source)
		{
		}

		public override int Read(float[] buffer, int offset, int count)
		{
			int num = count * 4;
			base.EnsureSourceBuffer(num);
			int num2 = this.source.Read(this.sourceBuffer, 0, num);
			int num3 = offset;
			for (int i = 0; i < num2; i += 4)
			{
				buffer[num3++] = (float)((int)((sbyte)this.sourceBuffer[i + 3]) << 24 | (int)this.sourceBuffer[i + 2] << 16 | (int)this.sourceBuffer[i + 1] << 8 | (int)this.sourceBuffer[i]) / 2.14748365E+09f;
			}
			return num2 / 4;
		}
	}
}
