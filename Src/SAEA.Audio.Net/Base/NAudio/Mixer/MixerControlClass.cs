using System;

namespace SAEA.Audio.NAudio.Mixer
{
	[Flags]
	internal enum MixerControlClass
	{
		Custom = 0,
		Meter = 268435456,
		Switch = 536870912,
		Number = 805306368,
		Slider = 1073741824,
		Fader = 1342177280,
		Time = 1610612736,
		List = 1879048192,
		Mask = 1879048192
	}
}
