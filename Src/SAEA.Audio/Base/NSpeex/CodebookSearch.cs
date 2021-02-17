using System;

namespace SAEA.Audio.Base.NSpeex
{
	internal abstract class CodebookSearch
	{
		public abstract void Quantify(float[] target, float[] ak, float[] awk1, float[] awk2, int p, int nsf, float[] exc, int es, float[] r, Bits bits, int complexity);

		public abstract void Unquantify(float[] exc, int es, int nsf, Bits bits);
	}
}
