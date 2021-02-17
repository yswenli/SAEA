using System;

namespace SAEA.Audio.Base.NAudio.Dsp
{
	internal class WdlResampler
	{
		private class WDL_Resampler_IIRFilter
		{
			private double m_fpos;

			private double m_a1;

			private double m_a2;

			private double m_b0;

			private double m_b1;

			private double m_b2;

			private double[,] m_hist;

			public WDL_Resampler_IIRFilter()
			{
				this.m_fpos = -1.0;
				this.Reset();
			}

			public void Reset()
			{
				this.m_hist = new double[256, 4];
			}

			public void setParms(double fpos, double Q)
			{
				if (Math.Abs(fpos - this.m_fpos) < 1E-06)
				{
					return;
				}
				this.m_fpos = fpos;
				double expr_2B = fpos * 3.1415926535897931;
				double num = Math.Cos(expr_2B);
				double num2 = Math.Sin(expr_2B) / (2.0 * Q);
				double num3 = 1.0 / (1.0 + num2);
				this.m_b1 = (1.0 - num) * num3;
				this.m_b2 = (this.m_b0 = this.m_b1 * 0.5);
				this.m_a1 = -2.0 * num * num3;
				this.m_a2 = (1.0 - num2) * num3;
			}

			public void Apply(float[] inBuffer, int inIndex, float[] outBuffer, int outIndex, int ns, int span, int w)
			{
				double b = this.m_b0;
				double b2 = this.m_b1;
				double b3 = this.m_b2;
				double a = this.m_a1;
				double a2 = this.m_a2;
				while (ns-- != 0)
				{
					double num = (double)inBuffer[inIndex];
					inIndex += span;
					double x = num * b + this.m_hist[w, 0] * b2 + this.m_hist[w, 1] * b3 - this.m_hist[w, 2] * a - this.m_hist[w, 3] * a2;
					this.m_hist[w, 1] = this.m_hist[w, 0];
					this.m_hist[w, 0] = num;
					this.m_hist[w, 3] = this.m_hist[w, 2];
					this.m_hist[w, 2] = this.denormal_filter(x);
					outBuffer[outIndex] = (float)this.m_hist[w, 2];
					outIndex += span;
				}
			}

			private double denormal_filter(float x)
			{
				return (double)x;
			}

			private double denormal_filter(double x)
			{
				return x;
			}
		}

		private const int WDL_RESAMPLE_MAX_FILTERS = 4;

		private const int WDL_RESAMPLE_MAX_NCH = 64;

		private const double PI = 3.1415926535897931;

		private double m_sratein;

		private double m_srateout;

		private double m_fracpos;

		private double m_ratio;

		private double m_filter_ratio;

		private float m_filterq;

		private float m_filterpos;

		private float[] m_rsinbuf;

		private float[] m_filter_coeffs;

		private WdlResampler.WDL_Resampler_IIRFilter m_iirfilter;

		private int m_filter_coeffs_size;

		private int m_last_requested;

		private int m_filtlatency;

		private int m_samples_in_rsinbuf;

		private int m_lp_oversize;

		private int m_sincsize;

		private int m_filtercnt;

		private int m_sincoversize;

		private bool m_interp;

		private bool m_feedmode;

		public WdlResampler()
		{
			this.m_filterq = 0.707f;
			this.m_filterpos = 0.693f;
			this.m_sincoversize = 0;
			this.m_lp_oversize = 1;
			this.m_sincsize = 0;
			this.m_filtercnt = 1;
			this.m_interp = true;
			this.m_feedmode = false;
			this.m_filter_coeffs_size = 0;
			this.m_sratein = 44100.0;
			this.m_srateout = 44100.0;
			this.m_ratio = 1.0;
			this.m_filter_ratio = -1.0;
			this.Reset(0.0);
		}

		public void SetMode(bool interp, int filtercnt, bool sinc, int sinc_size = 64, int sinc_interpsize = 32)
		{
			this.m_sincsize = ((sinc && sinc_size >= 4) ? ((sinc_size > 8192) ? 8192 : sinc_size) : 0);
			this.m_sincoversize = ((this.m_sincsize != 0) ? ((sinc_interpsize <= 1) ? 1 : ((sinc_interpsize >= 4096) ? 4096 : sinc_interpsize)) : 1);
			this.m_filtercnt = ((this.m_sincsize != 0) ? 0 : ((filtercnt <= 0) ? 0 : ((filtercnt >= 4) ? 4 : filtercnt)));
			this.m_interp = (interp && this.m_sincsize == 0);
			if (this.m_sincsize == 0)
			{
				this.m_filter_coeffs = new float[0];
				this.m_filter_coeffs_size = 0;
			}
			if (this.m_filtercnt == 0)
			{
				this.m_iirfilter = null;
			}
		}

