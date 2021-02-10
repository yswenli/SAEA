using System;

namespace SAEA.Audio.NAudio.Wave
{
	public enum WaveCallbackStrategy
	{
		FunctionCallback,
		NewWindow,
		ExistingWindow,
		Event
	}
}
