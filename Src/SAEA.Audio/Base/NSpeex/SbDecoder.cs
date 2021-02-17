using System;

namespace SAEA.Audio.Base.NSpeex
{
	internal class SbDecoder : SbCodec, IDecoder
	{
		protected internal IDecoder lowdec;

		protected internal Stereo stereo;

		protected internal bool enhanced;

		private float[] innov2;

		public virtual bool PerceptualEnhancement
		{
			get
			{
				return this.enhanced;
			}
			set
			{
				this.enhanced = value;
			}
		}

		public SbDecoder(bool ultraWide) : base(ultraWide)
		{
			this.stereo = new Stereo();
			this.enhanced = true;
			if (ultraWide)
			{
				this.Uwbinit();
				return;
			}
			this.Wbinit();
		}

		private void Wbinit()
		{
			this.lowdec = new NbDecoder();
			this.lowdec.PerceptualEnhancement = this.enhanced;
			this.Init(160, 40, 8, 640, 0.7f);
		}

		private void Uwbinit()
		{
			this.lowdec = new SbDecoder(false);
			this.lowdec.PerceptualEnhancement = this.enhanced;
			this.Init(320, 80, 8, 1280, 0.5f);
		}

		protected override void Init(int frameSize, int subframeSize, int lpcSize, int bufSize, float foldingGain)
		{
			base.Init(frameSize, subframeSize, lpcSize, bufSize, foldingGain);
			this.excIdx = 0;
			this.innov2 = new float[subframeSize];
		}

		public virtual int Decode(Bits bits, float[] xout)
		{
			int num = this.lowdec.Decode(bits, this.x0d);
			if (num != 0)
			{
				return num;
			}
			bool dtx = this.lowdec.Dtx;
			if (bits == null)
			{
				this.DecodeLost(xout, dtx);
				return 0;
			}
			int num2 = bits.Peek();
			if (num2 != 0)
			{
				num2 = bits.Unpack(1);
				this.submodeID = bits.Unpack(3);
			}
			else
			{
				this.submodeID = 0;
			}
			for (int i = 0; i < this.frameSize; i++)
			{
				this.excBuf[i] = 0f;
			}
			if (this.submodes[this.submodeID] != null)
			{
				float[] piGain = this.lowdec.PiGain;
				float[] exc = this.lowdec.Exc;
				float[] innov = this.lowdec.Innov;
				this.submodes[this.submodeID].LsqQuant.Unquant(this.qlsp, this.lpcSize, bits);
				if (this.first != 0)
				{
					for (int i = 0; i < this.lpcSize; i++)
					{
						this.old_qlsp[i] = this.qlsp[i];
					}
				}
				for (int j = 0; j < this.nbSubframes; j++)
				{
					float num3 = 0f;
					float num4 = 0f;
					int num5 = this.subframeSize * j;
					float num6 = (1f + (float)j) / (float)this.nbSubframes;
					for (int i = 0; i < this.lpcSize; i++)
					{
						this.interp_qlsp[i] = (1f - num6) * this.old_qlsp[i] + num6 * this.qlsp[i];
					}
					Lsp.Enforce_margin(this.interp_qlsp, this.lpcSize, 0.05f);
					for (int i = 0; i < this.lpcSize; i++)
					{
						this.interp_qlsp[i] = (float)Math.Cos((double)this.interp_qlsp[i]);
					}
					this.m_lsp.Lsp2lpc(this.interp_qlsp, this.interp_qlpc, this.lpcSize);
					if (this.enhanced)
					{
						float lpcEnhK = this.submodes[this.submodeID].LpcEnhK1;
						float lpcEnhK2 = this.submodes[this.submodeID].LpcEnhK2;
						float gamma = lpcEnhK - lpcEnhK2;
						Filters.Bw_lpc(lpcEnhK, this.interp_qlpc, this.awk1, this.lpcSize);
						Filters.Bw_lpc(lpcEnhK2, this.interp_qlpc, this.awk2, this.lpcSize);
						Filters.Bw_lpc(gamma, this.interp_qlpc, this.awk3, this.lpcSize);
					}
					num6 = 1f;
					this.pi_gain[j] = 0f;
					for (int i = 0; i <= this.lpcSize; i++)
					{
						num4 += num6 * this.interp_qlpc[i];
						num6 = -num6;
						this.pi_gain[j] += this.interp_qlpc[i];
					}
					float value = piGain[j];
					value = 1f / (Math.Abs(value) + 0.01f);
					num4 = 1f / (Math.Abs(num4) + 0.01f);
					float num7 = Math.Abs(0.01f + num4) / (0.01f + Math.Abs(value));
					for (int i = num5; i < num5 + this.subframeSize; i++)
					{
						this.excBuf[i] = 0f;
					}
					if (this.submodes[this.submodeID].Innovation == null)
					{
						int num8 = bits.Unpack(5);
						float num9 = (float)Math.Exp(((double)num8 - 10.0) / 8.0);
						num9 /= num7;
						for (int i = num5; i < num5 + this.subframeSize; i++)
						{
							this.excBuf[i] = this.foldingGain * num9 * innov[i];
						}
					}
					else
					{
						int num10 = bits.Unpack(4);
						for (int i = num5; i < num5 + this.subframeSize; i++)
						{
							num3 += exc[i] * exc[i];
						}
						float num11 = (float)Math.Exp((double)(0.270270258f * (float)num10 - 2f));
						float num12 = num11 * (float)Math.Sqrt((double)(1f + num3)) / num7;
						this.submodes[this.submodeID].Innovation.Unquantify(this.excBuf, num5, this.subframeSize, bits);
						for (int i = num5; i < num5 + this.subframeSize; i++)
						{
							this.excBuf[i] *= num12;
						}
						if (this.submodes[this.submodeID].DoubleCodebook != 0)
						{
							for (int i = 0; i < this.subframeSize; i++)
							{
								this.innov2[i] = 0f;
							}
							this.submodes[this.submodeID].Innovation.Unquantify(this.innov2, 0, this.subframeSize, bits);
							for (int i = 0; i < this.subframeSize; i++)
							{
								this.innov2[i] *= num12 * 0.4f;
							}
							for (int i = 0; i < this.subframeSize; i++)
							{
								this.excBuf[num5 + i] += this.innov2[i];
							}
						}
					}
					for (int i = num5; i < num5 + this.subframeSize; i++)
					{
						this.high[i] = this.excBuf[i];
					}
					if (this.enhanced)
					{
						Filters.Filter_mem2(this.high, num5, this.awk2, this.awk1, this.subframeSize, this.lpcSize, this.mem_sp, this.lpcSize);
						Filters.Filter_mem2(this.high, num5, this.awk3, this.interp_qlpc, this.subframeSize, this.lpcSize, this.mem_sp, 0);
					}
					else
					{
						for (int i = 0; i < this.lpcSize; i++)
						{
							this.mem_sp[this.lpcSize + i] = 0f;
						}
						Filters.Iir_mem2(this.high, num5, this.interp_qlpc, this.high, num5, this.subframeSize, this.lpcSize, this.mem_sp);
					}
				}
				this.filters.Fir_mem_up(this.x0d, Codebook_Constants.h0, this.y0, this.fullFrameSize, 64, this.g0_mem);
				this.filters.Fir_mem_up(this.high, Codebook_Constants.h1, this.y1, this.fullFrameSize, 64, this.g1_mem);
				for (int i = 0; i < this.fullFrameSize; i++)
				{
					xout[i] = 2f * (this.y0[i] - this.y1[i]);
				}
				for (int i = 0; i < this.lpcSize; i++)
				{
					this.old_qlsp[i] = this.qlsp[i];
				}
				this.first = 0;
				return 0;
			}
			if (dtx)
			{
				this.DecodeLost(xout, true);
				return 0;
			}
			for (int i = 0; i < this.frameSize; i++)
			{
				this.excBuf[i] = 0f;
			}
			this.first = 1;
			Filters.Iir_mem2(this.excBuf, this.excIdx, this.interp_qlpc, this.high, 0, this.frameSize, this.lpcSize, this.mem_sp);
			this.filters.Fir_mem_up(this.x0d, Codebook_Constants.h0, this.y0, this.fullFrameSize, 64, this.g0_mem);
			this.filters.Fir_mem_up(this.high, Codebook_Constants.h1, this.y1, this.fullFrameSize, 64, this.g1_mem);
			for (int i = 0; i < this.fullFrameSize; i++)
			{
				xout[i] = 2f * (this.y0[i] - this.y1[i]);
			}
			return 0;
		}

