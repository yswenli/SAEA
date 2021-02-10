using System;

namespace SAEA.Audio.NAudio.Midi
{
	public enum MetaEventType : byte
	{
		TrackSequenceNumber,
		TextEvent,
		Copyright,
		SequenceTrackName,
		TrackInstrumentName,
		Lyric,
		Marker,
		CuePoint,
		ProgramName,
		DeviceName,
		MidiChannel = 32,
		MidiPort,
		EndTrack = 47,
		SetTempo = 81,
		SmpteOffset = 84,
		TimeSignature = 88,
		KeySignature,
		SequencerSpecific = 127
	}
}
