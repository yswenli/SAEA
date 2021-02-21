using System;

namespace SAEA.Audio.Base.NSpeex
{
	internal class NbDecoder : NbCodec, IDecoder
	{
		private float[] innov2;

		private int count_lost;

		private int last_pitch;

		private float last_pitch_gain;

		private float[] pitch_gain_buf;

		private int pitch_gain_buf_idx;

		private float last_ol_gain;

		protected internal Random random;

		protected internal Stereo stereo;

		protected internal Inband inband;

		protected internal bool enhanced;

		public bool Dtx
		{
			get
			{
				return this.dtx_enabled != 0;
			}
		}

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

		public NbDecoder()
		{
			this.random = new Random();
			this.stereo = new Stereo();
			this.inband = new Inband(this.stereo);
			this.enhanced = true;
		}

		protected override void Init(int frameSize, int subframeSize, int lpcSize, int bufSize)
		{
			base.Init(frameSize, subframeSize, lpcSize, bufSize);
			this.innov2 = new float[40];
			this.count_lost = 0;
			this.last_pitch = 40;
			this.last_pitch_gain = 0f;
			this.pitch_gain_buf = new float[3];
			this.pitch_gain_buf_idx = 0;
			this.last_ol_gain = 0f;
		}

		public virtual int Decode(Bits bits, float[] xout)
		{
			int num = 0;
			float[] array = new float[3];
			float num2 = 0f;
			int num3 = 40;
			float num4 = 0f;
			float num5 = 0f;
			if (bits == null && this.dtx_enabled != 0)
			{
				this.submodeID = 0;
			}
			else
			{
				if (bits == null)
				{
					this.DecodeLost(xout);
					return 0;
				}
				while (bits.BitsRemaining() >= 5)
				{
					int num6;
					if (bits.Unpack(1) != 0)
					{
						num6 = bits.Unpack(3);
						int num7 = SbCodec.SB_FRAME_SIZE[num6];
						if (num7 < 0)
						{
							throw new InvalidFormatException("Invalid sideband mode encountered (1st sideband): " + num6);
						}
						num7 -= 4;
						bits.Advance(num7);
						if (bits.Unpack(1) != 0)
						{
							num6 = bits.Unpack(3);
							num7 = SbCodec.SB_FRAME_SIZE[num6];
							if (num7 < 0)
							{
								throw new InvalidFormatException("Invalid sideband mode encountered. (2nd sideband): " + num6);
							}
							num7 -= 4;
							bits.Advance(num7);
							if (bits.Unpack(1) != 0)
							{
								throw new InvalidFormatException("More than two sideband layers found");
							}
						}
					}
					if (bits.BitsRemaining() < 4)
					{
						return 1;
					}
					num6 = bits.Unpack(4);
					if (num6 == 15)
					{
						return 1;
					}
					if (num6 == 14)
					{
						this.inband.SpeexInbandRequest(bits);
					}
					else if (num6 == 13)
					{
						this.inband.UserInbandRequest(bits);
					}
					else if (num6 > 8)
					{
						throw new InvalidFormatException("Invalid mode encountered: " + num6);
					}
					if (num6 <= 8)
					{
						this.submodeID = num6;
						goto IL_16C;
					}
				}
				return -1;
			}
			IL_16C:
			Array.Copy(this.frmBuf, this.frameSize, this.frmBuf, 0, this.bufSize - this.frameSize);
			Array.Copy(this.excBuf, this.frameSize, this.excBuf, 0, this.bufSize - this.frameSize);
			if (this.submodes[this.submodeID] == null)
			{
				Filters.Bw_lpc(0.93f, this.interp_qlpc, this.lpc, 10);
				float num8 = 0f;
				for (int i = 0; i < this.frameSize; i++)
				{
					num8 += this.innov[i] * this.innov[i];
				}
				num8 = (float)Math.Sqrt((double)(num8 / (float)this.frameSize));
				for (int i = this.excIdx; i < this.excIdx + this.frameSize; i++)
				{
					this.excBuf[i] = (float)((double)(3f * num8) * (this.random.NextDouble() - 0.5));
				}
				this.first = 1;
				Filters.Iir_mem2(this.excBuf, this.excIdx, this.lpc, this.frmBuf, this.frmIdx, this.frameSize, this.lpcSize, this.mem_sp);
				xout[0] = this.frmBuf[this.frmIdx] + this.preemph * this.pre_mem;
				for (int i = 1; i < this.frameSize; i++)
				{
					xout[i] = this.frmBuf[this.frmIdx + i] + this.preemph * xout[i - 1];
				}
				this.pre_mem = xout[this.frameSize - 1];
				this.count_lost = 0;
				return 0;
			}
			this.submodes[this.submodeID].LsqQuant.Unquant(this.qlsp, this.lpcSize, bits);
			if (this.count_lost != 0)
			{
				float num9 = 0f;
				for (int i = 0; i < this.lpcSize; i++)
				{
					num9 += Math.Abs(this.old_qlsp[i] - this.qlsp[i]);
				}
				float num10 = (float)(0.6 * Math.Exp(-0.2 * (double)num9));
				for (int i = 0; i < 2 * this.lpcSize; i++)
				{
					this.mem_sp[i] *= num10;
				}
			}
			if (this.first != 0 || this.count_lost != 0)
			{
				for (int i = 0; i < this.lpcSize; i++)
				{
					this.old_qlsp[i] = this.qlsp[i];
				}
			}
			if (this.submodes[this.submodeID].LbrPitch != -1)
			{
				num = this.min_pitch + bits.Unpack(7);
			}
			if (this.submodes[this.submodeID].ForcedPitchGain != 0)
			{
				int num11 = bits.Unpack(4);
				num2 = 0.066667f * (float)num11;
			}
			int num12 = bits.Unpack(5);
			float num13 = (float)Math.Exp((double)num12 / 3.5);
			if (this.submodeID == 1)
			{
				int num14 = bits.Unpack(4);
				if (num14 == 15)
				{
					this.dtx_enabled = 1;
				}
				else
				{
					this.dtx_enabled = 0;
				}
			}
			if (this.submodeID > 1)
			{
				this.dtx_enabled = 0;
			}
			for (int j = 0; j < this.nbSubframes; j++)
			{
				int num15 = this.subframeSize * j;
				int num16 = this.frmIdx + num15;
				int num17 = this.excIdx + num15;
				float num18 = (1f + (float)j) / (float)this.nbSubframes;
				for (int i = 0; i < this.lpcSize; i++)
				{
					this.interp_qlsp[i] = (1f - num18) * this.old_qlsp[i] + num18 * this.qlsp[i];
				}
				Lsp.Enforce_margin(this.interp_qlsp, this.lpcSize, 0.002f);
				for (int i = 0; i < this.lpcSize; i++)
				{
					this.interp_qlsp[i] = (float)Math.Cos((double)this.interp_qlsp[i]);
				}
				this.m_lsp.Lsp2lpc(this.interp_qlsp, this.interp_qlpc, this.lpcSize);
				if (this.enhanced)
				{
					float num19 = 0.9f;
					float lpcEnhK = this.submodes[this.submodeID].LpcEnhK1;
					float lpcEnhK2 = this.submodes[this.submodeID].LpcEnhK2;
					float gamma = (1f - (1f - num19 * lpcEnhK) / (1f - num19 * lpcEnhK2)) / num19;
					Filters.Bw_lpc(lpcEnhK, this.interp_qlpc, this.awk1, this.lpcSize);
					Filters.Bw_lpc(lpcEnhK2, this.interp_qlpc, this.awk2, this.lpcSize);
					Filters.Bw_lpc(gamma, this.interp_qlpc, this.awk3, this.lpcSize);
				}
				num18 = 1f;
				this.pi_gain[j] = 0f;
				for (int i = 0; i <= this.lpcSize; i++)
				{
					this.pi_gain[j] += num18 * this.interp_qlpc[i];
					num18 = -num18;
				}
				for (int i = 0; i < this.subframeSize; i++)
				{
					this.excBuf[num17 + i] = 0f;
				}
				int num20;
				if (this.submodes[this.submodeID].LbrPitch != -1)
				{
					int lbrPitch = this.submodes[this.submodeID].LbrPitch;
					if (lbrPitch != 0)
					{
						num20 = num - lbrPitch + 1;
						if (num20 < this.min_pitch)
						{
							num20 = this.min_pitch;
						}
						int num21 = num + lbrPitch;
						if (num21 > this.max_pitch)
						{
							num21 = this.max_pitch;
						}
					}
					else
					{
						num20 = num;
					}
				}
				else
				{
					num20 = this.min_pitch;
					int num21 = this.max_pitch;
				}
				int num22 = this.submodes[this.submodeID].Ltp.Unquant(this.excBuf, num17, num20, num2, this.subframeSize, array, bits, this.count_lost, num15, this.last_pitch_gain);
				if (this.count_lost != 0 && num13 < this.last_ol_gain)
				{
					float num23 = num13 / (this.last_ol_gain + 1f);
					for (int i = 0; i < this.subframeSize; i++)
					{
						this.excBuf[this.excIdx + i] *= num23;
					}
				}
				num18 = Math.Abs(array[0] + array[1] + array[2]);
				num18 = Math.Abs(array[1]);
				if (array[0] > 0f)
				{
					num18 += array[0];
				}
				else
				{
					num18 -= 0.5f * array[0];
				}
				if (array[2] > 0f)
				{
					num18 += array[2];
				}
				else
				{
					num18 -= 0.5f * array[0];
				}
				num5 += num18;
				if (num18 > num4)
				{
					num3 = num22;
					num4 = num18;
				}
				int num24 = j * this.subframeSize;
				for (int i = num24; i < num24 + this.subframeSize; i++)
				{
					this.innov[i] = 0f;
				}
				float num26;
				if (this.submodes[this.submodeID].HaveSubframeGain == 3)
				{
					int num25 = bits.Unpack(3);
					num26 = (float)((double)num13 * Math.Exp((double)NbCodec.exc_gain_quant_scal3[num25]));
				}
				else if (this.submodes[this.submodeID].HaveSubframeGain == 1)
				{
					int num25 = bits.Unpack(1);
					num26 = (float)((double)num13 * Math.Exp((double)NbCodec.exc_gain_quant_scal1[num25]));
				}
				else
				{
					num26 = num13;
				}
				if (this.submodes[this.submodeID].Innovation != null)
				{
					this.submodes[this.submodeID].Innovation.Unquantify(this.innov, num24, this.subframeSize, bits);
				}
				for (int i = num24; i < num24 + this.subframeSize; i++)
				{
					this.innov[i] *= num26;
				}
				if (this.submodeID == 1)
				{
					float num27 = num2;
					for (int i = 0; i < this.subframeSize; i++)
					{
						this.excBuf[num17 + i] = 0f;
					}
					while (this.voc_offset < this.subframeSize)
					{
						if (this.voc_offset >= 0)
						{
							this.excBuf[num17 + this.voc_offset] = (float)Math.Sqrt((double)(1f * (float)num));
						}
						this.voc_offset += num;
					}
					this.voc_offset -= this.subframeSize;
					num27 = 0.5f + 2f * (num27 - 0.6f);
					if (num27 < 0f)
					{
						num27 = 0f;
					}
					if (num27 > 1f)
					{
						num27 = 1f;
					}
					for (int i = 0; i < this.subframeSize; i++)
					{
						float voc_m = this.excBuf[num17 + i];
						this.excBuf[num17 + i] = 0.8f * num27 * this.excBuf[num17 + i] * num13 + 0.6f * num27 * this.voc_m1 * num13 + 0.5f * num27 * this.innov[num24 + i] - 0.5f * num27 * this.voc_m2 + (1f - num27) * this.innov[num24 + i];
						this.voc_m1 = voc_m;
						this.voc_m2 = this.innov[num24 + i];
						this.voc_mean = 0.95f * this.voc_mean + 0.05f * this.excBuf[num17 + i];
						this.excBuf[num17 + i] -= this.voc_mean;
					}
				}
				else
				{
					for (int i = 0; i < this.subframeSize; i++)
					{
						this.excBuf[num17 + i] += this.innov[num24 + i];
					}
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
						this.innov2[i] *= num26 * 0.454545438f;
					}
					for (int i = 0; i < this.subframeSize; i++)
					{
						this.excBuf[num17 + i] += this.innov2[i];
					}
				}
				for (int i = 0; i < this.subframeSize; i++)
				{
					this.frmBuf[num16 + i] = this.excBuf[num17 + i];
				}
				if (this.enhanced && this.submodes[this.submodeID].CombGain > 0f)
				{
					this.filters.Comb_filter(this.excBuf, num17, this.frmBuf, num16, this.subframeSize, num22, array, this.submodes[this.submodeID].CombGain);
				}
				if (this.enhanced)
				{
					Filters.Filter_mem2(this.frmBuf, num16, this.awk2, this.awk1, this.subframeSize, this.lpcSize, this.mem_sp, this.lpcSize);
					Filters.Filter_mem2(this.frmBuf, num16, this.awk3, this.interp_qlpc, this.subframeSize, this.lpcSize, this.mem_sp, 0);
				}
				else
				{
					for (int i = 0; i < this.lpcSize; i++)
					{
						this.mem_sp[this.lpcSize + i] = 0f;
					}
					Filters.Iir_mem2(this.frmBuf, num16, this.interp_qlpc, this.frmBuf, num16, this.subframeSize, this.lpcSize, this.mem_sp);
				}
			}
			xout[0] = this.frmBuf[this.frmIdx] + this.preemph * this.pre_mem;
			for (int i = 1; i < this.frameSize; i++)
			{
				xout[i] = this.frmBuf[this.frmIdx + i] + this.preemph * xout[i - 1];
			}
			this.pre_mem = xout[this.frameSize - 1];
			for (int i = 0; i < this.lpcSize; i++)
			{
				this.old_qlsp[i] = this.qlsp[i];
			}
			this.first = 0;
			this.count_lost = 0;
			this.last_pitch = num3;
			this.last_pitch_gain = 0.25f * num5;
			this.pitch_gain_buf[this.pitch_gain_buf_idx++] = this.last_pitch_gain;
			if (this.pitch_gain_buf_idx > 2)
			{
				this.pitch_gain_buf_idx = 0;
			}
			this.last_ol_gain = num13;
			return 0;
		}

