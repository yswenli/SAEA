using System;

namespace SAEA.Audio.NAudio.Wave
{
	[Flags]
	public enum WaveHeaderFlags
	{
		BeginLoop = 4,
		Done = 1,
		EndLoop = 8,
		InQueue = 16,
		Prepared = 2
	}
}
