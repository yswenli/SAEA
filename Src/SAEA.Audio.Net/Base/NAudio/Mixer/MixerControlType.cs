using System;

namespace SAEA.Audio.NAudio.Mixer
{
	public enum MixerControlType
	{
		Custom,
		BooleanMeter = 268500992,
		SignedMeter = 268566528,
		PeakMeter,
		UnsignedMeter = 268632064,
		Boolean = 536936448,
		OnOff,
		Mute,
		Mono,
		Loudness,
		StereoEnhance,
		Button = 553713664,
		Decibels = 805568512,
		Signed = 805437440,
		Unsigned = 805502976,
		Percent = 805634048,
		Slider = 1073872896,
		Pan,
		QSoundPan,
		Fader = 1342373888,
		Volume,
		Bass,
		Treble,
		Equalizer,
		SingleSelect = 1879113728,
		Mux,
		MultipleSelect = 1895890944,
		Mixer,
		MicroTime = 1610809344,
		MilliTime = 1627586560
	}
}
