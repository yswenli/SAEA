using System;

namespace SAEA.Audio.NAudio.CoreAudioApi
{
	public enum AudioStreamCategory
	{
		Other,
		ForegroundOnlyMedia,
		BackgroundCapableMedia,
		Communications,
		Alerts,
		SoundEffects,
		GameEffects,
		GameMedia
	}
}
