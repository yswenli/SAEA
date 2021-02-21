using System;

namespace SAEA.Audio.Base.NAudio.Midi
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
