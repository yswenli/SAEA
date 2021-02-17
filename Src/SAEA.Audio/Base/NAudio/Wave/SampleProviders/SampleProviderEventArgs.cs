using System;

namespace SAEA.Audio.Base.NAudio.Wave.SampleProviders
{
	public class SampleProviderEventArgs : EventArgs
	{
		public ISampleProvider SampleProvider
		{
			get;
			private set;
		}

		public SampleProviderEventArgs(ISampleProvider sampleProvider)
		{
			this.SampleProvider = sampleProvider;
		}
	}
}
