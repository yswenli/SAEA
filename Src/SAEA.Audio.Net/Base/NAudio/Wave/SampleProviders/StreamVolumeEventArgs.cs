using System;

namespace SAEA.Audio.NAudio.Wave.SampleProviders
{
	public class StreamVolumeEventArgs : EventArgs
	{
		public float[] MaxSampleValues
		{
			get;
			set;
		}
	}
}
