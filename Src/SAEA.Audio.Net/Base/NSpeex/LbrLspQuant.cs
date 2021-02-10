using System;

namespace SAEA.Audio.NSpeex
{
	internal class LbrLspQuant : LspQuant
	{
		public sealed override void Quant(float[] lsp, float[] qlsp, int order, Bits bits)
		{
			float[] array = new float[20];
			for (int i = 0; i < order; i++)
			{
				qlsp[i] = lsp[i];
			}
			array[0] = 1f / (qlsp[1] - qlsp[0]);
			array[order - 1] = 1f / (qlsp[order - 1] - qlsp[order - 2]);
			for (int i = 1; i < order - 1; i++)
			{
				float num = 1f / ((0.15f + qlsp[i] - qlsp[i - 1]) * (0.15f + qlsp[i] - qlsp[i - 1]));
				float num2 = 1f / ((0.15f + qlsp[i + 1] - qlsp[i]) * (0.15f + qlsp[i + 1] - qlsp[i]));
				array[i] = ((num > num2) ? num : num2);
			}
			for (int i = 0; i < order; i++)
			{
				qlsp[i] -= new float?((float)(0.25 * (double)i + 0.25)).Value;
			}
			for (int i = 0; i < order; i++)
			{
				qlsp[i] *= 256f;
			}
			int data = LspQuant.Lsp_quant(qlsp, 0, Codebook_Constants.cdbk_nb, 64, order);
			bits.Pack(data, 6);
			for (int i = 0; i < order; i++)
			{
				qlsp[i] *= 2f;
			}
			data = LspQuant.Lsp_weight_quant(qlsp, 0, array, 0, Codebook_Constants.cdbk_nb_low1, 64, 5);
			bits.Pack(data, 6);
			data = LspQuant.Lsp_weight_quant(qlsp, 5, array, 5, Codebook_Constants.cdbk_nb_high1, 64, 5);
			bits.Pack(data, 6);
			for (int i = 0; i < order; i++)
			{
				qlsp[i] *= new float?(0.0019531f).Value;
			}
			for (int i = 0; i < order; i++)
			{
				qlsp[i] = lsp[i] - qlsp[i];
			}
		}

		public sealed override void Unquant(float[] lsp, int order, Bits bits)
		{
			for (int i = 0; i < order; i++)
			{
				lsp[i] = 0.25f * (float)i + 0.25f;
			}
			base.UnpackPlus(lsp, Codebook_Constants.cdbk_nb, bits, 0.0039062f, 10, 0);
			base.UnpackPlus(lsp, Codebook_Constants.cdbk_nb_low1, bits, 0.0019531f, 5, 0);
			base.UnpackPlus(lsp, Codebook_Constants.cdbk_nb_high1, bits, 0.0019531f, 5, 5);
		}
	}
}
