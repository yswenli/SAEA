using System;

namespace SAEA.Audio.Base.NAudio.Dsp
{
	public class BiQuadFilter
	{
		private double a0;

		private double a1;

		private double a2;

		private double a3;

		private double a4;

		private float x1;

		private float x2;

		private float y1;

		private float y2;

		public float Transform(float inSample)
		{
			double num = this.a0 * (double)inSample + this.a1 * (double)this.x1 + this.a2 * (double)this.x2 - this.a3 * (double)this.y1 - this.a4 * (double)this.y2;
			this.x2 = this.x1;
			this.x1 = inSample;
			this.y2 = this.y1;
			this.y1 = (float)num;
			return this.y1;
		}

		private void SetCoefficients(double aa0, double aa1, double aa2, double b0, double b1, double b2)
		{
			this.a0 = b0 / aa0;
			this.a1 = b1 / aa0;
			this.a2 = b2 / aa0;
			this.a3 = aa1 / aa0;
			this.a4 = aa2 / aa0;
		}

		public void SetLowPassFilter(float sampleRate, float cutoffFrequency, float q)
		{
			double expr_0F = 6.2831853071795862 * (double)cutoffFrequency / (double)sampleRate;
			double num = Math.Cos(expr_0F);
			double num2 = Math.Sin(expr_0F) / (double)(2f * q);
			double b = (1.0 - num) / 2.0;
			double b2 = 1.0 - num;
			double b3 = (1.0 - num) / 2.0;
			double aa = 1.0 + num2;
			double aa2 = -2.0 * num;
			double aa3 = 1.0 - num2;
			this.SetCoefficients(aa, aa2, aa3, b, b2, b3);
		}

		public void SetPeakingEq(float sampleRate, float centreFrequency, float q, float dbGain)
		{
			double expr_0F = 6.2831853071795862 * (double)centreFrequency / (double)sampleRate;
			double num = Math.Cos(expr_0F);
			double num2 = Math.Sin(expr_0F) / (double)(2f * q);
			double num3 = Math.Pow(10.0, (double)(dbGain / 40f));
			double b = 1.0 + num2 * num3;
			double b2 = -2.0 * num;
			double b3 = 1.0 - num2 * num3;
			double aa = 1.0 + num2 / num3;
			double aa2 = -2.0 * num;
			double aa3 = 1.0 - num2 / num3;
			this.SetCoefficients(aa, aa2, aa3, b, b2, b3);
		}

		public void SetHighPassFilter(float sampleRate, float cutoffFrequency, float q)
		{
			double expr_0F = 6.2831853071795862 * (double)cutoffFrequency / (double)sampleRate;
			double num = Math.Cos(expr_0F);
			double num2 = Math.Sin(expr_0F) / (double)(2f * q);
			double b = (1.0 + num) / 2.0;
			double b2 = -(1.0 + num);
			double b3 = (1.0 + num) / 2.0;
			double aa = 1.0 + num2;
			double aa2 = -2.0 * num;
			double aa3 = 1.0 - num2;
			this.SetCoefficients(aa, aa2, aa3, b, b2, b3);
		}

		public static BiQuadFilter LowPassFilter(float sampleRate, float cutoffFrequency, float q)
		{
			BiQuadFilter expr_05 = new BiQuadFilter();
			expr_05.SetLowPassFilter(sampleRate, cutoffFrequency, q);
			return expr_05;
		}

		public static BiQuadFilter HighPassFilter(float sampleRate, float cutoffFrequency, float q)
		{
			BiQuadFilter expr_05 = new BiQuadFilter();
			expr_05.SetHighPassFilter(sampleRate, cutoffFrequency, q);
			return expr_05;
		}

		public static BiQuadFilter BandPassFilterConstantSkirtGain(float sampleRate, float centreFrequency, float q)
		{
			double expr_0F = 6.2831853071795862 * (double)centreFrequency / (double)sampleRate;
			double num = Math.Cos(expr_0F);
			double expr_1B = Math.Sin(expr_0F);
			double num2 = expr_1B / (double)(2f * q);
			double b = expr_1B / 2.0;
			int num3 = 0;
			double b2 = -expr_1B / 2.0;
			double arg_6F_0 = 1.0 + num2;
			double num4 = -2.0 * num;
			double num5 = 1.0 - num2;
			return new BiQuadFilter(arg_6F_0, num4, num5, b, (double)num3, b2);
		}

		public static BiQuadFilter BandPassFilterConstantPeakGain(float sampleRate, float centreFrequency, float q)
		{
			double expr_0F = 6.2831853071795862 * (double)centreFrequency / (double)sampleRate;
			double num = Math.Cos(expr_0F);
			double num2 = Math.Sin(expr_0F) / (double)(2f * q);
			double b = num2;
			int num3 = 0;
			double b2 = -num2;
			double arg_5B_0 = 1.0 + num2;
			double num4 = -2.0 * num;
			double num5 = 1.0 - num2;
			return new BiQuadFilter(arg_5B_0, num4, num5, b, (double)num3, b2);
		}

