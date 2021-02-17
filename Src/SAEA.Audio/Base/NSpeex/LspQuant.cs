using System;

namespace SAEA.Audio.Base.NSpeex
{
	internal abstract class LspQuant
	{
		protected const int MAX_LSP_SIZE = 20;

		protected internal LspQuant()
		{
		}

		public abstract void Quant(float[] lsp, float[] qlsp, int order, Bits bits);

		public abstract void Unquant(float[] lsp, int order, Bits bits);

		protected internal void UnpackPlus(float[] lsp, int[] tab, Bits bits, float k, int ti, int li)
		{
			int num = bits.Unpack(6);
			for (int i = 0; i < ti; i++)
			{
				lsp[i + li] += k * (float)tab[num * ti + i];
			}
		}

		protected internal static int Lsp_quant(float[] x, int xs, int[] cdbk, int nbVec, int nbDim)
		{
			float num = 0f;
			int num2 = 0;
			int num3 = 0;
			for (int i = 0; i < nbVec; i++)
			{
				float num4 = 0f;
				for (int j = 0; j < nbDim; j++)
				{
					float num5 = x[xs + j] - (float)cdbk[num3++];
					num4 += num5 * num5;
				}
				if (num4 < num || i == 0)
				{
					num = num4;
					num2 = i;
				}
			}
			for (int j = 0; j < nbDim; j++)
			{
				x[xs + j] -= (float)cdbk[num2 * nbDim + j];
			}
			return num2;
		}

		protected internal static int Lsp_weight_quant(float[] x, int xs, float[] weight, int ws, int[] cdbk, int nbVec, int nbDim)
		{
			float num = 0f;
			int num2 = 0;
			int num3 = 0;
			for (int i = 0; i < nbVec; i++)
			{
				float num4 = 0f;
				for (int j = 0; j < nbDim; j++)
				{
					float num5 = x[xs + j] - (float)cdbk[num3++];
					num4 += weight[ws + j] * num5 * num5;
				}
				if (num4 < num || i == 0)
				{
					num = num4;
					num2 = i;
				}
			}
			for (int j = 0; j < nbDim; j++)
			{
				x[xs + j] -= (float)cdbk[num2 * nbDim + j];
			}
			return num2;
		}
	}
}
