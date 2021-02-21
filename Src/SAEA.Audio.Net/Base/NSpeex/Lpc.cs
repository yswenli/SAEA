using System;

namespace SAEA.Audio.Base.NSpeex
{
	internal class Lpc
	{
		public static float Wld(float[] lpc, float[] ac, float[] xref, int p)
		{
			float num = ac[0];
			if (ac[0] == 0f)
			{
				for (int i = 0; i < p; i++)
				{
					xref[i] = 0f;
				}
				return 0f;
			}
			for (int i = 0; i < p; i++)
			{
				float num2 = -ac[i + 1];
				int j;
				for (j = 0; j < i; j++)
				{
					num2 -= lpc[j] * ac[i - j];
				}
				num2 = (xref[i] = num2 / num);
				lpc[i] = num2;
				for (j = 0; j < i / 2; j++)
				{
					float num3 = lpc[j];
					lpc[j] += num2 * lpc[i - 1 - j];
					lpc[i - 1 - j] += num2 * num3;
				}
				if (i % 2 != 0)
				{
					lpc[j] += lpc[j] * num2;
				}
				num *= new float?(1f).Value - num2 * num2;
			}
			return num;
		}

		public static void Autocorr(float[] x, float[] ac, int lag, int n)
		{
			while (lag-- > 0)
			{
				int i = lag;
				float num = 0f;
				while (i < n)
				{
					num += x[i] * x[i - lag];
					i++;
				}
				ac[lag] = num;
			}
		}
	}
}
