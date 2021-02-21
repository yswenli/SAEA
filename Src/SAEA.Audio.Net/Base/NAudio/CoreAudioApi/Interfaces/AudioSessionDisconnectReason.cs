using System;

namespace SAEA.Audio.Base.NAudio.CoreAudioApi.Interfaces
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