		public void SetFilterParms(float filterpos = 0.693f, float filterq = 0.707f)
		{
			this.m_filterpos = filterpos;
			this.m_filterq = filterq;
		}

		public void SetFeedMode(bool wantInputDriven)
		{
			this.m_feedmode = wantInputDriven;
		}

		public void Reset(double fracpos = 0.0)
		{
			this.m_last_requested = 0;
			this.m_filtlatency = 0;
			this.m_fracpos = fracpos;
			this.m_samples_in_rsinbuf = 0;
			if (this.m_iirfilter != null)
			{
				this.m_iirfilter.Reset();
			}
		}

		public void SetRates(double rate_in, double rate_out)
		{
			if (rate_in < 1.0)
			{
				rate_in = 1.0;
			}
			if (rate_out < 1.0)
			{
				rate_out = 1.0;
			}
			if (rate_in != this.m_sratein || rate_out != this.m_srateout)
			{
				this.m_sratein = rate_in;
				this.m_srateout = rate_out;
				this.m_ratio = this.m_sratein / this.m_srateout;
			}
		}

		public double GetCurrentLatency()
		{
			double num = ((double)this.m_samples_in_rsinbuf - (double)this.m_filtlatency) / this.m_sratein;
			if (num < 0.0)
			{
				num = 0.0;
			}
			return num;
		}

		public int ResamplePrepare(int out_samples, int nch, out float[] inbuffer, out int inbufferOffset)
		{
			if (nch > 64 || nch < 1)
			{
				inbuffer = null;
				inbufferOffset = 0;
				return 0;
			}
			int num = 0;
			if (this.m_sincsize > 1)
			{
				num = this.m_sincsize;
			}
			int num2 = num / 2;
			if (num2 > 1 && this.m_samples_in_rsinbuf < num2 - 1)
			{
				this.m_filtlatency += num2 - 1 - this.m_samples_in_rsinbuf;
				this.m_samples_in_rsinbuf = num2 - 1;
				if (this.m_samples_in_rsinbuf > 0)
				{
					this.m_rsinbuf = new float[this.m_samples_in_rsinbuf * nch];
				}
			}
			int num3;
			if (!this.m_feedmode)
			{
				num3 = (int)(this.m_ratio * (double)out_samples) + 4 + num - this.m_samples_in_rsinbuf;
			}
			else
			{
				num3 = out_samples;
			}
			if (num3 < 0)
			{
				num3 = 0;
			}
			int num4;
			while (true)
			{
				Array.Resize<float>(ref this.m_rsinbuf, (this.m_samples_in_rsinbuf + num3) * nch);
				num4 = this.m_rsinbuf.Length / ((nch != 0) ? nch : 1) - this.m_samples_in_rsinbuf;
				if (num4 == num3)
				{
					goto IL_DD;
				}
				if (num3 <= 4 || num4 != 0)
				{
					break;
				}
				num3 /= 2;
			}
			num3 = num4;
			IL_DD:
			inbuffer = this.m_rsinbuf;
			inbufferOffset = this.m_samples_in_rsinbuf * nch;
			this.m_last_requested = num3;
			return num3;
		}