		public static BiQuadFilter NotchFilter(float sampleRate, float centreFrequency, float q)
		{
			double expr_0F = 6.2831853071795862 * (double)centreFrequency / (double)sampleRate;
			double num = Math.Cos(expr_0F);
			double num2 = Math.Sin(expr_0F) / (double)(2f * q);
			int num3 = 1;
			double b = -2.0 * num;
			int num4 = 1;
			double arg_65_0 = 1.0 + num2;
			double num5 = -2.0 * num;
			double num6 = 1.0 - num2;
			return new BiQuadFilter(arg_65_0, num5, num6, (double)num3, b, (double)num4);
		}

		public static BiQuadFilter AllPassFilter(float sampleRate, float centreFrequency, float q)
		{
			double expr_0F = 6.2831853071795862 * (double)centreFrequency / (double)sampleRate;
			double num = Math.Cos(expr_0F);
			double num2 = Math.Sin(expr_0F) / (double)(2f * q);
			double b = 1.0 - num2;
			double b2 = -2.0 * num;
			double b3 = 1.0 + num2;
			double arg_77_0 = 1.0 + num2;
			double num3 = -2.0 * num;
			double num4 = 1.0 - num2;
			return new BiQuadFilter(arg_77_0, num3, num4, b, b2, b3);
		}

		public static BiQuadFilter PeakingEQ(float sampleRate, float centreFrequency, float q, float dbGain)
		{
			BiQuadFilter expr_05 = new BiQuadFilter();
			expr_05.SetPeakingEq(sampleRate, centreFrequency, q, dbGain);
			return expr_05;
		}

		public static BiQuadFilter LowShelf(float sampleRate, float cutoffFrequency, float shelfSlope, float dbGain)
		{
			double expr_0F = 6.2831853071795862 * (double)cutoffFrequency / (double)sampleRate;
			double num = Math.Cos(expr_0F);
			double arg_3B_0 = Math.Sin(expr_0F);
			double num2 = Math.Pow(10.0, (double)(dbGain / 40f));
			double num3 = arg_3B_0 / 2.0 * Math.Sqrt((num2 + 1.0 / num2) * (double)(1f / shelfSlope - 1f) + 2.0);
			double num4 = 2.0 * Math.Sqrt(num2) * num3;
			double b = num2 * (num2 + 1.0 - (num2 - 1.0) * num + num4);
			double b2 = 2.0 * num2 * (num2 - 1.0 - (num2 + 1.0) * num);
			double b3 = num2 * (num2 + 1.0 - (num2 - 1.0) * num - num4);
			double arg_148_0 = num2 + 1.0 + (num2 - 1.0) * num + num4;
			double num5 = -2.0 * (num2 - 1.0 + (num2 + 1.0) * num);
			double num6 = num2 + 1.0 + (num2 - 1.0) * num - num4;
			return new BiQuadFilter(arg_148_0, num5, num6, b, b2, b3);
		}

		public static BiQuadFilter HighShelf(float sampleRate, float cutoffFrequency, float shelfSlope, float dbGain)
		{
			double expr_0F = 6.2831853071795862 * (double)cutoffFrequency / (double)sampleRate;
			double num = Math.Cos(expr_0F);
			double arg_3B_0 = Math.Sin(expr_0F);
			double num2 = Math.Pow(10.0, (double)(dbGain / 40f));
			double num3 = arg_3B_0 / 2.0 * Math.Sqrt((num2 + 1.0 / num2) * (double)(1f / shelfSlope - 1f) + 2.0);
			double num4 = 2.0 * Math.Sqrt(num2) * num3;
			double b = num2 * (num2 + 1.0 + (num2 - 1.0) * num + num4);
			double b2 = -2.0 * num2 * (num2 - 1.0 + (num2 + 1.0) * num);
			double b3 = num2 * (num2 + 1.0 + (num2 - 1.0) * num - num4);
			double arg_148_0 = num2 + 1.0 - (num2 - 1.0) * num + num4;
			double num5 = 2.0 * (num2 - 1.0 - (num2 + 1.0) * num);
			double num6 = num2 + 1.0 - (num2 - 1.0) * num - num4;
			return new BiQuadFilter(arg_148_0, num5, num6, b, b2, b3);
		}

		private BiQuadFilter()
		{
			this.x1 = (this.x2 = 0f);
			this.y1 = (this.y2 = 0f);
		}

		private BiQuadFilter(double a0, double a1, double a2, double b0, double b1, double b2)
		{
			this.SetCoefficients(a0, a1, a2, b0, b1, b2);
			this.x1 = (this.x2 = 0f);
			this.y1 = (this.y2 = 0f);
		}
	}
}
