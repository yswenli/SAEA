using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.CoreAudioApi.Interfaces
{
	[Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IMMNotificationClient
	{
		void OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, [MarshalAs(UnmanagedType.I4)] DeviceState newState);

		void OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);

		void OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string deviceId);

		void OnDefaultDeviceChanged(DataFlow flow, Role role, [MarshalAs(UnmanagedType.LPWStr)] string defaultDeviceId);

		void OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, PropertyKey key);
	}
}
