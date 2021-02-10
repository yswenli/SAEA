using System;

namespace SAEA.Audio.NAudio.Dmo
{
	[Flags]
	public enum DmoInputDataBufferFlags
	{
		None = 0,
		SyncPoint = 1,
		Time = 2,
		TimeLength = 4
	}
}
