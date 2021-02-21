using System;

namespace SAEA.Audio.Base.NAudio.Wave.SampleProviders
{
	public class OffsetSampleProvider : ISampleProvider
	{
		private readonly ISampleProvider sourceProvider;

		private int phase;

		private int phasePos;

		private int delayBySamples;

		private int skipOverSamples;

		private int takeSamples;

		private int leadOutSamples;

		public int DelayBySamples
		{
			get
			{
				return this.delayBySamples;
			}
			set
			{
				if (this.phase != 0)
				{
					throw new InvalidOperationException("Can't set DelayBySamples after calling Read");
				}
				if (value % this.WaveFormat.Channels != 0)
				{
					throw new ArgumentException("DelayBySamples must be a multiple of WaveFormat.Channels");
				}
				this.delayBySamples = value;
			}
		}

		public TimeSpan DelayBy
		{
			get
			{
				return this.SamplesToTimeSpan(this.delayBySamples);
			}
			set
			{
				this.delayBySamples = this.TimeSpanToSamples(value);
			}
		}

		public int SkipOverSamples
		{
			get
			{
				return this.skipOverSamples;
			}
			set
			{
				if (this.phase != 0)
				{
					throw new InvalidOperationException("Can't set SkipOverSamples after calling Read");
				}
				if (value % this.WaveFormat.Channels != 0)
				{
					throw new ArgumentException("SkipOverSamples must be a multiple of WaveFormat.Channels");
				}
				this.skipOverSamples = value;
			}
		}

		public TimeSpan SkipOver
		{
			get
			{
				return this.SamplesToTimeSpan(this.skipOverSamples);
			}
			set
			{
				this.skipOverSamples = this.TimeSpanToSamples(value);
			}
		}

		public int TakeSamples
		{
			get
			{
				return this.takeSamples;
			}
			set
			{
				if (this.phase != 0)
				{
					throw new InvalidOperationException("Can't set TakeSamples after calling Read");
				}
				if (value % this.WaveFormat.Channels != 0)
				{
					throw new ArgumentException("TakeSamples must be a multiple of WaveFormat.Channels");
				}
				this.takeSamples = value;
			}
		}

		public TimeSpan Take
		{
			get
			{
				return this.SamplesToTimeSpan(this.takeSamples);
			}
			set
			{
				this.takeSamples = this.TimeSpanToSamples(value);
			}
		}

		public int LeadOutSamples
		{
			get
			{
				return this.leadOutSamples;
			}
			set
			{
				if (this.phase != 0)
				{
					throw new InvalidOperationException("Can't set LeadOutSamples after calling Read");
				}
				if (value % this.WaveFormat.Channels != 0)
				{
					throw new ArgumentException("LeadOutSamples must be a multiple of WaveFormat.Channels");
				}
				this.leadOutSamples = value;
			}
		}

		public TimeSpan LeadOut
		{
			get
			{
				return this.SamplesToTimeSpan(this.leadOutSamples);
			}
			set
			{
				this.leadOutSamples = this.TimeSpanToSamples(value);
			}
		}

		public WaveFormat WaveFormat
		{
			get
			{
				return this.sourceProvider.WaveFormat;
			}
		}

		private int TimeSpanToSamples(TimeSpan time)
		{
			return (int)(time.TotalSeconds * (double)this.WaveFormat.SampleRate) * this.WaveFormat.Channels;
		}

		private TimeSpan SamplesToTimeSpan(int samples)
		{
			return TimeSpan.FromSeconds((double)(samples / this.WaveFormat.Channels) / (double)this.WaveFormat.SampleRate);
		}

		public OffsetSampleProvider(ISampleProvider sourceProvider)
		{
			this.sourceProvider = sourceProvider;
		}

		public int Read(float[] buffer, int offset, int count)
		{
			int num = 0;
			if (this.phase == 0)
			{
				this.phase++;
			}
			if (this.phase == 1)
			{
				int num2 = Math.Min(count, this.DelayBySamples - this.phasePos);
				for (int i = 0; i < num2; i++)
				{
					buffer[offset + i] = 0f;
				}
				this.phasePos += num2;
				num += num2;
				if (this.phasePos >= this.DelayBySamples)
				{
					this.phase++;
					this.phasePos = 0;
				}
			}
			if (this.phase == 2)
			{
				if (this.SkipOverSamples > 0)
				{
					float[] array = new float[this.WaveFormat.SampleRate * this.WaveFormat.Channels];
					int num3;
					for (int j = 0; j < this.SkipOverSamples; j += num3)
					{
						int count2 = Math.Min(this.SkipOverSamples - j, array.Length);
						num3 = this.sourceProvider.Read(array, 0, count2);
						if (num3 == 0)
						{
							break;
						}
					}
				}
				this.phase++;
				this.phasePos = 0;
			}
			if (this.phase == 3)
			{
				int num4 = count - num;
				if (this.takeSamples != 0)
				{
					num4 = Math.Min(num4, this.takeSamples - this.phasePos);
				}
				int num5 = this.sourceProvider.Read(buffer, offset + num, num4);
				this.phasePos += num5;
				num += num5;
				if (num5 < num4 || (this.takeSamples > 0 && this.phasePos >= this.takeSamples))
				{
					this.phase++;
					this.phasePos = 0;
				}
			}
			if (this.phase == 4)
			{
				int num6 = Math.Min(count - num, this.LeadOutSamples - this.phasePos);
				for (int k = 0; k < num6; k++)
				{
					buffer[offset + num + k] = 0f;
				}
				this.phasePos += num6;
				num += num6;
				if (this.phasePos >= this.LeadOutSamples)
				{
					this.phase++;
					this.phasePos = 0;
				}
			}
			return num;
		}
	}
}
