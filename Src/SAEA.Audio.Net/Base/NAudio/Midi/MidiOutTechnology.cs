using System;

namespace SAEA.Audio.NAudio.Midi
{
	public enum MidiOutTechnology
	{
		MidiPort = 1,
		Synth,
		SquareWaveSynth,
		FMSynth,
		MidiMapper,
		WaveTableSynth,
		SoftwareSynth
	}
}
