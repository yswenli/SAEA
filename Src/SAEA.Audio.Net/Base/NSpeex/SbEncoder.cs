using System;

namespace SAEA.Audio.NSpeex
{
	internal class SbEncoder : SbCodec, IEncoder
	{
		private static readonly int[] NB_QUALITY_MAP = new int[]
		{
			1,
			8,
			2,
			3,
			4,
			5,
			5,
			6,
			6,
			7,
			7
		};

		private static readonly int[] WB_QUALITY_MAP = new int[]
		{
			1,
			1,
			1,
			1,
			1,
			1,
			2,
			2,
			3,
			3,
			4
		};

		private static readonly int[] UWB_QUALITY_MAP = new int[]
		{
			0,
			1,
			1,
			1,
			1,
			1,
			1,
			1,
			1,
			1,
			1
		};

		protected internal IEncoder lowenc;

		private float[] x1d;

		private float[] h0_mem;

		private float[] buf;

		private float[] swBuf;

		private float[] res;

		private float[] target;

		private float[] window;

		private float[] lagWindow;

		private float[] rc;

		private float[] autocorr;

		private float[] lsp;

		private float[] old_lsp;

		private float[] interp_lsp;

		private float[] interp_lpc;

		private float[] bw_lpc1;

		private float[] bw_lpc2;

		private float[] mem_sp2;

		private float[] mem_sw;

		protected internal int nb_modes;

