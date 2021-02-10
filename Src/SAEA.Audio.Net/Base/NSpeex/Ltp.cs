using System;

namespace SAEA.Audio.NSpeex
{
	internal abstract class Ltp
	{
		public abstract int Quant(float[] target, float[] sw, int sws, float[] ak, float[] awk1, float[] awk2, float[] exc, int es, int start, int end, float pitch_coef, int p, int nsf, Bits bits, float[] exc2, int e2s, float[] r, int complexity);

		public abstract int Unquant(float[] exc, int es, int start, float pitch_coef, int nsf, float[] gain_val, Bits bits, int count_lost, int subframe_offset, float last_pitch_gain);

		protected internal static float Inner_prod(float[] x, int xs, float[] y, int ys, int len)
		{
			float num = 0f;
			float num2 = 0f;
			float num3 = 0f;
			float num4 = 0f;
			for (int i = 0; i < len; i += 4)
			{
				num += x[xs + i] * y[ys + i];
				num2 += x[xs + i + 1] * y[ys + i + 1];
				num3 += x[xs + i + 2] * y[ys + i + 2];
				num4 += x[xs + i + 3] * y[ys + i + 3];
			}
			return num + num2 + num3 + num4;
		}

		protected internal static void Open_loop_nbest_pitch(float[] sw, int swIdx, int start, int end, int len, int[] pitch, float[] gain, int N)
		{
			float[] array = new float[N];
			float[] array2 = new float[end - start + 1];
			float[] array3 = new float[end - start + 2];
			float[] array4 = new float[end - start + 1];
			for (int i = 0; i < N; i++)
			{
				array[i] = -1f;
				gain[i] = 0f;
				pitch[i] = start;
			}
			array3[0] = Ltp.Inner_prod(sw, swIdx - start, sw, swIdx - start, len);
			float num = Ltp.Inner_prod(sw, swIdx, sw, swIdx, len);
			for (int i = start; i <= end; i++)
			{
				array3[i - start + 1] = array3[i - start] + sw[swIdx - i - 1] * sw[swIdx - i - 1] - sw[swIdx - i + len - 1] * sw[swIdx - i + len - 1];
				if (array3[i - start + 1] < 1f)
				{
					array3[i - start + 1] = 1f;
				}
			}
			for (int i = start; i <= end; i++)
			{
				array2[i - start] = 0f;
				array4[i - start] = 0f;
			}
			for (int i = start; i <= end; i++)
			{
				array2[i - start] = Ltp.Inner_prod(sw, swIdx, sw, swIdx - i, len);
				array4[i - start] = array2[i - start] * array2[i - start] / (array3[i - start] + 1f);
			}
			for (int i = start; i <= end; i++)
			{
				if (array4[i - start] > array[N - 1])
				{
					float num2 = array2[i - start] / (array3[i - start] + 10f);
					float num3 = (float)Math.Sqrt((double)(num2 * array2[i - start] / (num + 10f)));
					if (num3 > num2)
					{
						num3 = num2;
					}
					if (num3 < 0f)
					{
						num3 = 0f;
					}
					for (int j = 0; j < N; j++)
					{
						if (array4[i - start] > array[j])
						{
							for (int k = N - 1; k > j; k--)
							{
								array[k] = array[k - 1];
								pitch[k] = pitch[k - 1];
								gain[k] = gain[k - 1];
							}
							array[j] = array4[i - start];
							pitch[j] = i;
							gain[j] = num3;
							break;
						}
					}
				}
			}
		}
	}
}
