using System;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.Audio.NAudio.Wave.SampleProviders
{
	public class ConcatenatingSampleProvider : ISampleProvider
	{
		private readonly ISampleProvider[] providers;

		private int currentProviderIndex;

		public WaveFormat WaveFormat
		{
			get
			{
				return this.providers[0].WaveFormat;
			}
		}

		public ConcatenatingSampleProvider(IEnumerable<ISampleProvider> providers)
		{
			if (providers == null)
			{
				throw new ArgumentNullException("providers");
			}
			this.providers = providers.ToArray<ISampleProvider>();
			if (this.providers.Length == 0)
			{
				throw new ArgumentException("Must provide at least one input", "providers");
			}
			if (this.providers.Any((ISampleProvider p) => p.WaveFormat.Channels != this.WaveFormat.Channels))
			{
				throw new ArgumentException("All inputs must have the same channel count", "providers");
			}
			if (this.providers.Any((ISampleProvider p) => p.WaveFormat.SampleRate != this.WaveFormat.SampleRate))
			{
				throw new ArgumentException("All inputs must have the same sample rate", "providers");
			}
		}

		public int Read(float[] buffer, int offset, int count)
		{
			int num = 0;
			while (num < count && this.currentProviderIndex < this.providers.Length)
			{
				int count2 = count - num;
				int num2 = this.providers[this.currentProviderIndex].Read(buffer, num, count2);
				num += num2;
				if (num2 == 0)
				{
					this.currentProviderIndex++;
				}
			}
			return num;
		}
	}
}
