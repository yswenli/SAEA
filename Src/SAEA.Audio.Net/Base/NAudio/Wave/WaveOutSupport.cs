using System;

namespace SAEA.Audio.Base.NAudio.Wave
{
	[Flags]
	internal enum WaveOutSupport
	{
		Pitch = 1,
		PlaybackRate = 2,
		Volume = 4,
		LRVolume = 8,
		Sync = 16,
		SampleAccurate = 32
	}
}
