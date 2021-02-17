using System;

namespace SAEA.Audio.Base.NAudio.Wave.Asio
{
	[Flags]
	internal enum AsioTimeInfoFlags
	{
		kSystemTimeValid = 1,
		kSamplePositionValid = 2,
		kSampleRateValid = 4,
		kSpeedValid = 8,
		kSampleRateChanged = 16,
		kClockSourceChanged = 32
	}
}
