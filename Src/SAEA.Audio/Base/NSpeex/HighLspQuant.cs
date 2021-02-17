using System;

namespace SAEA.Audio.Base.NSpeex
{
	internal class HighLspQuant : LspQuant
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
				array[i] = Math.Max(1f / (qlsp[i] - qlsp[i - 1]), 1f / (qlsp[i + 1] - qlsp[i]));
			}
			for (int i = 0; i < order; i++)
			{
				qlsp[i] -= 0.3125f * (float)i + 0.75f;
			}
			for (int i = 0; i < order; i++)
			{
				qlsp[i] *= 256f;
			}
			int data = LspQuant.Lsp_quant(qlsp, 0, Codebook_Constants.high_lsp_cdbk, 64, order);
			bits.Pack(data, 6);
			for (int i = 0; i < order; i++)
			{
				qlsp[i] *= 2f;
			}
			data = LspQuant.Lsp_weight_quant(qlsp, 0, array, 0, Codebook_Constants.high_lsp_cdbk2, 64, order);
			bits.Pack(data, 6);
			for (int i = 0; i < order; i++)
			{
				qlsp[i] *= 0.0019531f;
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
				lsp[i] = 0.3125f * (float)i + 0.75f;
			}
			base.UnpackPlus(lsp, Codebook_Constants.high_lsp_cdbk, bits, 0.0039062f, order, 0);
			base.UnpackPlus(lsp, Codebook_Constants.high_lsp_cdbk2, bits, 0.0019531f, order, 0);
		}
	}
}
