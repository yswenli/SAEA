using System;

namespace SAEA.Audio.Base.NSpeex
{
	internal class VQ
	{
		public static int Index(float ins0, float[] codebook, int entries)
		{
			float num = 0f;
			int result = 0;
			for (int i = 0; i < entries; i++)
			{
				float num2 = ins0 - codebook[i];
				num2 *= num2;
				if (i == 0 || num2 < num)
				{
					num = num2;
					result = i;
				}
			}
			return result;
		}

		public static int Index(float[] ins0, float[] codebook, int len, int entries)
		{
			int num = 0;
			float num2 = 0f;
			int result = 0;
			for (int i = 0; i < entries; i++)
			{
				float num3 = 0f;
				for (int j = 0; j < len; j++)
				{
					float num4 = ins0[j] - codebook[num++];
					num3 += num4 * num4;
				}
				if (i == 0 || num3 < num2)
				{
					num2 = num3;
					result = i;
				}
			}
			return result;
		}

		public static void Nbest(float[] ins0, int offset, float[] codebook, int len, int entries, float[] E, int N, int[] nbest, float[] best_dist)
		{
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < entries; i++)
			{
				float num3 = 0.5f * E[i];
				for (int j = 0; j < len; j++)
				{
					num3 -= ins0[offset + j] * codebook[num++];
				}
				if (i < N || num3 < best_dist[N - 1])
				{
					int num4 = N - 1;
					while (num4 >= 1 && (num4 > num2 || num3 < best_dist[num4 - 1]))
					{
						best_dist[num4] = best_dist[num4 - 1];
						nbest[num4] = nbest[num4 - 1];
						num4--;
					}
					best_dist[num4] = num3;
					nbest[num4] = i;
					num2++;
				}
			}
		}

		public static void Nbest_sign(float[] ins0, int offset, float[] codebook, int len, int entries, float[] E, int N, int[] nbest, float[] best_dist)
		{
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < entries; i++)
			{
				float num3 = 0f;
				for (int j = 0; j < len; j++)
				{
					num3 -= ins0[offset + j] * codebook[num++];
				}
				int num4;
				if (num3 > 0f)
				{
					num4 = 1;
					num3 = -num3;
				}
				else
				{
					num4 = 0;
				}
				num3 += 0.5f * E[i];
				if (i < N || num3 < best_dist[N - 1])
				{
					int num5 = N - 1;
					while (num5 >= 1 && (num5 > num2 || num3 < best_dist[num5 - 1]))
					{
						best_dist[num5] = best_dist[num5 - 1];
						nbest[num5] = nbest[num5 - 1];
						num5--;
					}
					best_dist[num5] = num3;
					nbest[num5] = i;
					num2++;
					if (num4 != 0)
					{
						nbest[num5] += entries;
					}
				}
			}
		}
	}
}
