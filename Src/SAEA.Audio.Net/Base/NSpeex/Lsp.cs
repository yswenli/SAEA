using System;

namespace SAEA.Audio.Base.NSpeex
{
	internal class Lsp
	{
		private float[] pw;

		public Lsp()
		{
			this.pw = new float[42];
		}

		public static float Cheb_poly_eva(float[] coef, float x, int m)
		{
			int num = m >> 1;
			float[] array = new float[num + 1];
			array[0] = 1f;
			array[1] = x;
			float num2 = coef[num] + coef[num - 1] * x;
			x *= 2f;
			for (int i = 2; i <= num; i++)
			{
				array[i] = x * array[i - 1] - array[i - 2];
				num2 += coef[num - i] * array[i];
			}
			return num2;
		}

		public static int Lpc2lsp(float[] a, int lpcrdr, float[] freq, int nb, float delta)
		{
			float num = 0f;
			int num2 = 0;
			int num3 = lpcrdr / 2;
			float[] array = new float[num3 + 1];
			float[] array2 = new float[num3 + 1];
			int num4 = 0;
			int num5 = 0;
			int num6 = num4;
			int num7 = num5;
			array2[num4++] = 1f;
			array[num5++] = 1f;
			for (int i = 1; i <= num3; i++)
			{
				array2[num4++] = a[i] + a[lpcrdr + 1 - i] - array2[num6++];
				array[num5++] = a[i] - a[lpcrdr + 1 - i] + array[num7++];
			}
			num4 = 0;
			num5 = 0;
			for (int i = 0; i < num3; i++)
			{
				array2[num4] = 2f * array2[num4];
				array[num5] = 2f * array[num5];
				num4++;
				num5++;
			}
			float num8 = 0f;
			float num9 = 1f;
			for (int j = 0; j < lpcrdr; j++)
			{
				float[] coef;
				if (j % 2 != 0)
				{
					coef = array;
				}
				else
				{
					coef = array2;
				}
				float num10 = Lsp.Cheb_poly_eva(coef, num9, lpcrdr);
				int num11 = 1;
				while (num11 == 1 && (double)num8 >= -1.0)
				{
					float num12 = (float)((double)delta * (1.0 - 0.9 * (double)num9 * (double)num9));
					if ((double)Math.Abs(num10) < 0.2)
					{
						num12 *= new float?(0.5f).Value;
					}
					num8 = num9 - num12;
					float num13 = Lsp.Cheb_poly_eva(coef, num8, lpcrdr);
					float num14 = num13;
					float num15 = num8;
					if ((double)(num13 * num10) < 0.0)
					{
						num2++;
						for (int k = 0; k <= nb; k++)
						{
							num = (num9 + num8) / 2f;
							float num16 = Lsp.Cheb_poly_eva(coef, num, lpcrdr);
							if ((double)(num16 * num10) > 0.0)
							{
								num10 = num16;
								num9 = num;
							}
							else
							{
								num8 = num;
							}
						}
						freq[j] = num;
						num9 = num;
						num11 = 0;
					}
					else
					{
						num10 = num14;
						num9 = num15;
					}
				}
			}
			return num2;
		}

		public void Lsp2lpc(float[] freq, float[] ak, int lpcrdr)
		{
			int num = 0;
			int num2 = lpcrdr / 2;
			for (int i = 0; i < 4 * num2 + 2; i++)
			{
				this.pw[i] = 0f;
			}
			float num3 = 1f;
			float num4 = 1f;
			for (int j = 0; j <= lpcrdr; j++)
			{
				int num5 = 0;
				int i = 0;
				float num9;
				float num10;
				while (i < num2)
				{
					int num6 = i * 4;
					int num7 = num6 + 1;
					int num8 = num7 + 1;
					num = num8 + 1;
					num9 = num3 - 2f * freq[num5] * this.pw[num6] + this.pw[num7];
					num10 = num4 - 2f * freq[num5 + 1] * this.pw[num8] + this.pw[num];
					this.pw[num7] = this.pw[num6];
					this.pw[num] = this.pw[num8];
					this.pw[num6] = num3;
					this.pw[num8] = num4;
					num3 = num9;
					num4 = num10;
					i++;
					num5 += 2;
				}
				num9 = num3 + this.pw[num + 1];
				num10 = num4 - this.pw[num + 2];
				ak[j] = (num9 + num10) * 0.5f;
				this.pw[num + 1] = num3;
				this.pw[num + 2] = num4;
				num3 = 0f;
				num4 = 0f;
			}
		}

		public static void Enforce_margin(float[] lsp, int len, float margin)
		{
			if (lsp[0] < margin)
			{
				lsp[0] = margin;
			}
			if (lsp[len - 1] > 3.14159274f - margin)
			{
				lsp[len - 1] = 3.14159274f - margin;
			}
			for (int i = 1; i < len - 1; i++)
			{
				if (lsp[i] < lsp[i - 1] + margin)
				{
					lsp[i] = lsp[i - 1] + margin;
				}
				if (lsp[i] > lsp[i + 1] - margin)
				{
					lsp[i] = 0.5f * (lsp[i] + lsp[i + 1] - margin);
				}
			}
		}
	}
}
