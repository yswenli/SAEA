using System;

namespace SAEA.Audio.Base.NAudio.Wave.SampleProviders
{
	public class Pcm8BitToSampleProvider : SampleProviderConverterBase
	{
		public Pcm8BitToSampleProvider(IWaveProvider source) : base(source)
		{
		}

		public override int Read(float[] buffer, int offset, int count)
		{
			base.EnsureSourceBuffer(count);
			int num = this.source.Read(this.sourceBuffer, 0, count);
			int num2 = offset;
			for (int i = 0; i < num; i++)
			{
				buffer[num2++] = (float)this.sourceBuffer[i] / 128f - 1f;
			}
			return num;
		}
	}
}
