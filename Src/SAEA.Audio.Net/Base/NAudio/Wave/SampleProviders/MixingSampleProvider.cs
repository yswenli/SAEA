using SAEA.Audio.NAudio.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SAEA.Audio.NAudio.Wave.SampleProviders
{
	public class MixingSampleProvider : ISampleProvider
	{
		private readonly List<ISampleProvider> sources;

		private float[] sourceBuffer;

		private const int MaxInputs = 1024;

		[method: CompilerGenerated]
		[CompilerGenerated]
		public event EventHandler<SampleProviderEventArgs> MixerInputEnded;

		public IEnumerable<ISampleProvider> MixerInputs
		{
			get
			{
				return this.sources;
			}
		}

		public bool ReadFully
		{
			get;
			set;
		}

		public WaveFormat WaveFormat
		{
			get;
			private set;
		}

		public MixingSampleProvider(WaveFormat waveFormat)
		{
			if (waveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
			{
				throw new ArgumentException("Mixer wave format must be IEEE float");
			}
			this.sources = new List<ISampleProvider>();
			this.WaveFormat = waveFormat;
		}

		public MixingSampleProvider(IEnumerable<ISampleProvider> sources)
		{
			this.sources = new List<ISampleProvider>();
			foreach (ISampleProvider current in sources)
			{
				this.AddMixerInput(current);
			}
			if (this.sources.Count == 0)
			{
				throw new ArgumentException("Must provide at least one input in this constructor");
			}
		}

		public void AddMixerInput(IWaveProvider mixerInput)
		{
			this.AddMixerInput(SampleProviderConverters.ConvertWaveProviderIntoSampleProvider(mixerInput));
		}

		public void AddMixerInput(ISampleProvider mixerInput)
		{
			List<ISampleProvider> obj = this.sources;
			lock (obj)
			{
				if (this.sources.Count >= 1024)
				{
					throw new InvalidOperationException("Too many mixer inputs");
				}
				this.sources.Add(mixerInput);
			}
			if (this.WaveFormat == null)
			{
				this.WaveFormat = mixerInput.WaveFormat;
				return;
			}
			if (this.WaveFormat.SampleRate != mixerInput.WaveFormat.SampleRate || this.WaveFormat.Channels != mixerInput.WaveFormat.Channels)
			{
				throw new ArgumentException("All mixer inputs must have the same WaveFormat");
			}
		}

		public void RemoveMixerInput(ISampleProvider mixerInput)
		{
			List<ISampleProvider> obj = this.sources;
			lock (obj)
			{
				this.sources.Remove(mixerInput);
			}
		}

		public void RemoveAllMixerInputs()
		{
			List<ISampleProvider> obj = this.sources;
			lock (obj)
			{
				this.sources.Clear();
			}
		}

		public int Read(float[] buffer, int offset, int count)
		{
			int num = 0;
			this.sourceBuffer = BufferHelpers.Ensure(this.sourceBuffer, count);
			List<ISampleProvider> obj = this.sources;
			lock (obj)
			{
				for (int i = this.sources.Count - 1; i >= 0; i--)
				{
					ISampleProvider sampleProvider = this.sources[i];
					int num2 = sampleProvider.Read(this.sourceBuffer, 0, count);
					int num3 = offset;
					for (int j = 0; j < num2; j++)
					{
						if (j >= num)
						{
							buffer[num3++] = this.sourceBuffer[j];
						}
						else
						{
							buffer[num3++] += this.sourceBuffer[j];
						}
					}
					num = Math.Max(num2, num);
					if (num2 < count)
					{
						EventHandler<SampleProviderEventArgs> expr_AC = this.MixerInputEnded;
						if (expr_AC != null)
						{
							expr_AC(this, new SampleProviderEventArgs(sampleProvider));
						}
						this.sources.RemoveAt(i);
					}
				}
			}
			if (this.ReadFully && num < count)
			{
				int k = offset + num;
				while (k < offset + count)
				{
					buffer[k++] = 0f;
				}
				num = count;
			}
			return num;
		}
	}
}
