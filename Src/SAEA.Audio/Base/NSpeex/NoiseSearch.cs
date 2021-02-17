using System;

namespace SAEA.Audio.Base.NSpeex
{
	internal class NoiseSearch : CodebookSearch
	{
		public sealed override void Quantify(float[] target, float[] ak, float[] awk1, float[] awk2, int p, int nsf, float[] exc, int es, float[] r, Bits bits, int complexity)
		{
			float[] array = new float[nsf];
			Filters.Residue_percep_zero(target, 0, ak, awk1, awk2, array, nsf, p);
			for (int i = 0; i < nsf; i++)
			{
				exc[es + i] += array[i];
			}
			for (int i = 0; i < nsf; i++)
			{
				target[i] = 0f;
			}
		}

		public sealed override void Unquantify(float[] exc, int es, int nsf, Bits bits)
		{
			for (int i = 0; i < nsf; i++)
			{
				exc[es + i] += (float)(3.0 * (new Random().NextDouble() - 0.5));
			}
		}
	}
}
