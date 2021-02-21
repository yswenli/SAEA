using System;

namespace SAEA.Audio.Base.NAudio.Mixer
{
	[Flags]
	internal enum MixerControlSubclass
	{
		SwitchBoolean = 0,
		SwitchButton = 16777216,
		MeterPolled = 0,
		TimeMicrosecs = 0,
		TimeMillisecs = 16777216,
		ListSingle = 0,
		ListMultiple = 16777216,
		Mask = 251658240
	}
}
