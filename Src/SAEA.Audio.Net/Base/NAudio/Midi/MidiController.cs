using System;

namespace SAEA.Audio.Base.NAudio.Midi
{
	public enum MidiController : byte
	{
		BankSelect,
		Modulation,
		BreathController,
		FootController = 4,
		MainVolume = 7,
		Pan = 10,
		Expression,
		BankSelectLsb = 32,
		Sustain = 64,
		Portamento,
		Sostenuto,
		SoftPedal,
		LegatoFootswitch,
		ResetAllControllers = 121,
		AllNotesOff = 123
	}
}
