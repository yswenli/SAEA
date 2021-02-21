using System;

namespace SAEA.Audio.Base.NSpeex
{
	internal class Ltp3Tap : Ltp
	{
		private float[] gain;

		private int[] gain_cdbk;

		private int gain_bits;

		private int pitch_bits;

		private float[][] e;

		public Ltp3Tap(int[] gain_cdbk_0, int gain_bits_1, int pitch_bits_2)
		{
			this.gain = new float[3];
			this.gain_cdbk = gain_cdbk_0;
			this.gain_bits = gain_bits_1;
			this.pitch_bits = pitch_bits_2;
			this.e = this.CreateJaggedArray<float>(3, 128);
		}

		private T[][] CreateJaggedArray<T>(int dim1, int dim2)
		{
			T[][] array = new T[dim1][];
			for (int i = 0; i < dim1; i++)
			{
				array[i] = new T[dim2];
				Array.Clear(array[i], 0, dim2);
			}
			return array;
		}

		public sealed override int Quant(float[] target, float[] sw, int sws, float[] ak, float[] awk1, float[] awk2, float[] exc, int es, int start, int end, float pitch_coef, int p, int nsf, Bits bits, float[] exc2, int e2s, float[] r, int complexity)
		{
			int[] array = new int[1];
			int num = 0;
			int data = 0;
			int num2 = 0;
			float num3 = -1f;
			int num4 = complexity;
			if (num4 > 10)
			{
				num4 = 10;
			}
			int[] array2 = new int[num4];
			float[] array3 = new float[num4];
			if (num4 == 0 || end < start)
			{
				bits.Pack(0, this.pitch_bits);
				bits.Pack(0, this.gain_bits);
				for (int i = 0; i < nsf; i++)
				{
					exc[es + i] = 0f;
				}
				return start;
			}
			float[] array4 = new float[nsf];
			if (num4 > end - start + 1)
			{
				num4 = end - start + 1;
			}
			Ltp.Open_loop_nbest_pitch(sw, sws, start, end, nsf, array2, array3, num4);
			for (int i = 0; i < num4; i++)
			{
				num = array2[i];
				for (int j = 0; j < nsf; j++)
				{
					exc[es + j] = 0f;
				}
				float num5 = this.Pitch_gain_search_3tap(target, ak, awk1, awk2, exc, es, num, p, nsf, bits, exc2, e2s, r, array);
				if (num5 < num3 || num3 < 0f)
				{
					for (int j = 0; j < nsf; j++)
					{
						array4[j] = exc[es + j];
					}
					num3 = num5;
					num2 = num;
					data = array[0];
				}
			}
			bits.Pack(num2 - start, this.pitch_bits);
			bits.Pack(data, this.gain_bits);
			for (int i = 0; i < nsf; i++)
			{
				exc[es + i] = array4[i];
			}
			return num;
		}

		public sealed override int Unquant(float[] exc, int es, int start, float pitch_coef, int nsf, float[] gain_val, Bits bits, int count_lost, int subframe_offset, float last_pitch_gain)
		{
			int num = bits.Unpack(this.pitch_bits);
			num += start;
			int num2 = bits.Unpack(this.gain_bits);
			this.gain[0] = 0.015625f * (float)this.gain_cdbk[num2 * 3] + 0.5f;
			this.gain[1] = 0.015625f * (float)this.gain_cdbk[num2 * 3 + 1] + 0.5f;
			this.gain[2] = 0.015625f * (float)this.gain_cdbk[num2 * 3 + 2] + 0.5f;
			if (count_lost != 0 && num > subframe_offset)
			{
				float num3 = Math.Abs(this.gain[1]);
				float num4 = (count_lost < 4) ? last_pitch_gain : (0.4f * last_pitch_gain);
				if (num4 > 0.95f)
				{
					num4 = 0.95f;
				}
				if (this.gain[0] > 0f)
				{
					num3 += this.gain[0];
				}
				else
				{
					num3 -= 0.5f * this.gain[0];
				}
				if (this.gain[2] > 0f)
				{
					num3 += this.gain[2];
				}
				else
				{
					num3 -= 0.5f * this.gain[0];
				}
				if (num3 > num4)
				{
					float num5 = num4 / num3;
					for (int i = 0; i < 3; i++)
					{
						this.gain[i] *= num5;
					}
				}
			}
			gain_val[0] = this.gain[0];
			gain_val[1] = this.gain[1];
			gain_val[2] = this.gain[2];
			for (int i = 0; i < 3; i++)
			{
				int num6 = num + 1 - i;
				int num7 = nsf;
				if (num7 > num6)
				{
					num7 = num6;
				}
				int num8 = nsf;
				if (num8 > num6 + num)
				{
					num8 = num6 + num;
				}
				for (int j = 0; j < num7; j++)
				{
					this.e[i][j] = exc[es + j - num6];
				}
				for (int j = num7; j < num8; j++)
				{
					this.e[i][j] = exc[es + j - num6 - num];
				}
				for (int j = num8; j < nsf; j++)
				{
					this.e[i][j] = 0f;
				}
			}
			for (int i = 0; i < nsf; i++)
			{
				exc[es + i] = this.gain[0] * this.e[2][i] + this.gain[1] * this.e[1][i] + this.gain[2] * this.e[0][i];
			}
			return num;
		}

