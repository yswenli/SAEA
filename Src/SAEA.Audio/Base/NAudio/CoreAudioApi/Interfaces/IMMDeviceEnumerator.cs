using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.CoreAudioApi.Interfaces
{
	[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IMMDeviceEnumerator
	{
		int EnumAudioEndpoints(DataFlow dataFlow, DeviceState stateMask, out IMMDeviceCollection devices);

		[PreserveSig]
		int GetDefaultAudioEndpoint(DataFlow dataFlow, Role role, out IMMDevice endpoint);

		int GetDevice(string id, out IMMDevice deviceName);

		int RegisterEndpointNotificationCallback(IMMNotificationClient client);

		int UnregisterEndpointNotificationCallback(IMMNotificationClient client);
	}
}