		public int ResampleOut(float[] outBuffer, int outBufferIndex, int nsamples_in, int nsamples_out, int nch)
		{
			if (nch > 64 || nch < 1)
			{
				return 0;
			}
			if (this.m_filtercnt > 0 && this.m_ratio > 1.0 && nsamples_in > 0)
			{
				if (this.m_iirfilter == null)
				{
					this.m_iirfilter = new WdlResampler.WDL_Resampler_IIRFilter();
				}
				int filtercnt = this.m_filtercnt;
				this.m_iirfilter.setParms(1.0 / this.m_ratio * (double)this.m_filterpos, (double)this.m_filterq);
				int num = this.m_samples_in_rsinbuf * nch;
				int num2 = 0;
				for (int i = 0; i < nch; i++)
				{
					for (int j = 0; j < filtercnt; j++)
					{
						this.m_iirfilter.Apply(this.m_rsinbuf, num + i, this.m_rsinbuf, num + i, nsamples_in, nch, num2++);
					}
				}
			}
			this.m_samples_in_rsinbuf += Math.Min(nsamples_in, this.m_last_requested);
			int num3 = this.m_samples_in_rsinbuf;
			if (nsamples_in < this.m_last_requested)
			{
				int num4 = (this.m_last_requested - nsamples_in) * 2 + this.m_sincsize * 2;
				int num5 = (this.m_samples_in_rsinbuf + num4) * nch;
				Array.Resize<float>(ref this.m_rsinbuf, num5);
				if (this.m_rsinbuf.Length == num5)
				{
					Array.Clear(this.m_rsinbuf, this.m_samples_in_rsinbuf * nch, num4 * nch);
					num3 = this.m_samples_in_rsinbuf + num4;
				}
			}
			int num6 = 0;
			double num7 = this.m_fracpos;
			double ratio = this.m_ratio;
			int num8 = 0;
			int num9 = outBufferIndex;
			int num10 = nsamples_out;
			int num11 = 0;
			if (this.m_sincsize != 0)
			{
				if (this.m_ratio > 1.0)
				{
					this.BuildLowPass(1.0 / (this.m_ratio * 1.03));
				}
				else
				{
					this.BuildLowPass(1.0);
				}
				int filter_coeffs_size = this.m_filter_coeffs_size;
				int num12 = num3 - filter_coeffs_size;
				num11 = filter_coeffs_size / 2 - 1;
				int filterIndex = 0;
				if (nch == 1)
				{
					while (num10-- != 0)
					{
						int num13 = (int)num7;
						if (num13 >= num12 - 1)
						{
							break;
						}
						this.SincSample1(outBuffer, num9, this.m_rsinbuf, num8 + num13, num7 - (double)num13, this.m_filter_coeffs, filterIndex, filter_coeffs_size);
						num9++;
						num7 += ratio;
						num6++;
					}
				}
				else if (nch == 2)
				{
					while (num10-- != 0)
					{
						int num14 = (int)num7;
						if (num14 >= num12 - 1)
						{
							break;
						}
						this.SincSample2(outBuffer, num9, this.m_rsinbuf, num8 + num14 * 2, num7 - (double)num14, this.m_filter_coeffs, filterIndex, filter_coeffs_size);
						num9 += 2;
						num7 += ratio;
						num6++;
					}
				}
				else
				{
					while (num10-- != 0)
					{
						int num15 = (int)num7;
						if (num15 >= num12 - 1)
						{
							break;
						}
						this.SincSample(outBuffer, num9, this.m_rsinbuf, num8 + num15 * nch, num7 - (double)num15, nch, this.m_filter_coeffs, filterIndex, filter_coeffs_size);
						num9 += nch;
						num7 += ratio;
						num6++;
					}
				}
			}
			else if (!this.m_interp)
			{
				if (nch == 1)
				{
					while (num10-- != 0)
					{
						int num16 = (int)num7;
						if (num16 >= num3)
						{
							break;
						}
						outBuffer[num9++] = this.m_rsinbuf[num8 + num16];
						num7 += ratio;
						num6++;
					}
				}
				else if (nch == 2)
				{
					while (num10-- != 0)
					{
						int num17 = (int)num7;
						if (num17 >= num3)
						{
							break;
						}
						num17 += num17;
						outBuffer[num9] = this.m_rsinbuf[num8 + num17];
						outBuffer[num9 + 1] = this.m_rsinbuf[num8 + num17 + 1];
						num9 += 2;
						num7 += ratio;
						num6++;
					}
				}
				else
				{
					while (num10-- != 0)
					{
						int num18 = (int)num7;
						if (num18 >= num3)
						{
							break;
						}
						Array.Copy(this.m_rsinbuf, num8 + num18 * nch, outBuffer, num9, nch);
						num9 += nch;
						num7 += ratio;
						num6++;
					}
				}
			}
			else if (nch == 1)
			{
				while (num10-- != 0)
				{
					int num19 = (int)num7;
					double num20 = num7 - (double)num19;
					if (num19 >= num3 - 1)
					{
						break;
					}
					double num21 = 1.0 - num20;
					int num22 = num8 + num19;
					outBuffer[num9++] = (float)((double)this.m_rsinbuf[num22] * num21 + (double)this.m_rsinbuf[num22 + 1] * num20);
					num7 += ratio;
					num6++;
				}
			}
			else if (nch == 2)
			{
				while (num10-- != 0)
				{
					int num23 = (int)num7;
					double num24 = num7 - (double)num23;
					if (num23 >= num3 - 1)
					{
						break;
					}
					double num25 = 1.0 - num24;
					int num26 = num8 + num23 * 2;
					outBuffer[num9] = (float)((double)this.m_rsinbuf[num26] * num25 + (double)this.m_rsinbuf[num26 + 2] * num24);
					outBuffer[num9 + 1] = (float)((double)this.m_rsinbuf[num26 + 1] * num25 + (double)this.m_rsinbuf[num26 + 3] * num24);
					num9 += 2;
					num7 += ratio;
					num6++;
				}
			}
			else
			{
				while (num10-- != 0)
				{
					int num27 = (int)num7;
					double num28 = num7 - (double)num27;
					if (num27 >= num3 - 1)
					{
						break;
					}
					double num29 = 1.0 - num28;
					int num30 = nch;
					int num31 = num8 + num27 * nch;
					while (num30-- != 0)
					{
						outBuffer[num9++] = (float)((double)this.m_rsinbuf[num31] * num29 + (double)this.m_rsinbuf[num31 + nch] * num28);
						num31++;
					}
					num7 += ratio;
					num6++;
				}
			}
			if (this.m_filtercnt > 0 && this.m_ratio < 1.0 && num6 > 0)
			{
				if (this.m_iirfilter == null)
				{
					this.m_iirfilter = new WdlResampler.WDL_Resampler_IIRFilter();
				}
				int filtercnt2 = this.m_filtercnt;
				this.m_iirfilter.setParms(this.m_ratio * (double)this.m_filterpos, (double)this.m_filterq);
				int num32 = 0;
				for (int k = 0; k < nch; k++)
				{
					for (int l = 0; l < filtercnt2; l++)
					{
						this.m_iirfilter.Apply(outBuffer, k, outBuffer, k, num6, nch, num32++);
					}
				}
			}
			if (num6 > 0 && num3 > this.m_samples_in_rsinbuf)
			{
				double num33 = (num7 - (double)this.m_samples_in_rsinbuf + (double)num11) / ratio;
				if (num33 > 0.0)
				{
					num6 -= (int)(num33 + 0.5);
					if (num6 < 0)
					{
						num6 = 0;
					}
				}
			}
			int num34 = (int)num7;
			this.m_fracpos = num7 - (double)num34;
			this.m_samples_in_rsinbuf -= num34;
			if (this.m_samples_in_rsinbuf <= 0)
			{
				this.m_samples_in_rsinbuf = 0;
			}
			else
			{
				Array.Copy(this.m_rsinbuf, num8 + num34 * nch, this.m_rsinbuf, num8, this.m_samples_in_rsinbuf * nch);
			}
			return num6;
		}

