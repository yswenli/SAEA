using System;

namespace SAEA.Audio.Base.NSpeex
{
	internal class Filters
	{
		private int last_pitch;

		private float[] last_pitch_gain;

		private float smooth_gain;

		private float[] xx;

		public Filters()
		{
			this.last_pitch_gain = new float[3];
			this.xx = new float[1024];
			this.last_pitch = 0;
			this.last_pitch_gain[0] = (this.last_pitch_gain[1] = (this.last_pitch_gain[2] = 0f));
			this.smooth_gain = 1f;
		}

		public static void Bw_lpc(float gamma, float[] lpc_in, float[] lpc_out, int order)
		{
			float num = 1f;
			for (int i = 0; i < order + 1; i++)
			{
				lpc_out[i] = num * lpc_in[i];
				num *= gamma;
			}
		}

		public static void Filter_mem2(float[] x, int xs, float[] num, float[] den, int N, int ord, float[] mem, int ms)
		{
			for (int i = 0; i < N; i++)
			{
				float num2 = x[xs + i];
				x[xs + i] = num[0] * num2 + mem[ms];
				float num3 = x[xs + i];
				for (int j = 0; j < ord - 1; j++)
				{
					mem[ms + j] = mem[ms + j + 1] + num[j + 1] * num2 - den[j + 1] * num3;
				}
				mem[ms + ord - 1] = num[ord] * num2 - den[ord] * num3;
			}
		}

		public static void Filter_mem2(float[] x, int xs, float[] num, float[] den, float[] y, int ys, int N, int ord, float[] mem, int ms)
		{
			for (int i = 0; i < N; i++)
			{
				float num2 = x[xs + i];
				y[ys + i] = num[0] * num2 + mem[0];
				float num3 = y[ys + i];
				for (int j = 0; j < ord - 1; j++)
				{
					mem[ms + j] = mem[ms + j + 1] + num[j + 1] * num2 - den[j + 1] * num3;
				}
				mem[ms + ord - 1] = num[ord] * num2 - den[ord] * num3;
			}
		}

		public static void Iir_mem2(float[] x, int xs, float[] den, float[] y, int ys, int N, int ord, float[] mem)
		{
			for (int i = 0; i < N; i++)
			{
				y[ys + i] = x[xs + i] + mem[0];
				for (int j = 0; j < ord - 1; j++)
				{
					mem[j] = mem[j + 1] - den[j + 1] * y[ys + i];
				}
				mem[ord - 1] = -den[ord] * y[ys + i];
			}
		}

		public static void Fir_mem2(float[] x, int xs, float[] num, float[] y, int ys, int N, int ord, float[] mem)
		{
			for (int i = 0; i < N; i++)
			{
				float num2 = x[xs + i];
				y[ys + i] = num[0] * num2 + mem[0];
				for (int j = 0; j < ord - 1; j++)
				{
					mem[j] = mem[j + 1] + num[j + 1] * num2;
				}
				mem[ord - 1] = num[ord] * num2;
			}
		}

		public static void Syn_percep_zero(float[] xx_0, int xxs, float[] ak, float[] awk1, float[] awk2, float[] y, int N, int ord)
		{
			float[] array = new float[ord];
			Filters.Filter_mem2(xx_0, xxs, awk1, ak, y, 0, N, ord, array, 0);
			for (int i = 0; i < ord; i++)
			{
				array[i] = 0f;
			}
			Filters.Iir_mem2(y, 0, awk2, y, 0, N, ord, array);
		}

		public static void Residue_percep_zero(float[] xx_0, int xxs, float[] ak, float[] awk1, float[] awk2, float[] y, int N, int ord)
		{
			float[] array = new float[ord];
			Filters.Filter_mem2(xx_0, xxs, ak, awk1, y, 0, N, ord, array, 0);
			for (int i = 0; i < ord; i++)
			{
				array[i] = 0f;
			}
			Filters.Fir_mem2(y, 0, awk2, y, 0, N, ord, array);
		}

