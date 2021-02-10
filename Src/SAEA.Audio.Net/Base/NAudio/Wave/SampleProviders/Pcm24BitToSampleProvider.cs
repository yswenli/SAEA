using System;

namespace SAEA.Audio.NAudio.Wave.SampleProviders
{
	public class Pcm24BitToSampleProvider : SampleProviderConverterBase
	{
		public Pcm24BitToSampleProvider(IWaveProvider source) : base(source)
		{
		}

		public override int Read(float[] buffer, int offset, int count)
		{
			int num = count * 3;
			base.EnsureSourceBuffer(num);
			int num2 = this.source.Read(this.sourceBuffer, 0, num);
			int num3 = offset;
			for (int i = 0; i < num2; i += 3)
			{
				buffer[num3++] = (float)((int)((sbyte)this.sourceBuffer[i + 2]) << 16 | (int)this.sourceBuffer[i + 1] << 8 | (int)this.sourceBuffer[i]) / 8388608f;
			}
			return num2 / 3;
		}
	}
}
