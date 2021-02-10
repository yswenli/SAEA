using System;

namespace SAEA.Audio.NAudio.Wave
{
	[Flags]
	internal enum AcmStreamConvertFlags
	{
		BlockAlign = 4,
		Start = 16,
		End = 32
	}
}