		private float Pitch_gain_search_3tap(float[] target, float[] ak, float[] awk1, float[] awk2, float[] exc, int es, int pitch, int p, int nsf, Bits bits, float[] exc2, int e2s, float[] r, int[] cdbk_index)
		{
			float[] array = new float[3];
			float[][] array2 = this.CreateJaggedArray<float>(3, 3);
			int num = 1 << this.gain_bits;
			float[][] array3 = this.CreateJaggedArray<float>(3, nsf);
			this.e = this.CreateJaggedArray<float>(3, nsf);
			for (int i = 2; i >= 0; i--)
			{
				int num2 = pitch + 1 - i;
				for (int j = 0; j < nsf; j++)
				{
					if (j - num2 < 0)
					{
						this.e[i][j] = exc2[e2s + j - num2];
					}
					else if (j - num2 - pitch < 0)
					{
						this.e[i][j] = exc2[e2s + j - num2 - pitch];
					}
					else
					{
						this.e[i][j] = 0f;
					}
				}
				if (i == 2)
				{
					Filters.Syn_percep_zero(this.e[i], 0, ak, awk1, awk2, array3[i], nsf, p);
				}
				else
				{
					for (int j = 0; j < nsf - 1; j++)
					{
						array3[i][j + 1] = array3[i + 1][j];
					}
					array3[i][0] = 0f;
					for (int j = 0; j < nsf; j++)
					{
						array3[i][j] += this.e[i][0] * r[j];
					}
				}
			}
			for (int i = 0; i < 3; i++)
			{
				array[i] = Ltp.Inner_prod(array3[i], 0, target, 0, nsf);
			}
			for (int i = 0; i < 3; i++)
			{
				for (int j = 0; j <= i; j++)
				{
					array2[i][j] = (array2[j][i] = Ltp.Inner_prod(array3[i], 0, array3[j], 0, nsf));
				}
			}
			float[] array4 = new float[9];
			int num3 = 0;
			float num4 = 0f;
			array4[0] = array[2];
			array4[1] = array[1];
			array4[2] = array[0];
			array4[3] = array2[1][2];
			array4[4] = array2[0][1];
			array4[5] = array2[0][2];
			array4[6] = array2[2][2];
			array4[7] = array2[1][1];
			array4[8] = array2[0][0];
			for (int i = 0; i < num; i++)
			{
				float num5 = 0f;
				int num6 = 3 * i;
				float num7 = 0.015625f * (float)this.gain_cdbk[num6] + 0.5f;
				float num8 = 0.015625f * (float)this.gain_cdbk[num6 + 1] + 0.5f;
				float num9 = 0.015625f * (float)this.gain_cdbk[num6 + 2] + 0.5f;
				num5 += array4[0] * num7;
				num5 += array4[1] * num8;
				num5 += array4[2] * num9;
				num5 -= array4[3] * num7 * num8;
				num5 -= array4[4] * num9 * num8;
				num5 -= array4[5] * num9 * num7;
				num5 -= 0.5f * array4[6] * num7 * num7;
				num5 -= 0.5f * array4[7] * num8 * num8;
				num5 -= 0.5f * array4[8] * num9 * num9;
				if (num5 > num4 || i == 0)
				{
					num4 = num5;
					num3 = i;
				}
			}
			this.gain[0] = 0.015625f * (float)this.gain_cdbk[num3 * 3] + 0.5f;
			this.gain[1] = 0.015625f * (float)this.gain_cdbk[num3 * 3 + 1] + 0.5f;
			this.gain[2] = 0.015625f * (float)this.gain_cdbk[num3 * 3 + 2] + 0.5f;
			cdbk_index[0] = num3;
			for (int i = 0; i < nsf; i++)
			{
				exc[es + i] = this.gain[0] * this.e[2][i] + this.gain[1] * this.e[1][i] + this.gain[2] * this.e[0][i];
			}
			float num10 = 0f;
			float num11 = 0f;
			for (int i = 0; i < nsf; i++)
			{
				num10 += target[i] * target[i];
			}
			for (int i = 0; i < nsf; i++)
			{
				num11 += (target[i] - this.gain[2] * array3[0][i] - this.gain[1] * array3[1][i] - this.gain[0] * array3[2][i]) * (target[i] - this.gain[2] * array3[0][i] - this.gain[1] * array3[1][i] - this.gain[0] * array3[2][i]);
			}
			return num11;
		}
	}
}
