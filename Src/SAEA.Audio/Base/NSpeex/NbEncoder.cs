using System;

namespace SAEA.Audio.Base.NSpeex
{
	internal class NbEncoder : NbCodec, IEncoder
	{
		public static readonly int[] NB_QUALITY_MAP = new int[]
		{
			1,
			8,
			2,
			3,
			3,
			4,
			4,
			5,
			5,
			6,
			7
		};

		private int bounded_pitch;

		private int[] pitch;

		private float pre_mem2;

		private float[] exc2Buf;

		private int exc2Idx;

		private float[] swBuf;

		private int swIdx;

		private float[] window;

		private float[] buf2;

		private float[] autocorr;

		private float[] lagWindow;

		private float[] lsp;

		private float[] old_lsp;

		private float[] interp_lsp;

		private float[] interp_lpc;

		private float[] bw_lpc1;

		private float[] bw_lpc2;

		private float[] rc;

		private float[] mem_sw;

		private float[] mem_sw_whole;

		private float[] mem_exc;

		private Vbr vbr;

		private int dtx_count;

		private float[] innov2;

		protected internal int complexity;

		protected internal int vbr_enabled;

		protected internal int vad_enabled;

		protected internal int abr_enabled;

		protected internal float vbr_quality;

		protected internal float relative_quality;

		protected internal float abr_drift;

		protected internal float abr_drift2;

		protected internal float abr_count;

		protected internal int sampling_rate;

		protected internal int submodeSelect;

		public virtual int EncodedFrameSize
		{
			get
			{
				return NbCodec.NB_FRAME_SIZE[this.submodeID];
			}
		}

		public virtual int Quality
		{
			set
			{
				if (value < 0)
				{
					value = 0;
				}
				if (value > 10)
				{
					value = 10;
				}
				this.submodeID = (this.submodeSelect = NbEncoder.NB_QUALITY_MAP[value]);
			}
		}

		public virtual int BitRate
		{
			get
			{
				if (this.submodes[this.submodeID] != null)
				{
					return this.sampling_rate * this.submodes[this.submodeID].BitsPerFrame / this.frameSize;
				}
				return this.sampling_rate * 5 / this.frameSize;
			}
			set
			{
				for (int i = 10; i >= 0; i--)
				{
					this.Quality = i;
					if (this.BitRate <= value)
					{
						return;
					}
				}
			}
		}

		public virtual int Mode
		{
			get
			{
				return this.submodeID;
			}
			set
			{
				if (value < 0)
				{
					value = 0;
				}
				this.submodeID = (this.submodeSelect = value);
			}
		}

		public virtual bool Vbr
		{
			get
			{
				return this.vbr_enabled != 0;
			}
			set
			{
				this.vbr_enabled = (value ? 1 : 0);
			}
		}

		public virtual bool Vad
		{
			get
			{
				return this.vad_enabled != 0;
			}
			set
			{
				this.vad_enabled = (value ? 1 : 0);
			}
		}

		public virtual bool Dtx
		{
			get
			{
				return this.dtx_enabled == 1;
			}
			set
			{
				this.dtx_enabled = (value ? 1 : 0);
			}
		}

		public virtual int Abr
		{
			get
			{
				return this.abr_enabled;
			}
			set
			{
				this.abr_enabled = ((value != 0) ? 1 : 0);
				this.vbr_enabled = 1;
				int i;
				for (i = 10; i >= 0; i--)
				{
					this.Quality = i;
					int bitRate = this.BitRate;
					if (bitRate <= value)
					{
						break;
					}
				}
				float num = (float)i;
				if (num < 0f)
				{
					num = 0f;
				}
				this.VbrQuality = num;
				this.abr_count = 0f;
				this.abr_drift = 0f;
				this.abr_drift2 = 0f;
			}
		}

		public virtual float VbrQuality
		{
			get
			{
				return this.vbr_quality;
			}
			set
			{
				if (value < 0f)
				{
					value = 0f;
				}
				if (value > 10f)
				{
					value = 10f;
				}
				this.vbr_quality = value;
			}
		}

