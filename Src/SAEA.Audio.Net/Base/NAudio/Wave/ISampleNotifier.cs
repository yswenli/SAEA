using System;

namespace SAEA.Audio.NAudio.Wave
{
	public interface ISampleNotifier
	{
		event EventHandler<SampleEventArgs> Sample;
	}
}