		private void BuildLowPass(double filtpos)
		{
			int sincsize = this.m_sincsize;
			int sincoversize = this.m_sincoversize;
			if (this.m_filter_ratio != filtpos || this.m_filter_coeffs_size != sincsize || this.m_lp_oversize != sincoversize)
			{
				this.m_lp_oversize = sincoversize;
				this.m_filter_ratio = filtpos;
				int num = (sincsize + 1) * this.m_lp_oversize;
				Array.Resize<float>(ref this.m_filter_coeffs, num);
				if (this.m_filter_coeffs.Length == num)
				{
					this.m_filter_coeffs_size = sincsize;
					int num2 = sincsize * this.m_lp_oversize;
					int num3 = num2 / 2;
					double num4 = 0.0;
					double num5 = 0.0;
					double num6 = 6.2831853071795862 / (double)num2;
					double num7 = 3.1415926535897931 / (double)this.m_lp_oversize * filtpos;
					double num8 = num7 * (double)(-(double)num3);
					for (int i = -num3; i < num3 + this.m_lp_oversize; i++)
					{
						double num9 = 0.35875 - 0.48829 * Math.Cos(num5) + 0.14128 * Math.Cos(2.0 * num5) - 0.01168 * Math.Cos(6.0 * num5);
						if (i != 0)
						{
							num9 *= Math.Sin(num8) / num8;
						}
						num5 += num6;
						num8 += num7;
						this.m_filter_coeffs[num3 + i] = (float)num9;
						if (i < num3)
						{
							num4 += num9;
						}
					}
					num4 = (double)this.m_lp_oversize / num4;
					for (int i = 0; i < num2 + this.m_lp_oversize; i++)
					{
						this.m_filter_coeffs[i] = (float)((double)this.m_filter_coeffs[i] * num4);
					}
					return;
				}
				this.m_filter_coeffs_size = 0;
			}
		}

