using System;

namespace SAEA.Audio.Base.NAudio.Dsp
{
	public class ImpulseResponseConvolution
	{
		public float[] Convolve(float[] input, float[] impulseResponse)
		{
			float[] array = new float[input.Length + impulseResponse.Length];
			for (int i = 0; i < array.Length; i++)
			{
				for (int j = 0; j < impulseResponse.Length; j++)
				{
					if (i >= j && i - j < input.Length)
					{
						array[i] += impulseResponse[j] * input[i - j];
					}
				}
			}
			this.Normalize(array);
			return array;
		}

		public void Normalize(float[] data)
		{
			float num = 0f;
			for (int i = 0; i < data.Length; i++)
			{
				num = Math.Max(num, Math.Abs(data[i]));
			}
			if ((double)num > 1.0)
			{
				for (int j = 0; j < data.Length; j++)
				{
					data[j] /= num;
				}
			}
		}
	}
}
