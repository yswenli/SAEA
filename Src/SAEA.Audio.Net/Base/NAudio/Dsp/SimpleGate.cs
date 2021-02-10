using SAEA.Audio.NAudio.Utils;
using System;

namespace SAEA.Audio.NAudio.Dsp
{
	internal class SimpleGate : AttRelEnvelope
	{
		private double threshdB;

		private double thresh;

		private double env;

		public double Threshold
		{
			get
			{
				return this.threshdB;
			}
			set
			{
				this.threshdB = value;
				this.thresh = Decibels.DecibelsToLinear(value);
			}
		}

		public SimpleGate() : base(10.0, 10.0, 44100.0)
		{
			this.threshdB = 0.0;
			this.thresh = 1.0;
			this.env = 1E-25;
		}

		public void Process(ref double in1, ref double in2)
		{
			double arg_10_0 = Math.Abs(in1);
			double val = Math.Abs(in2);
			double num = (Math.Max(arg_10_0, val) > this.thresh) ? 1.0 : 0.0;
			num += 1E-25;
			base.Run(num, ref this.env);
			num = this.env - 1E-25;
			in1 *= num;
			in2 *= num;
		}
	}
}
