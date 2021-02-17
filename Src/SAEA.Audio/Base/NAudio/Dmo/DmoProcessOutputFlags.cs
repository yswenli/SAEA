using System;

namespace SAEA.Audio.Base.NAudio.Dmo
{
	[Flags]
	public enum DmoProcessOutputFlags
	{
		None = 0,
		DiscardWhenNoBuffer = 1
	}
}
