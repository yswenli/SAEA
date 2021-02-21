using System;

namespace SAEA.Audio.Base.NAudio.CoreAudioApi
{
	[Flags]
	public enum AudioClientStreamFlags
	{
		None = 0,
		CrossProcess = 65536,
		Loopback = 131072,
		EventCallback = 262144,
		NoPersist = 524288
	}
}
