using System;

namespace SAEA.Audio.NSpeex
{
	internal class SbCodec : NbCodec
	{
		public const int SB_SUBMODES = 8;

		public const int SB_SUBMODE_BITS = 3;

		public const int QMF_ORDER = 64;

		public static readonly int[] SB_FRAME_SIZE = new int[]
		{
			4,
			36,
			112,
			192,
			352,
			-1,
			-1,
			-1
		};

		protected internal int fullFrameSize;

		protected internal float foldingGain;

		protected internal float[] high;

		protected internal float[] y0;

		protected internal float[] y1;

		protected internal float[] x0d;

		protected internal float[] g0_mem;

		protected internal float[] g1_mem;

		public override int FrameSize
		{
			get
			{
				return this.fullFrameSize;
			}
		}

		public bool Dtx
		{
			get
			{
				return this.dtx_enabled != 0;
			}
		}

		public override float[] Exc
		{
			get
			{
				float[] array = new float[this.fullFrameSize];
				for (int i = 0; i < this.frameSize; i++)
				{
					array[2 * i] = 2f * this.excBuf[this.excIdx + i];
				}
				return array;
			}
		}

		public override float[] Innov
		{
			get
			{
				return this.Exc;
			}
		}

		public SbCodec(bool ultraWide)
		{
			if (ultraWide)
			{
				this.submodes = SbCodec.BuildUwbSubModes();
				this.submodeID = 1;
				return;
			}
			this.submodes = SbCodec.BuildWbSubModes();
			this.submodeID = 3;
		}

		protected virtual void Init(int frameSize, int subframeSize, int lpcSize, int bufSize, float foldingGain_0)
		{
			base.Init(frameSize, subframeSize, lpcSize, bufSize);
			this.fullFrameSize = 2 * frameSize;
			this.foldingGain = foldingGain_0;
			this.lag_factor = 0.002f;
			this.high = new float[this.fullFrameSize];
			this.y0 = new float[this.fullFrameSize];
			this.y1 = new float[this.fullFrameSize];
			this.x0d = new float[frameSize];
			this.g0_mem = new float[64];
			this.g1_mem = new float[64];
		}

		protected internal static SubMode[] BuildWbSubModes()
		{
			HighLspQuant lspQuant = new HighLspQuant();
			SplitShapeSearch innovation = new SplitShapeSearch(40, 10, 4, Codebook_Constants.hexc_10_32_table, 5, 0);
			SplitShapeSearch innovation2 = new SplitShapeSearch(40, 8, 5, Codebook_Constants.hexc_table, 7, 1);
			SubMode[] array = new SubMode[8];
			array[1] = new SubMode(0, 0, 1, 0, lspQuant, null, null, 0.75f, 0.75f, -1f, 36);
			array[2] = new SubMode(0, 0, 1, 0, lspQuant, null, innovation, 0.85f, 0.6f, -1f, 112);
			array[3] = new SubMode(0, 0, 1, 0, lspQuant, null, innovation2, 0.75f, 0.7f, -1f, 192);
			array[4] = new SubMode(0, 0, 1, 1, lspQuant, null, innovation2, 0.75f, 0.75f, -1f, 352);
			return array;
		}

		protected internal static SubMode[] BuildUwbSubModes()
		{
			HighLspQuant lspQuant = new HighLspQuant();
			SubMode[] array = new SubMode[8];
			array[1] = new SubMode(0, 0, 1, 0, lspQuant, null, null, 0.75f, 0.75f, -1f, 2);
			return array;
		}
	}
}
