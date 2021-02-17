using System;

namespace SAEA.Audio.Base.NAudio.Wave
{
	public enum WaveCallbackStrategy
	{
		FunctionCallback,
		NewWindow,
		ExistingWindow,
		Event
	}
}
