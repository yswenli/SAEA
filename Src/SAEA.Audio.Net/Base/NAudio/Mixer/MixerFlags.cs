using System;

namespace SAEA.Audio.NAudio.Mixer
{
	[Flags]
	public enum MixerFlags
	{
		Handle = -2147483648,
		Mixer = 0,
		MixerHandle = -2147483648,
		WaveOut = 268435456,
		WaveOutHandle = -1879048192,
		WaveIn = 536870912,
		WaveInHandle = -1610612736,
		MidiOut = 805306368,
		MidiOutHandle = -1342177280,
		MidiIn = 1073741824,
		MidiInHandle = -1073741824,
		Aux = 1342177280,
		Value = 0,
		ListText = 1,
		QueryMask = 15,
		All = 0,
		OneById = 1,
		OneByType = 2,
		GetLineInfoOfDestination = 0,
		GetLineInfoOfSource = 1,
		GetLineInfoOfLineId = 2,
		GetLineInfoOfComponentType = 3,
		GetLineInfoOfTargetType = 4,
		GetLineInfoOfQueryMask = 15
	}
}
