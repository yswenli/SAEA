using System;

namespace SAEA.Audio.Base.NSpeex
{
	internal class NbCodec
	{
		protected const float VERY_SMALL = 0f;

		protected const int NB_SUBMODES = 16;

		protected const int NB_SUBMODE_BITS = 4;

		protected static readonly int[] NB_FRAME_SIZE = new int[]
		{
			5,
			43,
			119,
			160,
			220,
			300,
			364,
			492,
			79,
			1,
			1,
			1,
			1,
			1,
			1,
			1
		};

		protected static readonly float[] exc_gain_quant_scal1 = new float[]
		{
			-0.35f,
			0.05f
		};

		protected static readonly float[] exc_gain_quant_scal3 = new float[]
		{
			-2.79475f,
			-1.81066f,
			-1.16985f,
			-0.848119f,
			-0.58719f,
			-0.329818f,
			-0.063266f,
			0.282826f
		};

		protected internal Lsp m_lsp;

		protected internal Filters filters;

		protected internal SubMode[] submodes;

		protected internal int submodeID;

		protected internal int first;

		protected internal int frameSize;

		protected internal int subframeSize;

		protected internal int nbSubframes;

		protected internal int windowSize;

		protected internal int lpcSize;

		protected internal int bufSize;

		protected internal int min_pitch;

		protected internal int max_pitch;

		protected internal float gamma1;

		protected internal float gamma2;

		protected internal float lag_factor;

		protected internal float lpc_floor;

		protected internal float preemph;

		protected internal float pre_mem;

		protected internal float[] frmBuf;

		protected internal int frmIdx;

		protected internal float[] excBuf;

		protected internal int excIdx;

		protected internal float[] innov;

		protected internal float[] lpc;

		protected internal float[] qlsp;

		protected internal float[] old_qlsp;

		protected internal float[] interp_qlsp;

		protected internal float[] interp_qlpc;

		protected internal float[] mem_sp;

		protected internal float[] pi_gain;

		protected internal float[] awk1;

		protected internal float[] awk2;

		protected internal float[] awk3;

		protected internal float voc_m1;

		protected internal float voc_m2;

		protected internal float voc_mean;

		protected internal int voc_offset;

		protected internal int dtx_enabled;

		public virtual int FrameSize
		{
			get
			{
				return this.frameSize;
			}
		}

		public float[] PiGain
		{
			get
			{
				return this.pi_gain;
			}
		}

		public virtual float[] Exc
		{
			get
			{
				float[] array = new float[this.frameSize];
				Array.Copy(this.excBuf, this.excIdx, array, 0, this.frameSize);
				return array;
			}
		}

		public virtual float[] Innov
		{
			get
			{
				return this.innov;
			}
		}

		public NbCodec()
		{
			this.m_lsp = new Lsp();
			this.filters = new Filters();
			this.Nbinit();
		}

		private void Nbinit()
		{
			this.submodes = NbCodec.BuildNbSubModes();
			this.submodeID = 5;
			this.Init(160, 40, 10, 640);
		}

		protected virtual void Init(int frameSize, int subframeSize, int lpcSize, int bufSize)
		{
			this.first = 1;
			this.frameSize = frameSize;
			this.windowSize = frameSize * 3 / 2;
			this.subframeSize = subframeSize;
			this.nbSubframes = frameSize / subframeSize;
			this.lpcSize = lpcSize;
			this.bufSize = bufSize;
			this.min_pitch = 17;
			this.max_pitch = 144;
			this.preemph = 0f;
			this.pre_mem = 0f;
			this.gamma1 = 0.9f;
			this.gamma2 = 0.6f;
			this.lag_factor = 0.01f;
			this.lpc_floor = 1.0001f;
			this.frmBuf = new float[bufSize];
			this.frmIdx = bufSize - this.windowSize;
			this.excBuf = new float[bufSize];
			this.excIdx = bufSize - this.windowSize;
			this.innov = new float[frameSize];
			this.lpc = new float[lpcSize + 1];
			this.qlsp = new float[lpcSize];
			this.old_qlsp = new float[lpcSize];
			this.interp_qlsp = new float[lpcSize];
			this.interp_qlpc = new float[lpcSize + 1];
			this.mem_sp = new float[5 * lpcSize];
			this.pi_gain = new float[this.nbSubframes];
			this.awk1 = new float[lpcSize + 1];
			this.awk2 = new float[lpcSize + 1];
			this.awk3 = new float[lpcSize + 1];
			this.voc_m1 = (this.voc_m2 = (this.voc_mean = 0f));
			this.voc_offset = 0;
			this.dtx_enabled = 0;
		}

		private static SubMode[] BuildNbSubModes()
		{
			Ltp3Tap ltp = new Ltp3Tap(Codebook_Constants.gain_cdbk_nb, 7, 7);
			Ltp3Tap ltp2 = new Ltp3Tap(Codebook_Constants.gain_cdbk_lbr, 5, 0);
			Ltp3Tap ltp3 = new Ltp3Tap(Codebook_Constants.gain_cdbk_lbr, 5, 7);
			Ltp3Tap ltp4 = new Ltp3Tap(Codebook_Constants.gain_cdbk_lbr, 5, 7);
			LtpForcedPitch ltp5 = new LtpForcedPitch();
			NoiseSearch innovation = new NoiseSearch();
			SplitShapeSearch innovation2 = new SplitShapeSearch(40, 10, 4, Codebook_Constants.exc_10_16_table, 4, 0);
			SplitShapeSearch innovation3 = new SplitShapeSearch(40, 10, 4, Codebook_Constants.exc_10_32_table, 5, 0);
			SplitShapeSearch innovation4 = new SplitShapeSearch(40, 5, 8, Codebook_Constants.exc_5_64_table, 6, 0);
			SplitShapeSearch innovation5 = new SplitShapeSearch(40, 8, 5, Codebook_Constants.exc_8_128_table, 7, 0);
			SplitShapeSearch innovation6 = new SplitShapeSearch(40, 5, 8, Codebook_Constants.exc_5_256_table, 8, 0);
			SplitShapeSearch innovation7 = new SplitShapeSearch(40, 20, 2, Codebook_Constants.exc_20_32_table, 5, 0);
			NbLspQuant lspQuant = new NbLspQuant();
			LbrLspQuant lspQuant2 = new LbrLspQuant();
			SubMode[] array = new SubMode[16];
			array[1] = new SubMode(0, 1, 0, 0, lspQuant2, ltp5, innovation, 0.7f, 0.7f, -1f, 43);
			array[2] = new SubMode(0, 0, 0, 0, lspQuant2, ltp2, innovation2, 0.7f, 0.5f, 0.55f, 119);
			array[3] = new SubMode(-1, 0, 1, 0, lspQuant2, ltp3, innovation3, 0.7f, 0.55f, 0.45f, 160);
			array[4] = new SubMode(-1, 0, 1, 0, lspQuant2, ltp4, innovation5, 0.7f, 0.63f, 0.35f, 220);
			array[5] = new SubMode(-1, 0, 3, 0, lspQuant, ltp, innovation4, 0.7f, 0.65f, 0.25f, 300);
			array[6] = new SubMode(-1, 0, 3, 0, lspQuant, ltp, innovation6, 0.68f, 0.65f, 0.1f, 364);
			array[7] = new SubMode(-1, 0, 3, 1, lspQuant, ltp, innovation4, 0.65f, 0.65f, -1f, 492);
			array[8] = new SubMode(0, 1, 0, 0, lspQuant2, ltp5, innovation7, 0.7f, 0.5f, 0.65f, 79);
			return array;
		}
	}
}
