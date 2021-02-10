using System;

namespace SAEA.Audio.NAudio.Dmo
{
	[Flags]
	public enum DmoProcessOutputFlags
	{
		None = 0,
		DiscardWhenNoBuffer = 1
	}
}
