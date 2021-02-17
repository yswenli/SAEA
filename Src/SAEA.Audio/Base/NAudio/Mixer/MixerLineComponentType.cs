using System;

namespace SAEA.Audio.Base.NAudio.Mixer
{
	public enum MixerLineComponentType
	{
		DestinationUndefined,
		DestinationDigital,
		DestinationLine,
		DestinationMonitor,
		DestinationSpeakers,
		DestinationHeadphones,
		DestinationTelephone,
		DestinationWaveIn,
		DestinationVoiceIn,
		SourceUndefined = 4096,
		SourceDigital,
		SourceLine,
		SourceMicrophone,
		SourceSynthesizer,
		SourceCompactDisc,
		SourceTelephone,
		SourcePcSpeaker,
		SourceWaveOut,
		SourceAuxiliary,
		SourceAnalog
	}
}
