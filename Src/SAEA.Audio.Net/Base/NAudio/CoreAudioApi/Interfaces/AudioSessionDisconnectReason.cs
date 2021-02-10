using System;

namespace SAEA.Audio.NAudio.CoreAudioApi.Interfaces
{
	public enum AudioSessionDisconnectReason
	{
		DisconnectReasonDeviceRemoval,
		DisconnectReasonServerShutdown,
		DisconnectReasonFormatChanged,
		DisconnectReasonSessionLogoff,
		DisconnectReasonSessionDisconnected,
		DisconnectReasonExclusiveModeOverride
	}
}