		public int DecodeLost(float[] xout)
		{
			float num = (float)Math.Exp(-0.04 * (double)this.count_lost * (double)this.count_lost);
			float num2 = (this.pitch_gain_buf[0] < this.pitch_gain_buf[1]) ? ((this.pitch_gain_buf[1] < this.pitch_gain_buf[2]) ? this.pitch_gain_buf[1] : ((this.pitch_gain_buf[0] < this.pitch_gain_buf[2]) ? this.pitch_gain_buf[2] : this.pitch_gain_buf[0])) : ((this.pitch_gain_buf[2] < this.pitch_gain_buf[1]) ? this.pitch_gain_buf[1] : ((this.pitch_gain_buf[2] < this.pitch_gain_buf[0]) ? this.pitch_gain_buf[2] : this.pitch_gain_buf[0]));
			if (num2 < this.last_pitch_gain)
			{
				this.last_pitch_gain = num2;
			}
			float num3 = this.last_pitch_gain;
			if (num3 > 0.95f)
			{
				num3 = 0.95f;
			}
			num3 *= num;
			Array.Copy(this.frmBuf, this.frameSize, this.frmBuf, 0, this.bufSize - this.frameSize);
			Array.Copy(this.excBuf, this.frameSize, this.excBuf, 0, this.bufSize - this.frameSize);
			for (int i = 0; i < this.nbSubframes; i++)
			{
				int num4 = this.subframeSize * i;
				int num5 = this.frmIdx + num4;
				int num6 = this.excIdx + num4;
				if (this.enhanced)
				{
					float num7 = 0.9f;
					float num8;
					float num9;
					if (this.submodes[this.submodeID] != null)
					{
						num8 = this.submodes[this.submodeID].LpcEnhK1;
						num9 = this.submodes[this.submodeID].LpcEnhK2;
					}
					else
					{
						num9 = (num8 = 0.7f);
					}
					float gamma = (1f - (1f - num7 * num8) / (1f - num7 * num9)) / num7;
					Filters.Bw_lpc(num8, this.interp_qlpc, this.awk1, this.lpcSize);
					Filters.Bw_lpc(num9, this.interp_qlpc, this.awk2, this.lpcSize);
					Filters.Bw_lpc(gamma, this.interp_qlpc, this.awk3, this.lpcSize);
				}
				float num10 = 0f;
				for (int j = 0; j < this.frameSize; j++)
				{
					num10 += this.innov[j] * this.innov[j];
				}
				num10 = (float)Math.Sqrt((double)(num10 / (float)this.frameSize));
				for (int j = 0; j < this.subframeSize; j++)
				{
					this.excBuf[num6 + j] = num3 * this.excBuf[num6 + j - this.last_pitch] + num * (float)Math.Sqrt((double)(1f - num3)) * 3f * num10 * ((float)this.random.NextDouble() - 0.5f);
				}
				for (int j = 0; j < this.subframeSize; j++)
				{
					this.frmBuf[num5 + j] = this.excBuf[num6 + j];
				}
				if (this.enhanced)
				{
					Filters.Filter_mem2(this.frmBuf, num5, this.awk2, this.awk1, this.subframeSize, this.lpcSize, this.mem_sp, this.lpcSize);
					Filters.Filter_mem2(this.frmBuf, num5, this.awk3, this.interp_qlpc, this.subframeSize, this.lpcSize, this.mem_sp, 0);
				}
				else
				{
					for (int j = 0; j < this.lpcSize; j++)
					{
						this.mem_sp[this.lpcSize + j] = 0f;
					}
					Filters.Iir_mem2(this.frmBuf, num5, this.interp_qlpc, this.frmBuf, num5, this.subframeSize, this.lpcSize, this.mem_sp);
				}
			}
			xout[0] = this.frmBuf[0] + this.preemph * this.pre_mem;
			for (int j = 1; j < this.frameSize; j++)
			{
				xout[j] = this.frmBuf[j] + this.preemph * xout[j - 1];
			}
			this.pre_mem = xout[this.frameSize - 1];
			this.first = 0;
			this.count_lost++;
			this.pitch_gain_buf[this.pitch_gain_buf_idx++] = num3;
			if (this.pitch_gain_buf_idx > 2)
			{
				this.pitch_gain_buf_idx = 0;
			}
			return 0;
		}

		public virtual void DecodeStereo(float[] data, int frameSize)
		{
			this.stereo.Decode(data, frameSize);
		}
	}
}
