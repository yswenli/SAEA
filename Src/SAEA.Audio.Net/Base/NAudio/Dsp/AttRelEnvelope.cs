using System;

namespace SAEA.Audio.NAudio.Dsp
{
	internal class AttRelEnvelope
	{
		protected const double DC_OFFSET = 1E-25;

		private readonly EnvelopeDetector attack;

		private readonly EnvelopeDetector release;

		public double Attack
		{
			get
			{
				return this.attack.TimeConstant;
			}
			set
			{
				this.attack.TimeConstant = value;
			}
		}

		public double Release
		{
			get
			{
				return this.release.TimeConstant;
			}
			set
			{
				this.release.TimeConstant = value;
			}
		}

		public double SampleRate
		{
			get
			{
				return this.attack.SampleRate;
			}
			set
			{
				EnvelopeDetector arg_15_0 = this.attack;
				this.release.SampleRate = value;
				arg_15_0.SampleRate = value;
			}
		}

		public AttRelEnvelope(double attackMilliseconds, double releaseMilliseconds, double sampleRate)
		{
			this.attack = new EnvelopeDetector(attackMilliseconds, sampleRate);
			this.release = new EnvelopeDetector(releaseMilliseconds, sampleRate);
		}

		public void Run(double inValue, ref double state)
		{
			if (inValue > state)
			{
				this.attack.Run(inValue, ref state);
				return;
			}
			this.release.Run(inValue, ref state);
		}
	}
}