		private void SincSample(float[] outBuffer, int outBufferIndex, float[] inBuffer, int inBufferIndex, double fracpos, int nch, float[] filter, int filterIndex, int filtsz)
		{
			int lp_oversize = this.m_lp_oversize;
			fracpos *= (double)lp_oversize;
			int num = (int)fracpos;
			filterIndex += lp_oversize - 1 - num;
			fracpos -= (double)num;
			for (int i = 0; i < nch; i++)
			{
				double num2 = 0.0;
				double num3 = 0.0;
				int num4 = filterIndex;
				int num5 = inBufferIndex + i;
				int num6 = filtsz;
				while (num6-- != 0)
				{
					num2 += (double)(filter[num4] * inBuffer[num5]);
					num3 += (double)(filter[num4 + 1] * inBuffer[num5]);
					num5 += nch;
					num4 += lp_oversize;
				}
				outBuffer[outBufferIndex + i] = (float)(num2 * fracpos + num3 * (1.0 - fracpos));
			}
		}

		private void SincSample1(float[] outBuffer, int outBufferIndex, float[] inBuffer, int inBufferIndex, double fracpos, float[] filter, int filterIndex, int filtsz)
		{
			int lp_oversize = this.m_lp_oversize;
			fracpos *= (double)lp_oversize;
			int num = (int)fracpos;
			filterIndex += lp_oversize - 1 - num;
			fracpos -= (double)num;
			double num2 = 0.0;
			double num3 = 0.0;
			int num4 = filterIndex;
			int num5 = inBufferIndex;
			int num6 = filtsz;
			while (num6-- != 0)
			{
				num2 += (double)(filter[num4] * inBuffer[num5]);
				num3 += (double)(filter[num4 + 1] * inBuffer[num5]);
				num5++;
				num4 += lp_oversize;
			}
			outBuffer[outBufferIndex] = (float)(num2 * fracpos + num3 * (1.0 - fracpos));
		}

		private void SincSample2(float[] outptr, int outBufferIndex, float[] inBuffer, int inBufferIndex, double fracpos, float[] filter, int filterIndex, int filtsz)
		{
			int lp_oversize = this.m_lp_oversize;
			fracpos *= (double)lp_oversize;
			int num = (int)fracpos;
			filterIndex += lp_oversize - 1 - num;
			fracpos -= (double)num;
			double num2 = 0.0;
			double num3 = 0.0;
			double num4 = 0.0;
			double num5 = 0.0;
			int num6 = filterIndex;
			int num7 = inBufferIndex;
			int num8 = filtsz / 2;
			while (num8-- != 0)
			{
				num2 += (double)(filter[num6] * inBuffer[num7]);
				num3 += (double)(filter[num6] * inBuffer[num7 + 1]);
				num4 += (double)(filter[num6 + 1] * inBuffer[num7]);
				num5 += (double)(filter[num6 + 1] * inBuffer[num7 + 1]);
				num2 += (double)(filter[num6 + lp_oversize] * inBuffer[num7 + 2]);
				num3 += (double)(filter[num6 + lp_oversize] * inBuffer[num7 + 3]);
				num4 += (double)(filter[num6 + lp_oversize + 1] * inBuffer[num7 + 2]);
				num5 += (double)(filter[num6 + lp_oversize + 1] * inBuffer[num7 + 3]);
				num7 += 4;
				num6 += lp_oversize * 2;
			}
			outptr[outBufferIndex] = (float)(num2 * fracpos + num4 * (1.0 - fracpos));
			outptr[outBufferIndex + 1] = (float)(num3 * fracpos + num5 * (1.0 - fracpos));
		}
	}
}