		public virtual int Complexity
		{
			get
			{
				return this.complexity;
			}
			set
			{
				if (value < 0)
				{
					value = 0;
				}
				if (value > 10)
				{
					value = 10;
				}
				this.complexity = value;
			}
		}

		public virtual int SamplingRate
		{
			get
			{
				return this.sampling_rate;
			}
			set
			{
				this.sampling_rate = value;
			}
		}

		public virtual int LookAhead
		{
			get
			{
				return this.windowSize - this.frameSize;
			}
		}

		public virtual float RelativeQuality
		{
			get
			{
				return this.relative_quality;
			}
		}

		protected override void Init(int frameSize, int subframeSize, int lpcSize, int bufSize)
		{
			base.Init(frameSize, subframeSize, lpcSize, bufSize);
			this.complexity = 3;
			this.vbr_enabled = 0;
			this.vad_enabled = 0;
			this.abr_enabled = 0;
			this.vbr_quality = 8f;
			this.submodeSelect = 5;
			this.pre_mem2 = 0f;
			this.bounded_pitch = 1;
			this.exc2Buf = new float[bufSize];
			this.exc2Idx = bufSize - this.windowSize;
			this.swBuf = new float[bufSize];
			this.swIdx = bufSize - this.windowSize;
			this.window = Misc.Window(this.windowSize, subframeSize);
			this.lagWindow = Misc.LagWindow(lpcSize, this.lag_factor);
			this.autocorr = new float[lpcSize + 1];
			this.buf2 = new float[this.windowSize];
			this.interp_lpc = new float[lpcSize + 1];
			this.interp_qlpc = new float[lpcSize + 1];
			this.bw_lpc1 = new float[lpcSize + 1];
			this.bw_lpc2 = new float[lpcSize + 1];
			this.lsp = new float[lpcSize];
			this.qlsp = new float[lpcSize];
			this.old_lsp = new float[lpcSize];
			this.old_qlsp = new float[lpcSize];
			this.interp_lsp = new float[lpcSize];
			this.interp_qlsp = new float[lpcSize];
			this.rc = new float[lpcSize];
			this.mem_sp = new float[lpcSize];
			this.mem_sw = new float[lpcSize];
			this.mem_sw_whole = new float[lpcSize];
			this.mem_exc = new float[lpcSize];
			this.vbr = new Vbr();
			this.dtx_count = 0;
			this.abr_count = 0f;
			this.sampling_rate = 8000;
			this.awk1 = new float[lpcSize + 1];
			this.awk2 = new float[lpcSize + 1];
			this.awk3 = new float[lpcSize + 1];
			this.innov2 = new float[40];
			this.pitch = new int[this.nbSubframes];
		}

