using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.CoreAudioApi.Interfaces
{
	[Guid("6f49ff73-6727-49AC-A008-D98CF5E70048"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IAudioClock2 : IAudioClock
	{
		[PreserveSig]
		int GetDevicePosition(out ulong devicePosition, out ulong qpcPosition);
	}
}
