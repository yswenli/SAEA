using System;

namespace SAEA.Audio.Base.NAudio.Wave
{
	public interface ISampleNotifier
	{
		event EventHandler<SampleEventArgs> Sample;
	}
}
