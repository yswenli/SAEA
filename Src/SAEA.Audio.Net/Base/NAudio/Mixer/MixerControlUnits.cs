using System;

namespace SAEA.Audio.NAudio.Mixer
{
	[Flags]
	internal enum MixerControlUnits
	{
		Custom = 0,
		Boolean = 65536,
		Signed = 131072,
		Unsigned = 196608,
		Decibels = 262144,
		Percent = 327680,
		Mask = 16711680
	}
}
