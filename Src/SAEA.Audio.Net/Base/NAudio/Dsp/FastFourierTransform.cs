using System;

namespace SAEA.Audio.Base.NAudio.Dsp
{
	public static class FastFourierTransform
	{
		public static void FFT(bool forward, int m, Complex[] data)
		{
			int num = 1;
			for (int i = 0; i < m; i++)
			{
				num *= 2;
			}
			int num2 = num >> 1;
			int j = 0;
			for (int i = 0; i < num - 1; i++)
			{
				if (i < j)
				{
					float x = data[i].X;
					float y = data[i].Y;
					data[i].X = data[j].X;
					data[i].Y = data[j].Y;
					data[j].X = x;
					data[j].Y = y;
				}
				int k;
				for (k = num2; k <= j; k >>= 1)
				{
					j -= k;
				}
				j += k;
			}
			float num3 = -1f;
			float num4 = 0f;
			int num5 = 1;
			for (int l = 0; l < m; l++)
			{
				int num6 = num5;
				num5 <<= 1;
				float num7 = 1f;
				float num8 = 0f;
				for (j = 0; j < num6; j++)
				{
					for (int i = j; i < num; i += num5)
					{
						int num9 = i + num6;
						float num10 = num7 * data[num9].X - num8 * data[num9].Y;
						float num11 = num7 * data[num9].Y + num8 * data[num9].X;
						data[num9].X = data[i].X - num10;
						data[num9].Y = data[i].Y - num11;
						int expr_17C_cp_0_cp_1 = i;
						data[expr_17C_cp_0_cp_1].X = data[expr_17C_cp_0_cp_1].X + num10;
						int expr_18E_cp_0_cp_1 = i;
						data[expr_18E_cp_0_cp_1].Y = data[expr_18E_cp_0_cp_1].Y + num11;
					}
					float num12 = num7 * num3 - num8 * num4;
					num8 = num7 * num4 + num8 * num3;
					num7 = num12;
				}
				num4 = (float)Math.Sqrt((double)((1f - num3) / 2f));
				if (forward)
				{
					num4 = -num4;
				}
				num3 = (float)Math.Sqrt((double)((1f + num3) / 2f));
			}
			if (forward)
			{
				for (int i = 0; i < num; i++)
				{
					int expr_221_cp_0_cp_1 = i;
					data[expr_221_cp_0_cp_1].X = data[expr_221_cp_0_cp_1].X / (float)num;
					int expr_233_cp_0_cp_1 = i;
					data[expr_233_cp_0_cp_1].Y = data[expr_233_cp_0_cp_1].Y / (float)num;
				}
			}
		}

		public static double HammingWindow(int n, int frameSize)
		{
			return 0.54 - 0.46 * Math.Cos(6.2831853071795862 * (double)n / (double)(frameSize - 1));
		}

		public static double HannWindow(int n, int frameSize)
		{
			return 0.5 * (1.0 - Math.Cos(6.2831853071795862 * (double)n / (double)(frameSize - 1)));
		}

		public static double BlackmannHarrisWindow(int n, int frameSize)
		{
			return 0.35875 - 0.48829 * Math.Cos(6.2831853071795862 * (double)n / (double)(frameSize - 1)) + 0.14128 * Math.Cos(12.566370614359172 * (double)n / (double)(frameSize - 1)) - 0.01168 * Math.Cos(18.849555921538759 * (double)n / (double)(frameSize - 1));
		}
	}
}
