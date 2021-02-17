using System;

namespace SAEA.Audio.Base.NAudio.Dsp
{
	internal class EnvelopeDetector
	{
		private double sampleRate;

		private double ms;

		private double coeff;

		public double TimeConstant
		{
			get
			{
				return this.ms;
			}
			set
			{
				this.ms = value;
				this.SetCoef();
			}
		}

		public double SampleRate
		{
			get
			{
				return this.sampleRate;
			}
			set
			{
				this.sampleRate = value;
				this.SetCoef();
			}
		}

		public EnvelopeDetector() : this(1.0, 44100.0)
		{
		}

		public EnvelopeDetector(double ms, double sampleRate)
		{
			this.sampleRate = sampleRate;
			this.ms = ms;
			this.SetCoef();
		}

		public void Run(double inValue, ref double state)
		{
			state = inValue + this.coeff * (state - inValue);
		}

		private void SetCoef()
		{
			this.coeff = Math.Exp(-1.0 / (0.001 * this.ms * this.sampleRate));
		}
	}
}