		public void Fir_mem_up(float[] x, float[] a, float[] y, int N, int M, float[] mem)
		{
			for (int i = 0; i < N / 2; i++)
			{
				this.xx[2 * i] = x[N / 2 - 1 - i];
			}
			for (int i = 0; i < M - 1; i += 2)
			{
				this.xx[N + i] = mem[i + 1];
			}
			for (int i = 0; i < N; i += 4)
			{
				float num4;
				float num3;
				float num2;
				float num = num2 = (num3 = (num4 = 0f));
				float num5 = this.xx[N - 4 - i];
				for (int j = 0; j < M; j += 4)
				{
					float num6 = a[j];
					float num7 = a[j + 1];
					float num8 = this.xx[N - 2 + j - i];
					num2 += num6 * num8;
					num += num7 * num8;
					num3 += num6 * num5;
					num4 += num7 * num5;
					num6 = a[j + 2];
					num7 = a[j + 3];
					num5 = this.xx[N + j - i];
					num2 += num6 * num5;
					num += num7 * num5;
					num3 += num6 * num8;
					num4 += num7 * num8;
				}
				y[i] = num2;
				y[i + 1] = num;
				y[i + 2] = num3;
				y[i + 3] = num4;
			}
			for (int i = 0; i < M - 1; i += 2)
			{
				mem[i + 1] = this.xx[i];
			}
		}

		public void Comb_filter(float[] exc, int esi, float[] new_exc, int nsi, int nsf, int pitch, float[] pitch_gain, float comb_gain)
		{
			float num = 0f;
			float num2 = 0f;
			int i;
			for (i = esi; i < esi + nsf; i++)
			{
				num += exc[i] * exc[i];
			}
			float num3 = 0.5f * Math.Abs(pitch_gain[0] + pitch_gain[1] + pitch_gain[2] + this.last_pitch_gain[0] + this.last_pitch_gain[1] + this.last_pitch_gain[2]);
			if (num3 > 1.3f)
			{
				comb_gain *= 1.3f / num3;
			}
			if (num3 < 0.5f)
			{
				comb_gain *= 2f * num3;
			}
			float num4 = 1f / (float)nsf;
			float num5 = 0f;
			i = 0;
			int num6 = esi;
			while (i < nsf)
			{
				num5 += num4;
				new_exc[nsi + i] = exc[num6] + comb_gain * num5 * (pitch_gain[0] * exc[num6 - pitch + 1] + pitch_gain[1] * exc[num6 - pitch] + pitch_gain[2] * exc[num6 - pitch - 1]) + comb_gain * (1f - num5) * (this.last_pitch_gain[0] * exc[num6 - this.last_pitch + 1] + this.last_pitch_gain[1] * exc[num6 - this.last_pitch] + this.last_pitch_gain[2] * exc[num6 - this.last_pitch - 1]);
				i++;
				num6++;
			}
			this.last_pitch_gain[0] = pitch_gain[0];
			this.last_pitch_gain[1] = pitch_gain[1];
			this.last_pitch_gain[2] = pitch_gain[2];
			this.last_pitch = pitch;
			for (i = nsi; i < nsi + nsf; i++)
			{
				num2 += new_exc[i] * new_exc[i];
			}
			float num7 = (float)Math.Sqrt((double)(num / (0.1f + num2)));
			if (num7 < 0.5f)
			{
				num7 = 0.5f;
			}
			if (num7 > 1f)
			{
				num7 = 1f;
			}
			for (i = nsi; i < nsi + nsf; i++)
			{
				this.smooth_gain = 0.96f * this.smooth_gain + 0.04f * num7;
				new_exc[i] *= this.smooth_gain;
			}
		}

		public static void Qmf_decomp(float[] xx_0, float[] aa, float[] y1, float[] y2, int N, int M, float[] mem)
		{
			float[] array = new float[M];
			float[] array2 = new float[N + M - 1];
			int num = M - 1;
			int num2 = M >> 1;
			int i;
			for (i = 0; i < M; i++)
			{
				array[M - i - 1] = aa[i];
			}
			for (i = 0; i < M - 1; i++)
			{
				array2[i] = mem[M - i - 2];
			}
			for (i = 0; i < N; i++)
			{
				array2[i + M - 1] = xx_0[i];
			}
			i = 0;
			int num3 = 0;
			while (i < N)
			{
				y1[num3] = 0f;
				y2[num3] = 0f;
				for (int j = 0; j < num2; j++)
				{
					y1[num3] += array[j] * (array2[i + j] + array2[num + i - j]);
					y2[num3] -= array[j] * (array2[i + j] - array2[num + i - j]);
					j++;
					y1[num3] += array[j] * (array2[i + j] + array2[num + i - j]);
					y2[num3] += array[j] * (array2[i + j] - array2[num + i - j]);
				}
				i += 2;
				num3++;
			}
			for (i = 0; i < M - 1; i++)
			{
				mem[i] = xx_0[N - i - 1];
			}
		}
	}
}
