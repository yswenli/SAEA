using System;

namespace SAEA.Audio.Base.NAudio.Wave
{
	[Flags]
	internal enum AcmStreamConvertFlags
	{
		BlockAlign = 4,
		Start = 16,
		End = 32
	}
}