		public int DecodeLost(float[] xout, bool dtx)
		{
			int submodeID = 0;
			if (dtx)
			{
				submodeID = this.submodeID;
				this.submodeID = 1;
			}
			else
			{
				Filters.Bw_lpc(0.99f, this.interp_qlpc, this.interp_qlpc, this.lpcSize);
			}
			this.first = 1;
			this.awk1 = new float[this.lpcSize + 1];
			this.awk2 = new float[this.lpcSize + 1];
			this.awk3 = new float[this.lpcSize + 1];
			if (this.enhanced)
			{
				float num;
				float num2;
				if (this.submodes[this.submodeID] != null)
				{
					num = this.submodes[this.submodeID].LpcEnhK1;
					num2 = this.submodes[this.submodeID].LpcEnhK2;
				}
				else
				{
					num2 = (num = 0.7f);
				}
				float gamma = num - num2;
				Filters.Bw_lpc(num, this.interp_qlpc, this.awk1, this.lpcSize);
				Filters.Bw_lpc(num2, this.interp_qlpc, this.awk2, this.lpcSize);
				Filters.Bw_lpc(gamma, this.interp_qlpc, this.awk3, this.lpcSize);
			}
			if (!dtx)
			{
				for (int i = 0; i < this.frameSize; i++)
				{
					this.excBuf[this.excIdx + i] *= new float?(0.9f).Value;
				}
			}
			for (int i = 0; i < this.frameSize; i++)
			{
				this.high[i] = this.excBuf[this.excIdx + i];
			}
			if (this.enhanced)
			{
				Filters.Filter_mem2(this.high, 0, this.awk2, this.awk1, this.high, 0, this.frameSize, this.lpcSize, this.mem_sp, this.lpcSize);
				Filters.Filter_mem2(this.high, 0, this.awk3, this.interp_qlpc, this.high, 0, this.frameSize, this.lpcSize, this.mem_sp, 0);
			}
			else
			{
				for (int i = 0; i < this.lpcSize; i++)
				{
					this.mem_sp[this.lpcSize + i] = 0f;
				}
				Filters.Iir_mem2(this.high, 0, this.interp_qlpc, this.high, 0, this.frameSize, this.lpcSize, this.mem_sp);
			}
			this.filters.Fir_mem_up(this.x0d, Codebook_Constants.h0, this.y0, this.fullFrameSize, 64, this.g0_mem);
			this.filters.Fir_mem_up(this.high, Codebook_Constants.h1, this.y1, this.fullFrameSize, 64, this.g1_mem);
			for (int i = 0; i < this.fullFrameSize; i++)
			{
				xout[i] = 2f * (this.y0[i] - this.y1[i]);
			}
			if (dtx)
			{
				this.submodeID = submodeID;
			}
			return 0;
		}

		public virtual void DecodeStereo(float[] data, int frameSize)
		{
			this.stereo.Decode(data, frameSize);
		}
	}
}
