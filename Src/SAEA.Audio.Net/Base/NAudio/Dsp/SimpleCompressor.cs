using SAEA.Audio.NAudio.Utils;
using System;

namespace SAEA.Audio.NAudio.Dsp
{
    internal class SimpleCompressor : AttRelEnvelope
    {
        private double envdB;

        public double MakeUpGain
        {
            get;
            set;
        }

        public double Threshold
        {
            get;
            set;
        }

        public double Ratio
        {
            get;
            set;
        }

        public SimpleCompressor(double attackTime, double releaseTime, double sampleRate) : base(attackTime, releaseTime, sampleRate)
        {
            this.Threshold = 0.0;
            this.Ratio = 1.0;
            this.MakeUpGain = 0.0;
            this.envdB = 1E-25;
        }

        public SimpleCompressor() : base(10.0, 10.0, 44100.0)
        {
            this.Threshold = 0.0;
            this.Ratio = 1.0;
            this.MakeUpGain = 0.0;
            this.envdB = 1E-25;
        }

        public void InitRuntime()
        {
            this.envdB = 1E-25;
        }

        public void Process(ref double in1, ref double in2)
        {
            double arg_10_0 = Math.Abs(in1);
            double val = Math.Abs(in2);
            double num = Decibels.LinearToDecibels(Math.Max(arg_10_0, val) + 1E-25) - this.Threshold;
            if (num < 0.0)
            {
                num = 0.0;
            }
            num += 1E-25;
            base.Run(num, ref this.envdB);
            num = this.envdB - 1E-25;
            double num2 = num * (this.Ratio - 1.0);
            num2 = Decibels.DecibelsToLinear(num2) * Decibels.DecibelsToLinear(this.MakeUpGain);
            in1 *= num2;
            in2 *= num2;
        }
    }
}
