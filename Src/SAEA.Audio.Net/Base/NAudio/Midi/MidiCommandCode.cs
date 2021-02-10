using System;

namespace SAEA.Audio.NAudio.Midi
{
	public enum MidiCommandCode : byte
	{
		NoteOff = 128,
		NoteOn = 144,
		KeyAfterTouch = 160,
		ControlChange = 176,
		PatchChange = 192,
		ChannelAfterTouch = 208,
		PitchWheelChange = 224,
		Sysex = 240,
		Eox = 247,
		TimingClock,
		StartSequence = 250,
		ContinueSequence,
		StopSequence,
		AutoSensing = 254,
		MetaEvent
	}
}