		private bool uwb;

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
				int num = SbCodec.SB_FRAME_SIZE[this.submodeID];
				return num + this.lowenc.EncodedFrameSize;
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
				if (this.uwb)
				{
					this.lowenc.Quality = value;
					this.Mode = SbEncoder.UWB_QUALITY_MAP[value];
					return;
				}
				this.lowenc.Mode = SbEncoder.NB_QUALITY_MAP[value];
				this.Mode = SbEncoder.WB_QUALITY_MAP[value];
			}
		}

		public virtual int BitRate
		{
			get
			{
				if (this.submodes[this.submodeID] != null)
				{
					return this.lowenc.BitRate + this.sampling_rate * this.submodes[this.submodeID].BitsPerFrame / this.frameSize;
				}
				return this.lowenc.BitRate + this.sampling_rate * 4 / this.frameSize;
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

		public virtual int LookAhead
		{
			get
			{
				return 2 * this.lowenc.LookAhead + 64 - 1;
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
				this.lowenc.Vbr = value;
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

		public new bool Dtx
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
				this.lowenc.Vbr = true;
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
				this.vbr_quality = value;
				float num = value + 0.6f;
				if (num > 10f)
				{
					num = 10f;
				}
				this.lowenc.VbrQuality = num;
				int num2 = (int)Math.Floor(0.5 + (double)value);
				if (num2 > 10)
				{
					num2 = 10;
				}
				this.Quality = num2;
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
				this.lowenc.SamplingRate = value;
			}
		}

		public virtual float RelativeQuality
		{
			get
			{
				return this.relative_quality;
			}
		}

		public SbEncoder(bool ultraWide) : base(ultraWide)
		{
			if (ultraWide)
			{
				this.Uwbinit();
				return;
			}
			this.Wbinit();
		}

		private void Wbinit()
		{
			this.lowenc = new NbEncoder();
			this.Init(160, 40, 8, 640, 0.9f);
			this.uwb = false;
			this.nb_modes = 5;
			this.sampling_rate = 16000;
		}

		private void Uwbinit()
		{
			this.lowenc = new SbEncoder(false);
			this.Init(320, 80, 8, 1280, 0.7f);
			this.uwb = true;
			this.nb_modes = 2;
			this.sampling_rate = 32000;
		}

		protected override void Init(int frameSize, int subframeSize, int lpcSize, int bufSize, float foldingGain)
		{
			base.Init(frameSize, subframeSize, lpcSize, bufSize, foldingGain);
			this.complexity = 3;
			this.vbr_enabled = 0;
			this.vad_enabled = 0;
			this.abr_enabled = 0;
			this.vbr_quality = 8f;
			this.submodeSelect = this.submodeID;
			this.x1d = new float[frameSize];
			this.h0_mem = new float[64];
			this.buf = new float[this.windowSize];
			this.swBuf = new float[frameSize];
			this.res = new float[frameSize];
			this.target = new float[subframeSize];
			this.window = Misc.Window(this.windowSize, subframeSize);
			this.lagWindow = Misc.LagWindow(lpcSize, this.lag_factor);
			this.rc = new float[lpcSize];
			this.autocorr = new float[lpcSize + 1];
			this.lsp = new float[lpcSize];
			this.old_lsp = new float[lpcSize];
			this.interp_lsp = new float[lpcSize];
			this.interp_lpc = new float[lpcSize + 1];
			this.bw_lpc1 = new float[lpcSize + 1];
			this.bw_lpc2 = new float[lpcSize + 1];
			this.mem_sp2 = new float[lpcSize];
			this.mem_sw = new float[lpcSize];
			this.abr_count = 0f;
		}

		public virtual int Encode(Bits bits, float[] ins0)
		{
			Filters.Qmf_decomp(ins0, Codebook_Constants.h0, this.x0d, this.x1d, this.fullFrameSize, 64, this.h0_mem);
			this.lowenc.Encode(bits, this.x0d);
			for (int i = 0; i < this.windowSize - this.frameSize; i++)
			{
				this.high[i] = this.high[this.frameSize + i];
			}
			for (int i = 0; i < this.frameSize; i++)
			{
				this.high[this.windowSize - this.frameSize + i] = this.x1d[i];
			}
			Array.Copy(this.excBuf, this.frameSize, this.excBuf, 0, this.bufSize - this.frameSize);
			float[] piGain = this.lowenc.PiGain;
			float[] exc = this.lowenc.Exc;
			float[] innov = this.lowenc.Innov;
			int num;
			if (this.lowenc.Mode == 0)
			{
				num = 1;
			}
			else
			{
				num = 0;
			}
			for (int i = 0; i < this.windowSize; i++)
			{
				this.buf[i] = this.high[i] * this.window[i];
			}
			Lpc.Autocorr(this.buf, this.autocorr, this.lpcSize + 1, this.windowSize);
			this.autocorr[0] += 1f;
			this.autocorr[0] *= this.lpc_floor;
			for (int i = 0; i < this.lpcSize + 1; i++)
			{
				this.autocorr[i] *= this.lagWindow[i];
			}
			Lpc.Wld(this.lpc, this.autocorr, this.rc, this.lpcSize);
			Array.Copy(this.lpc, 0, this.lpc, 1, this.lpcSize);
			this.lpc[0] = 1f;
			int num2 = Lsp.Lpc2lsp(this.lpc, this.lpcSize, this.lsp, 15, 0.2f);
			if (num2 != this.lpcSize)
			{
				num2 = Lsp.Lpc2lsp(this.lpc, this.lpcSize, this.lsp, 11, 0.02f);
				if (num2 != this.lpcSize)
				{
					for (int i = 0; i < this.lpcSize; i++)
					{
						this.lsp[i] = (float)Math.Cos(3.1415926535897931 * (double)((float)(i + 1)) / (double)(this.lpcSize + 1));
					}
				}
			}
			for (int i = 0; i < this.lpcSize; i++)
			{
				this.lsp[i] = (float)Math.Acos((double)this.lsp[i]);
			}
			float num3 = 0f;
			for (int i = 0; i < this.lpcSize; i++)
			{
				num3 += (this.old_lsp[i] - this.lsp[i]) * (this.old_lsp[i] - this.lsp[i]);
			}
			if ((this.vbr_enabled != 0 || this.vad_enabled != 0) && num == 0)
			{
				float num4 = 0f;
				float num5 = 0f;
				if (this.abr_enabled != 0)
				{
					float num6 = 0f;
					if (this.abr_drift2 * this.abr_drift > 0f)
					{
						num6 = -1E-05f * this.abr_drift / (1f + this.abr_count);
						if (num6 > 0.1f)
						{
							num6 = 0.1f;
						}
						if (num6 < -0.1f)
						{
							num6 = -0.1f;
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
				for (int i = 0; i < this.frameSize; i++)
				{
					num4 += this.x0d[i] * this.x0d[i];
					num5 += this.high[i] * this.high[i];
				}
				float num7 = (float)Math.Log((double)((1f + num5) / (1f + num4)));
				this.relative_quality = this.lowenc.RelativeQuality;
				if (num7 < -4f)
				{
					num7 = -4f;
				}
				if (num7 > 2f)
				{
					num7 = 2f;
				}
				if (this.vbr_enabled != 0)
				{
					int num8 = this.nb_modes - 1;
					this.relative_quality += 1f * (num7 + 2f);
					if (this.relative_quality < -1f)
					{
						this.relative_quality = -1f;
					}
					while (num8 != 0)
					{
						int num9 = (int)Math.Floor((double)this.vbr_quality);
						float num10;
						if (num9 == 10)
						{
							num10 = NSpeex.Vbr.hb_thresh[num8][num9];
						}
						else
						{
							num10 = (this.vbr_quality - (float)num9) * NSpeex.Vbr.hb_thresh[num8][num9 + 1] + ((float)(1 + num9) - this.vbr_quality) * NSpeex.Vbr.hb_thresh[num8][num9];
						}
						if (this.relative_quality >= num10)
						{
							break;
						}
						num8--;
					}
					this.Mode = num8;
					if (this.abr_enabled != 0)
					{
						int bitRate = this.BitRate;
						this.abr_drift += (float)(bitRate - this.abr_enabled);
						this.abr_drift2 = 0.95f * this.abr_drift2 + 0.05f * (float)(bitRate - this.abr_enabled);
						this.abr_count += 1f;
					}
				}
				else
				{
					int submodeID;
					if ((double)this.relative_quality < 2.0)
					{
						submodeID = 1;
					}
					else
					{
						submodeID = this.submodeSelect;
					}
					this.submodeID = submodeID;
				}
			}
			bits.Pack(1, 1);
			if (num != 0)
			{
				bits.Pack(0, 3);
			}
			else
			{
				bits.Pack(this.submodeID, 3);
			}
			if (num == 0 && this.submodes[this.submodeID] != null)
			{
				this.submodes[this.submodeID].LsqQuant.Quant(this.lsp, this.qlsp, this.lpcSize, bits);
				if (this.first != 0)
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
				float[] array = new float[this.lpcSize];
				float[] array2 = new float[this.subframeSize];
				float[] array3 = new float[this.subframeSize];
				for (int j = 0; j < this.nbSubframes; j++)
				{
					float num11 = 0f;
					float num12 = 0f;
					int num13 = this.subframeSize * j;
					int num14 = num13;
					int num15 = this.excIdx + num13;
					int num16 = num13;
					int num17 = num13;
					float num18 = (1f + (float)j) / (float)this.nbSubframes;
					for (int i = 0; i < this.lpcSize; i++)
					{
						this.interp_lsp[i] = (1f - num18) * this.old_lsp[i] + num18 * this.lsp[i];
					}
					for (int i = 0; i < this.lpcSize; i++)
					{
						this.interp_qlsp[i] = (1f - num18) * this.old_qlsp[i] + num18 * this.qlsp[i];
					}
					Lsp.Enforce_margin(this.interp_lsp, this.lpcSize, 0.05f);
					Lsp.Enforce_margin(this.interp_qlsp, this.lpcSize, 0.05f);
					for (int i = 0; i < this.lpcSize; i++)
					{
						this.interp_lsp[i] = (float)Math.Cos((double)this.interp_lsp[i]);
					}
					for (int i = 0; i < this.lpcSize; i++)
					{
						this.interp_qlsp[i] = (float)Math.Cos((double)this.interp_qlsp[i]);
					}
					this.m_lsp.Lsp2lpc(this.interp_lsp, this.interp_lpc, this.lpcSize);
					this.m_lsp.Lsp2lpc(this.interp_qlsp, this.interp_qlpc, this.lpcSize);
					Filters.Bw_lpc(this.gamma1, this.interp_lpc, this.bw_lpc1, this.lpcSize);
					Filters.Bw_lpc(this.gamma2, this.interp_lpc, this.bw_lpc2, this.lpcSize);
					float num19 = 0f;
					num18 = 1f;
					this.pi_gain[j] = 0f;
					for (int i = 0; i <= this.lpcSize; i++)
					{
						num19 += num18 * this.interp_qlpc[i];
						num18 = -num18;
						this.pi_gain[j] += this.interp_qlpc[i];
					}
					float value = piGain[j];
					value = 1f / (Math.Abs(value) + 0.01f);
					num19 = 1f / (Math.Abs(num19) + 0.01f);
					float num20 = Math.Abs(0.01f + num19) / (0.01f + Math.Abs(value));
					Filters.Fir_mem2(this.high, num14, this.interp_qlpc, this.excBuf, num15, this.subframeSize, this.lpcSize, this.mem_sp2);
					for (int i = 0; i < this.subframeSize; i++)
					{
						num11 += this.excBuf[num15 + i] * this.excBuf[num15 + i];
					}
					if (this.submodes[this.submodeID].Innovation == null)
					{
						for (int i = 0; i < this.subframeSize; i++)
						{
							num12 += innov[num13 + i] * innov[num13 + i];
						}
						float num21 = num11 / (0.01f + num12);
						num21 = (float)Math.Sqrt((double)num21);
						num21 *= num20;
						int num22 = (int)Math.Floor(10.5 + 8.0 * Math.Log((double)num21 + 0.0001));
						if (num22 < 0)
						{
							num22 = 0;
						}
						if (num22 > 31)
						{
							num22 = 31;
						}
						bits.Pack(num22, 5);
						num21 = (float)(0.1 * Math.Exp((double)num22 / 9.4));
						num21 /= num20;
					}
					else
					{
						for (int i = 0; i < this.subframeSize; i++)
						{
							num12 += exc[num13 + i] * exc[num13 + i];
						}
						float num23 = (float)(Math.Sqrt((double)(1f + num11)) * (double)num20 / Math.Sqrt((double)((1f + num12) * (float)this.subframeSize)));
						int num24 = (int)Math.Floor(0.5 + 3.7 * (Math.Log((double)num23) + 2.0));
						if (num24 < 0)
						{
							num24 = 0;
						}
						if (num24 > 15)
						{
							num24 = 15;
						}
						bits.Pack(num24, 4);
						num23 = (float)Math.Exp(0.27027027027027023 * (double)num24 - 2.0);
						float num25 = num23 * (float)Math.Sqrt((double)(1f + num12)) / num20;
						float num26 = 1f / num25;
						for (int i = 0; i < this.subframeSize; i++)
						{
							this.excBuf[num15 + i] = 0f;
						}
						this.excBuf[num15] = 1f;
						Filters.Syn_percep_zero(this.excBuf, num15, this.interp_qlpc, this.bw_lpc1, this.bw_lpc2, array2, this.subframeSize, this.lpcSize);
						for (int i = 0; i < this.subframeSize; i++)
						{
							this.excBuf[num15 + i] = 0f;
						}
						for (int i = 0; i < this.lpcSize; i++)
						{
							array[i] = this.mem_sp[i];
						}
						Filters.Iir_mem2(this.excBuf, num15, this.interp_qlpc, this.excBuf, num15, this.subframeSize, this.lpcSize, array);
						for (int i = 0; i < this.lpcSize; i++)
						{
							array[i] = this.mem_sw[i];
						}
						Filters.Filter_mem2(this.excBuf, num15, this.bw_lpc1, this.bw_lpc2, this.res, num16, this.subframeSize, this.lpcSize, array, 0);
						for (int i = 0; i < this.lpcSize; i++)
						{
							array[i] = this.mem_sw[i];
						}
						Filters.Filter_mem2(this.high, num14, this.bw_lpc1, this.bw_lpc2, this.swBuf, num17, this.subframeSize, this.lpcSize, array, 0);
						for (int i = 0; i < this.subframeSize; i++)
						{
							this.target[i] = this.swBuf[num17 + i] - this.res[num16 + i];
						}
						for (int i = 0; i < this.subframeSize; i++)
						{
							this.excBuf[num15 + i] = 0f;
						}
						for (int i = 0; i < this.subframeSize; i++)
						{
							this.target[i] *= num26;
						}
						for (int i = 0; i < this.subframeSize; i++)
						{
							array3[i] = 0f;
						}
						this.submodes[this.submodeID].Innovation.Quantify(this.target, this.interp_qlpc, this.bw_lpc1, this.bw_lpc2, this.lpcSize, this.subframeSize, array3, 0, array2, bits, this.complexity + 1 >> 1);
						for (int i = 0; i < this.subframeSize; i++)
						{
							this.excBuf[num15 + i] += array3[i] * num25;
						}
						if (this.submodes[this.submodeID].DoubleCodebook != 0)
						{
							float[] array4 = new float[this.subframeSize];
							for (int i = 0; i < this.subframeSize; i++)
							{
								array4[i] = 0f;
							}
							for (int i = 0; i < this.subframeSize; i++)
							{
								this.target[i] *= 2.5f;
							}
							this.submodes[this.submodeID].Innovation.Quantify(this.target, this.interp_qlpc, this.bw_lpc1, this.bw_lpc2, this.lpcSize, this.subframeSize, array4, 0, array2, bits, this.complexity + 1 >> 1);
							for (int i = 0; i < this.subframeSize; i++)
							{
								array4[i] *= (float)((double)num25 * 0.4);
							}
							for (int i = 0; i < this.subframeSize; i++)
							{
								this.excBuf[num15 + i] += array4[i];
							}
						}
					}
					for (int i = 0; i < this.lpcSize; i++)
					{
						array[i] = this.mem_sp[i];
					}
					Filters.Iir_mem2(this.excBuf, num15, this.interp_qlpc, this.high, num14, this.subframeSize, this.lpcSize, this.mem_sp);
					Filters.Filter_mem2(this.high, num14, this.bw_lpc1, this.bw_lpc2, this.swBuf, num17, this.subframeSize, this.lpcSize, this.mem_sw, 0);
				}
				this.filters.Fir_mem_up(this.x0d, Codebook_Constants.h0, this.y0, this.fullFrameSize, 64, this.g0_mem);
				this.filters.Fir_mem_up(this.high, Codebook_Constants.h1, this.y1, this.fullFrameSize, 64, this.g1_mem);
				for (int i = 0; i < this.fullFrameSize; i++)
				{
					ins0[i] = 2f * (this.y0[i] - this.y1[i]);
				}
				for (int i = 0; i < this.lpcSize; i++)
				{
					this.old_lsp[i] = this.lsp[i];
				}
				for (int i = 0; i < this.lpcSize; i++)
				{
					this.old_qlsp[i] = this.qlsp[i];
				}
				this.first = 0;
				return 1;
			}
			for (int i = 0; i < this.frameSize; i++)
			{
				this.excBuf[this.excIdx + i] = (this.swBuf[i] = 0f);
			}
			for (int i = 0; i < this.lpcSize; i++)
			{
				this.mem_sw[i] = 0f;
			}
			this.first = 1;
			Filters.Iir_mem2(this.excBuf, this.excIdx, this.interp_qlpc, this.high, 0, this.subframeSize, this.lpcSize, this.mem_sp);
			this.filters.Fir_mem_up(this.x0d, Codebook_Constants.h0, this.y0, this.fullFrameSize, 64, this.g0_mem);
			this.filters.Fir_mem_up(this.high, Codebook_Constants.h1, this.y1, this.fullFrameSize, 64, this.g1_mem);
			for (int i = 0; i < this.fullFrameSize; i++)
			{
				ins0[i] = 2f * (this.y0[i] - this.y1[i]);
			}
			if (num != 0)
			{
				return 0;
			}
			return 1;
		}
	}
}
