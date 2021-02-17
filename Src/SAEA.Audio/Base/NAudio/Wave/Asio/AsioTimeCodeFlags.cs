using System;

namespace SAEA.Audio.Base.NAudio.Wave.Asio
{
	[Flags]
	internal enum AsioTimeCodeFlags
	{
		kTcValid = 1,
		kTcRunning = 2,
		kTcReverse = 4,
		kTcOnspeed = 8,
		kTcStill = 16,
		kTcSpeedValid = 256
	}
}
