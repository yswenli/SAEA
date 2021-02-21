using System;
using System.Runtime.CompilerServices;

namespace SAEA.Audio.Base.NAudio.Wave.SampleProviders
{
	public class SignalGenerator : ISampleProvider
	{
		private readonly WaveFormat waveFormat;

		private readonly Random random = new Random();

		private readonly double[] pinkNoiseBuffer = new double[7];

		private const double TwoPi = 6.2831853071795862;

		private int nSample;

		private double phi;

		public WaveFormat WaveFormat
		{
			get
			{
				return this.waveFormat;
			}
		}

		public double Frequency
		{
			get;
			set;
		}

		public double FrequencyLog
		{
			get
			{
				return Math.Log(this.Frequency);
			}
		}

		public double FrequencyEnd
		{
			get;
			set;
		}

		public double FrequencyEndLog
		{
			get
			{
				return Math.Log(this.FrequencyEnd);
			}
		}

		public double Gain
		{
			get;
			set;
		}

		public bool[] PhaseReverse
		{
            get;private set;
		}

		public SignalGeneratorType Type
		{
			get;
			set;
		}

		public double SweepLengthSecs
		{
			get;
			set;
		}

		public SignalGenerator() : this(44100, 2)
		{
		}

		public SignalGenerator(int sampleRate, int channel)
		{
			this.phi = 0.0;
			this.waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channel);
			this.Type = SignalGeneratorType.Sin;
			this.Frequency = 440.0;
			this.Gain = 1.0;
			this.PhaseReverse = new bool[channel];
			this.SweepLengthSecs = 2.0;
		}

		public int Read(float[] buffer, int offset, int count)
		{
			int num = offset;
			for (int i = 0; i < count / this.waveFormat.Channels; i++)
			{
				double num4;
				switch (this.Type)
				{
				case SignalGeneratorType.Pink:
				{
					double num2 = this.NextRandomTwo();
					this.pinkNoiseBuffer[0] = 0.99886 * this.pinkNoiseBuffer[0] + num2 * 0.0555179;
					this.pinkNoiseBuffer[1] = 0.99332 * this.pinkNoiseBuffer[1] + num2 * 0.0750759;
					this.pinkNoiseBuffer[2] = 0.969 * this.pinkNoiseBuffer[2] + num2 * 0.153852;
					this.pinkNoiseBuffer[3] = 0.8665 * this.pinkNoiseBuffer[3] + num2 * 0.3104856;
					this.pinkNoiseBuffer[4] = 0.55 * this.pinkNoiseBuffer[4] + num2 * 0.5329522;
					this.pinkNoiseBuffer[5] = -0.7616 * this.pinkNoiseBuffer[5] - num2 * 0.016898;
					double num3 = this.pinkNoiseBuffer[0] + this.pinkNoiseBuffer[1] + this.pinkNoiseBuffer[2] + this.pinkNoiseBuffer[3] + this.pinkNoiseBuffer[4] + this.pinkNoiseBuffer[5] + this.pinkNoiseBuffer[6] + num2 * 0.5362;
					this.pinkNoiseBuffer[6] = num2 * 0.115926;
					num4 = this.Gain * (num3 / 5.0);
					break;
				}
				case SignalGeneratorType.White:
					num4 = this.Gain * this.NextRandomTwo();
					break;
				case SignalGeneratorType.Sweep:
				{
					double num5 = Math.Exp(this.FrequencyLog + (double)this.nSample * (this.FrequencyEndLog - this.FrequencyLog) / (this.SweepLengthSecs * (double)this.waveFormat.SampleRate));
					double num6 = 6.2831853071795862 * num5 / (double)this.waveFormat.SampleRate;
					this.phi += num6;
					num4 = this.Gain * Math.Sin(this.phi);
					this.nSample++;
					if ((double)this.nSample > this.SweepLengthSecs * (double)this.waveFormat.SampleRate)
					{
						this.nSample = 0;
						this.phi = 0.0;
					}
					break;
				}
				case SignalGeneratorType.Sin:
				{
					double num6 = 6.2831853071795862 * this.Frequency / (double)this.waveFormat.SampleRate;
					num4 = this.Gain * Math.Sin((double)this.nSample * num6);
					this.nSample++;
					break;
				}
				case SignalGeneratorType.Square:
				{
					double num6 = 2.0 * this.Frequency / (double)this.waveFormat.SampleRate;
					double num7 = (double)this.nSample * num6 % 2.0 - 1.0;
					num4 = ((num7 > 0.0) ? this.Gain : (-this.Gain));
					this.nSample++;
					break;
				}
				case SignalGeneratorType.Triangle:
				{
					double num6 = 2.0 * this.Frequency / (double)this.waveFormat.SampleRate;
					double num7 = (double)this.nSample * num6 % 2.0;
					num4 = 2.0 * num7;
					if (num4 > 1.0)
					{
						num4 = 2.0 - num4;
					}
					if (num4 < -1.0)
					{
						num4 = -2.0 - num4;
					}
					num4 *= this.Gain;
					this.nSample++;
					break;
				}
				case SignalGeneratorType.SawTooth:
				{
					double num6 = 2.0 * this.Frequency / (double)this.waveFormat.SampleRate;
					double num7 = (double)this.nSample * num6 % 2.0 - 1.0;
					num4 = this.Gain * num7;
					this.nSample++;
					break;
				}
				default:
					num4 = 0.0;
					break;
				}
				for (int j = 0; j < this.waveFormat.Channels; j++)
				{
					if (this.PhaseReverse[j])
					{
						buffer[num++] = (float)(-(float)num4);
					}
					else
					{
						buffer[num++] = (float)num4;
					}
				}
			}
			return count;
		}

		private double NextRandomTwo()
		{
			return 2.0 * this.random.NextDouble() - 1.0;
		}
	}
}
