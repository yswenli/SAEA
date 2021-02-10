using System;

namespace SAEA.Audio.NAudio.CoreAudioApi
{
	[Flags]
	public enum DeviceState
	{
		Active = 1,
		Disabled = 2,
		NotPresent = 4,
		Unplugged = 8,
		All = 15
	}
}
