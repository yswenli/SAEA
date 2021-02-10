using System;
using System.Runtime.CompilerServices;

namespace SAEA.Audio.NAudio.Wave.SampleProviders
{
	public class MeteringSampleProvider : ISampleProvider
	{
		private readonly ISampleProvider source;

		private readonly float[] maxSamples;

		private int sampleCount;

		private readonly int channels;

		private readonly StreamVolumeEventArgs args;

		[method: CompilerGenerated]
		[CompilerGenerated]
		public event EventHandler<StreamVolumeEventArgs> StreamVolume;

		public int SamplesPerNotification
		{
			get;
			set;
		}

		public WaveFormat WaveFormat
		{
			get
			{
				return this.source.WaveFormat;
			}
		}

		public MeteringSampleProvider(ISampleProvider source) : this(source, source.WaveFormat.SampleRate / 10)
		{
		}

		public MeteringSampleProvider(ISampleProvider source, int samplesPerNotification)
		{
			this.source = source;
			this.channels = source.WaveFormat.Channels;
			this.maxSamples = new float[this.channels];
			this.SamplesPerNotification = samplesPerNotification;
			this.args = new StreamVolumeEventArgs
			{
				MaxSampleValues = this.maxSamples
			};
		}

		public int Read(float[] buffer, int offset, int count)
		{
			int num = this.source.Read(buffer, offset, count);
			if (this.StreamVolume != null)
			{
				for (int i = 0; i < num; i += this.channels)
				{
					for (int j = 0; j < this.channels; j++)
					{
						float val = Math.Abs(buffer[offset + i + j]);
						this.maxSamples[j] = Math.Max(this.maxSamples[j], val);
					}
					this.sampleCount++;
					if (this.sampleCount >= this.SamplesPerNotification)
					{
						this.StreamVolume(this, this.args);
						this.sampleCount = 0;
						Array.Clear(this.maxSamples, 0, this.channels);
					}
				}
			}
			return num;
		}
	}
}