		public virtual int Encode(Bits bits, float[] ins0)
		{
			Array.Copy(this.frmBuf, this.frameSize, this.frmBuf, 0, this.bufSize - this.frameSize);
			this.frmBuf[this.bufSize - this.frameSize] = ins0[0] - this.preemph * this.pre_mem;
			for (int i = 1; i < this.frameSize; i++)
			{
				this.frmBuf[this.bufSize - this.frameSize + i] = ins0[i] - this.preemph * ins0[i - 1];
			}
			this.pre_mem = ins0[this.frameSize - 1];
			Array.Copy(this.exc2Buf, this.frameSize, this.exc2Buf, 0, this.bufSize - this.frameSize);
			Array.Copy(this.excBuf, this.frameSize, this.excBuf, 0, this.bufSize - this.frameSize);
			Array.Copy(this.swBuf, this.frameSize, this.swBuf, 0, this.bufSize - this.frameSize);
			for (int i = 0; i < this.windowSize; i++)
			{
				this.buf2[i] = this.frmBuf[i + this.frmIdx] * this.window[i];
			}
			Lpc.Autocorr(this.buf2, this.autocorr, this.lpcSize + 1, this.windowSize);
			this.autocorr[0] += 10f;
			this.autocorr[0] *= this.lpc_floor;
			for (int i = 0; i < this.lpcSize + 1; i++)
			{
				this.autocorr[i] *= this.lagWindow[i];
			}
			Lpc.Wld(this.lpc, this.autocorr, this.rc, this.lpcSize);
			Array.Copy(this.lpc, 0, this.lpc, 1, this.lpcSize);
			this.lpc[0] = 1f;
			int num = Lsp.Lpc2lsp(this.lpc, this.lpcSize, this.lsp, 15, 0.2f);
			if (num == this.lpcSize)
			{
				for (int i = 0; i < this.lpcSize; i++)
				{
					this.lsp[i] = (float)Math.Acos((double)this.lsp[i]);
				}
			}
			else
			{
				if (this.complexity > 1)
				{
					num = Lsp.Lpc2lsp(this.lpc, this.lpcSize, this.lsp, 11, 0.05f);
				}
				if (num == this.lpcSize)
				{
					for (int i = 0; i < this.lpcSize; i++)
					{
						this.lsp[i] = (float)Math.Acos((double)this.lsp[i]);
					}
				}
				else
				{
					for (int i = 0; i < this.lpcSize; i++)
					{
						this.lsp[i] = this.old_lsp[i];
					}
				}
			}
			float num2 = 0f;
			for (int i = 0; i < this.lpcSize; i++)
			{
				num2 += (this.old_lsp[i] - this.lsp[i]) * (this.old_lsp[i] - this.lsp[i]);
			}
			if (this.first != 0)
			{
				for (int i = 0; i < this.lpcSize; i++)
				{
					this.interp_lsp[i] = this.lsp[i];
				}
			}
			else
			{
				for (int i = 0; i < this.lpcSize; i++)
				{
					this.interp_lsp[i] = 0.375f * this.old_lsp[i] + 0.625f * this.lsp[i];
				}
			}
			Lsp.Enforce_margin(this.interp_lsp, this.lpcSize, 0.002f);
			for (int i = 0; i < this.lpcSize; i++)
			{
				this.interp_lsp[i] = (float)Math.Cos((double)this.interp_lsp[i]);
			}
			this.m_lsp.Lsp2lpc(this.interp_lsp, this.interp_lpc, this.lpcSize);
			int num3;
			float num4;
			if (this.submodes[this.submodeID] == null || this.vbr_enabled != 0 || this.vad_enabled != 0 || this.submodes[this.submodeID].ForcedPitchGain != 0 || this.submodes[this.submodeID].LbrPitch != -1)
			{
				int[] array = new int[6];
				float[] array2 = new float[6];
				Filters.Bw_lpc(this.gamma1, this.interp_lpc, this.bw_lpc1, this.lpcSize);
				Filters.Bw_lpc(this.gamma2, this.interp_lpc, this.bw_lpc2, this.lpcSize);
				Filters.Filter_mem2(this.frmBuf, this.frmIdx, this.bw_lpc1, this.bw_lpc2, this.swBuf, this.swIdx, this.frameSize, this.lpcSize, this.mem_sw_whole, 0);
				Ltp.Open_loop_nbest_pitch(this.swBuf, this.swIdx, this.min_pitch, this.max_pitch, this.frameSize, array, array2, 6);
				num3 = array[0];
				num4 = array2[0];
				for (int i = 1; i < 6; i++)
				{
					if ((double)array2[i] > 0.85 * (double)num4 && (Math.Abs((double)array[i] - (double)num3 / 2.0) <= 1.0 || Math.Abs((double)array[i] - (double)num3 / 3.0) <= 1.0 || Math.Abs((double)array[i] - (double)num3 / 4.0) <= 1.0 || Math.Abs((double)array[i] - (double)num3 / 5.0) <= 1.0))
					{
						num3 = array[i];
					}
				}
			}
			else
			{
				num3 = 0;
				num4 = 0f;
			}
			Filters.Fir_mem2(this.frmBuf, this.frmIdx, this.interp_lpc, this.excBuf, this.excIdx, this.frameSize, this.lpcSize, this.mem_exc);
			float num5 = 0f;
			for (int i = 0; i < this.frameSize; i++)
			{
				num5 += this.excBuf[this.excIdx + i] * this.excBuf[this.excIdx + i];
			}
			num5 = (float)Math.Sqrt((double)(1f + num5 / (float)this.frameSize));
			if (this.vbr != null && (this.vbr_enabled != 0 || this.vad_enabled != 0))
			{
				if (this.abr_enabled != 0)
				{
					float num6 = 0f;
					if (this.abr_drift2 * this.abr_drift > 0f)
					{
						num6 = -1E-05f * this.abr_drift / (1f + this.abr_count);
						if (num6 > 0.05f)
						{
							num6 = 0.05f;
						}
						if (num6 < -0.05f)
						{
							num6 = -0.05f;
						}
					}
					this.vbr_quality += num6;
					if (this.vbr_quality > 10f)
					{
						this.vbr_quality = 10f;
					}
					if (this.vbr_quality < 0f)
					{
						this.vbr_quality = 0f;
					}
				}
				this.relative_quality = this.vbr.Analysis(ins0, this.frameSize, num3, num4);
				if (this.vbr_enabled != 0)
				{
					int num7 = 0;
					float num8 = 100f;
					int j;
					for (j = 8; j > 0; j--)
					{
						int num9 = (int)Math.Floor((double)this.vbr_quality);
						float num10;
						if (num9 == 10)
						{
							num10 = NSpeex.Vbr.nb_thresh[j][num9];
						}
						else
						{
							num10 = (this.vbr_quality - (float)num9) * NSpeex.Vbr.nb_thresh[j][num9 + 1] + ((float)(1 + num9) - this.vbr_quality) * NSpeex.Vbr.nb_thresh[j][num9];
						}
						if (this.relative_quality > num10 && this.relative_quality - num10 < num8)
						{
							num7 = j;
							num8 = this.relative_quality - num10;
						}
					}
					j = num7;
					if (j == 0)
					{
						if (this.dtx_count == 0 || (double)num2 > 0.05 || this.dtx_enabled == 0 || this.dtx_count > 20)
						{
							j = 1;
							this.dtx_count = 1;
						}
						else
						{
							j = 0;
							this.dtx_count++;
						}
					}
					else
					{
						this.dtx_count = 0;
					}
					this.Mode = j;
					if (this.abr_enabled != 0)
					{
						int bitRate = this.BitRate;
						this.abr_drift += (float)(bitRate - this.abr_enabled);
						this.abr_drift2 = 0.95f * this.abr_drift2 + 0.05f * (float)(bitRate - this.abr_enabled);
						this.abr_count += new float?(1f).Value;
					}
				}
				else
				{
					int submodeID;
					if (this.relative_quality < 2f)
					{
						if (this.dtx_count == 0 || (double)num2 > 0.05 || this.dtx_enabled == 0 || this.dtx_count > 20)
						{
							this.dtx_count = 1;
							submodeID = 1;
						}
						else
						{
							submodeID = 0;
							this.dtx_count++;
						}
					}
					else
					{
						this.dtx_count = 0;
						submodeID = this.submodeSelect;
					}
					this.submodeID = submodeID;
				}
			}
			else
			{
				this.relative_quality = -1f;
			}
			bits.Pack(0, 1);
			bits.Pack(this.submodeID, 4);
			if (this.submodes[this.submodeID] == null)
			{
				for (int i = 0; i < this.frameSize; i++)
				{
					this.excBuf[this.excIdx + i] = (this.exc2Buf[this.exc2Idx + i] = (this.swBuf[this.swIdx + i] = 0f));
				}
				for (int i = 0; i < this.lpcSize; i++)
				{
					this.mem_sw[i] = 0f;
				}
				this.first = 1;
				this.bounded_pitch = 1;
				Filters.Iir_mem2(this.excBuf, this.excIdx, this.interp_qlpc, this.frmBuf, this.frmIdx, this.frameSize, this.lpcSize, this.mem_sp);
				ins0[0] = this.frmBuf[this.frmIdx] + this.preemph * this.pre_mem2;
				for (int i = 1; i < this.frameSize; i++)
				{
					ins0[i] = this.frmBuf[this.frmIdx = i] + this.preemph * ins0[i - 1];
				}
				this.pre_mem2 = ins0[this.frameSize - 1];
				return 0;
			}
			if (this.first != 0)
			{
				for (int i = 0; i < this.lpcSize; i++)
				{
					this.old_lsp[i] = this.lsp[i];
				}
			}
			this.submodes[this.submodeID].LsqQuant.Quant(this.lsp, this.qlsp, this.lpcSize, bits);
			if (this.submodes[this.submodeID].LbrPitch != -1)
			{
				bits.Pack(num3 - this.min_pitch, 7);
			}
			if (this.submodes[this.submodeID].ForcedPitchGain != 0)
			{
				int num11 = (int)Math.Floor(0.5 + (double)(15f * num4));
				if (num11 > 15)
				{
					num11 = 15;
				}
				if (num11 < 0)
				{
					num11 = 0;
				}
				bits.Pack(num11, 4);
				num4 = 0.066667f * (float)num11;
			}
			int num12 = (int)Math.Floor(0.5 + 3.5 * Math.Log((double)num5));
			if (num12 < 0)
			{
				num12 = 0;
			}
			if (num12 > 31)
			{
				num12 = 31;
			}
			num5 = (float)Math.Exp((double)num12 / 3.5);
			bits.Pack(num12, 5);
			if (this.first != 0)
			{
				for (int i = 0; i < this.lpcSize; i++)
				{
					this.old_qlsp[i] = this.qlsp[i];
				}
			}
			float[] array3 = new float[this.subframeSize];
			float[] array4 = new float[this.subframeSize];
			float[] array5 = new float[this.subframeSize];
			float[] array6 = new float[this.lpcSize];
			float[] array7 = new float[this.frameSize];
			for (int i = 0; i < this.frameSize; i++)
			{
				array7[i] = this.frmBuf[this.frmIdx + i];
			}
			for (int k = 0; k < this.nbSubframes; k++)
			{
				int num13 = this.subframeSize * k;
				int num14 = this.frmIdx + num13;
				int num15 = this.excIdx + num13;
				int num16 = this.swIdx + num13;
				int num17 = this.exc2Idx + num13;
				float num18 = (float)(1.0 + (double)k) / (float)this.nbSubframes;
				for (int i = 0; i < this.lpcSize; i++)
				{
					this.interp_lsp[i] = (1f - num18) * this.old_lsp[i] + num18 * this.lsp[i];
				}
				for (int i = 0; i < this.lpcSize; i++)
				{
					this.interp_qlsp[i] = (1f - num18) * this.old_qlsp[i] + num18 * this.qlsp[i];
				}
				Lsp.Enforce_margin(this.interp_lsp, this.lpcSize, 0.002f);
				Lsp.Enforce_margin(this.interp_qlsp, this.lpcSize, 0.002f);
				for (int i = 0; i < this.lpcSize; i++)
				{
					this.interp_lsp[i] = (float)Math.Cos((double)this.interp_lsp[i]);
				}
				this.m_lsp.Lsp2lpc(this.interp_lsp, this.interp_lpc, this.lpcSize);
				for (int i = 0; i < this.lpcSize; i++)
				{
					this.interp_qlsp[i] = (float)Math.Cos((double)this.interp_qlsp[i]);
				}
				this.m_lsp.Lsp2lpc(this.interp_qlsp, this.interp_qlpc, this.lpcSize);
				num18 = 1f;
				this.pi_gain[k] = 0f;
				for (int i = 0; i <= this.lpcSize; i++)
				{
					this.pi_gain[k] += num18 * this.interp_qlpc[i];
					num18 = -num18;
				}
				Filters.Bw_lpc(this.gamma1, this.interp_lpc, this.bw_lpc1, this.lpcSize);
				if (this.gamma2 >= 0f)
				{
					Filters.Bw_lpc(this.gamma2, this.interp_lpc, this.bw_lpc2, this.lpcSize);
				}
				else
				{
					this.bw_lpc2[0] = 1f;
					this.bw_lpc2[1] = -this.preemph;
					for (int i = 2; i <= this.lpcSize; i++)
					{
						this.bw_lpc2[i] = 0f;
					}
				}
				for (int i = 0; i < this.subframeSize; i++)
				{
					this.excBuf[num15 + i] = 0f;
				}
				this.excBuf[num15] = 1f;
				Filters.Syn_percep_zero(this.excBuf, num15, this.interp_qlpc, this.bw_lpc1, this.bw_lpc2, array5, this.subframeSize, this.lpcSize);
				for (int i = 0; i < this.subframeSize; i++)
				{
					this.excBuf[num15 + i] = 0f;
				}
				for (int i = 0; i < this.subframeSize; i++)
				{
					this.exc2Buf[num17 + i] = 0f;
				}
				for (int i = 0; i < this.lpcSize; i++)
				{
					array6[i] = this.mem_sp[i];
				}
				Filters.Iir_mem2(this.excBuf, num15, this.interp_qlpc, this.excBuf, num15, this.subframeSize, this.lpcSize, array6);
				for (int i = 0; i < this.lpcSize; i++)
				{
					array6[i] = this.mem_sw[i];
				}
				Filters.Filter_mem2(this.excBuf, num15, this.bw_lpc1, this.bw_lpc2, array3, 0, this.subframeSize, this.lpcSize, array6, 0);
				for (int i = 0; i < this.lpcSize; i++)
				{
					array6[i] = this.mem_sw[i];
				}
				Filters.Filter_mem2(this.frmBuf, num14, this.bw_lpc1, this.bw_lpc2, this.swBuf, num16, this.subframeSize, this.lpcSize, array6, 0);
				for (int i = 0; i < this.subframeSize; i++)
				{
					array4[i] = this.swBuf[num16 + i] - array3[i];
				}
				for (int i = 0; i < this.subframeSize; i++)
				{
					this.excBuf[num15 + i] = (this.exc2Buf[num17 + i] = 0f);
				}
				int start;
				int num19;
				if (this.submodes[this.submodeID].LbrPitch != -1)
				{
					int lbrPitch = this.submodes[this.submodeID].LbrPitch;
					if (lbrPitch != 0)
					{
						if (num3 < this.min_pitch + lbrPitch - 1)
						{
							num3 = this.min_pitch + lbrPitch - 1;
						}
						if (num3 > this.max_pitch - lbrPitch)
						{
							num3 = this.max_pitch - lbrPitch;
						}
						start = num3 - lbrPitch + 1;
						num19 = num3 + lbrPitch;
					}
					else
					{
						num19 = (start = num3);
					}
				}
				else
				{
					start = this.min_pitch;
					num19 = this.max_pitch;
				}
				if (this.bounded_pitch != 0 && num19 > num13)
				{
					num19 = num13;
				}
				int num20 = this.submodes[this.submodeID].Ltp.Quant(array4, this.swBuf, num16, this.interp_qlpc, this.bw_lpc1, this.bw_lpc2, this.excBuf, num15, start, num19, num4, this.lpcSize, this.subframeSize, bits, this.exc2Buf, num17, array5, this.complexity);
				this.pitch[k] = num20;
				Filters.Syn_percep_zero(this.excBuf, num15, this.interp_qlpc, this.bw_lpc1, this.bw_lpc2, array3, this.subframeSize, this.lpcSize);
				for (int i = 0; i < this.subframeSize; i++)
				{
					array4[i] -= array3[i];
				}
				float num21 = 0f;
				int num22 = k * this.subframeSize;
				for (int i = 0; i < this.subframeSize; i++)
				{
					this.innov[num22 + i] = 0f;
				}
				Filters.Residue_percep_zero(array4, 0, this.interp_qlpc, this.bw_lpc1, this.bw_lpc2, this.buf2, this.subframeSize, this.lpcSize);
				for (int i = 0; i < this.subframeSize; i++)
				{
					num21 += this.buf2[i] * this.buf2[i];
				}
				num21 = (float)Math.Sqrt((double)(0.1f + num21 / (float)this.subframeSize));
				num21 /= num5;
				if (this.submodes[this.submodeID].HaveSubframeGain != 0)
				{
					num21 = (float)Math.Log((double)num21);
					if (this.submodes[this.submodeID].HaveSubframeGain == 3)
					{
						int num23 = VQ.Index(num21, NbCodec.exc_gain_quant_scal3, 8);
						bits.Pack(num23, 3);
						num21 = NbCodec.exc_gain_quant_scal3[num23];
					}
					else
					{
						int num23 = VQ.Index(num21, NbCodec.exc_gain_quant_scal1, 2);
						bits.Pack(num23, 1);
						num21 = NbCodec.exc_gain_quant_scal1[num23];
					}
					num21 = (float)Math.Exp((double)num21);
				}
				else
				{
					num21 = 1f;
				}
				num21 *= num5;
				float num24 = 1f / num21;
				for (int i = 0; i < this.subframeSize; i++)
				{
					array4[i] *= num24;
				}
				this.submodes[this.submodeID].Innovation.Quantify(array4, this.interp_qlpc, this.bw_lpc1, this.bw_lpc2, this.lpcSize, this.subframeSize, this.innov, num22, array5, bits, this.complexity);
				for (int i = 0; i < this.subframeSize; i++)
				{
					this.innov[num22 + i] *= num21;
				}
				for (int i = 0; i < this.subframeSize; i++)
				{
					this.excBuf[num15 + i] += this.innov[num22 + i];
				}
				if (this.submodes[this.submodeID].DoubleCodebook != 0)
				{
					float[] array8 = new float[this.subframeSize];
					for (int i = 0; i < this.subframeSize; i++)
					{
						array4[i] *= 2.2f;
					}
					this.submodes[this.submodeID].Innovation.Quantify(array4, this.interp_qlpc, this.bw_lpc1, this.bw_lpc2, this.lpcSize, this.subframeSize, array8, 0, array5, bits, this.complexity);
					for (int i = 0; i < this.subframeSize; i++)
					{
						array8[i] *= (float)((double)num21 * 0.45454545454545453);
					}
					for (int i = 0; i < this.subframeSize; i++)
					{
						this.excBuf[num15 + i] += array8[i];
					}
				}
				for (int i = 0; i < this.subframeSize; i++)
				{
					array4[i] *= num21;
				}
				for (int i = 0; i < this.lpcSize; i++)
				{
					array6[i] = this.mem_sp[i];
				}
				Filters.Iir_mem2(this.excBuf, num15, this.interp_qlpc, this.frmBuf, num14, this.subframeSize, this.lpcSize, this.mem_sp);
				Filters.Filter_mem2(this.frmBuf, num14, this.bw_lpc1, this.bw_lpc2, this.swBuf, num16, this.subframeSize, this.lpcSize, this.mem_sw, 0);
				for (int i = 0; i < this.subframeSize; i++)
				{
					this.exc2Buf[num17 + i] = this.excBuf[num15 + i];
				}
			}
			if (this.submodeID >= 1)
			{
				for (int i = 0; i < this.lpcSize; i++)
				{
					this.old_lsp[i] = this.lsp[i];
				}
				for (int i = 0; i < this.lpcSize; i++)
				{
					this.old_qlsp[i] = this.qlsp[i];
				}
			}
			if (this.submodeID == 1)
			{
				if (this.dtx_count != 0)
				{
					bits.Pack(15, 4);
				}
				else
				{
					bits.Pack(0, 4);
				}
			}
			this.first = 0;
			float num25 = 0f;
			float num26 = 0f;
			for (int i = 0; i < this.frameSize; i++)
			{
				num25 += this.frmBuf[this.frmIdx + i] * this.frmBuf[this.frmIdx + i];
				num26 += (this.frmBuf[this.frmIdx + i] - array7[i]) * (this.frmBuf[this.frmIdx + i] - array7[i]);
			}
			Math.Log((double)((num25 + 1f) / (num26 + 1f)));
			ins0[0] = this.frmBuf[this.frmIdx] + this.preemph * this.pre_mem2;
			for (int i = 1; i < this.frameSize; i++)
			{
				ins0[i] = this.frmBuf[this.frmIdx + i] + this.preemph * ins0[i - 1];
			}
			this.pre_mem2 = ins0[this.frameSize - 1];
			if (this.submodes[this.submodeID].Innovation is NoiseSearch || this.submodeID == 0)
			{
				this.bounded_pitch = 1;
			}
			else
			{
				this.bounded_pitch = 0;
			}
			return 1;
		}
	}
}
