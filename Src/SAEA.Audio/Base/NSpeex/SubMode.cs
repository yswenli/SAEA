using System;

namespace SAEA.Audio.Base.NSpeex
{
	internal class SubMode
	{
		private readonly int lbrPitch;

		private readonly int forcedPitchGain;

		private readonly int haveSubframeGain;

		private readonly int doubleCodebook;

		private readonly LspQuant lsqQuant;

		private readonly Ltp ltp;

		private readonly CodebookSearch innovation;

		private readonly float lpcEnhK1;

		private readonly float lpcEnhK2;

		private readonly float combGain;

		private readonly int bitsPerFrame;

		public int LbrPitch
		{
			get
			{
				return this.lbrPitch;
			}
		}

		public int ForcedPitchGain
		{
			get
			{
				return this.forcedPitchGain;
			}
		}

		public int HaveSubframeGain
		{
			get
			{
				return this.haveSubframeGain;
			}
		}

		public int DoubleCodebook
		{
			get
			{
				return this.doubleCodebook;
			}
		}

		public LspQuant LsqQuant
		{
			get
			{
				return this.lsqQuant;
			}
		}

		public Ltp Ltp
		{
			get
			{
				return this.ltp;
			}
		}

		public CodebookSearch Innovation
		{
			get
			{
				return this.innovation;
			}
		}

		public float LpcEnhK1
		{
			get
			{
				return this.lpcEnhK1;
			}
		}

		public float LpcEnhK2
		{
			get
			{
				return this.lpcEnhK2;
			}
		}

		public float CombGain
		{
			get
			{
				return this.combGain;
			}
		}

		public int BitsPerFrame
		{
			get
			{
				return this.bitsPerFrame;
			}
		}

		public SubMode(int lbrPitch, int forcedPitchGain, int haveSubframeGain, int doubleCodebook, LspQuant lspQuant, Ltp ltp, CodebookSearch innovation, float lpcEnhK1, float lpcEnhK2, float combGain, int bitsPerFrame)
		{
			this.lbrPitch = lbrPitch;
			this.forcedPitchGain = forcedPitchGain;
			this.haveSubframeGain = haveSubframeGain;
			this.doubleCodebook = doubleCodebook;
			this.lsqQuant = lspQuant;
			this.ltp = ltp;
			this.innovation = innovation;
			this.lpcEnhK1 = lpcEnhK1;
			this.lpcEnhK2 = lpcEnhK2;
			this.combGain = combGain;
			this.bitsPerFrame = bitsPerFrame;
		}
	}
}
