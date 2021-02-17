using System;

namespace SAEA.Audio.Base.NAudio.Wave.SampleProviders
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
