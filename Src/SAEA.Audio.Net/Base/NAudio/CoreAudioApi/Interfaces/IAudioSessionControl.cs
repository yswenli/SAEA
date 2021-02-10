using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.CoreAudioApi.Interfaces
{
	[Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IAudioSessionControl
	{
		[PreserveSig]
		int GetState(out AudioSessionState state);

		[PreserveSig]
		int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string displayName);

		[PreserveSig]
		int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] [In] string displayName, [MarshalAs(UnmanagedType.LPStruct)] [In] Guid eventContext);

		[PreserveSig]
		int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string iconPath);

		[PreserveSig]
		int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] [In] string iconPath, [MarshalAs(UnmanagedType.LPStruct)] [In] Guid eventContext);

		[PreserveSig]
		int GetGroupingParam(out Guid groupingId);

		[PreserveSig]
		int SetGroupingParam([MarshalAs(UnmanagedType.LPStruct)] [In] Guid groupingId, [MarshalAs(UnmanagedType.LPStruct)] [In] Guid eventContext);

		[PreserveSig]
		int RegisterAudioSessionNotification([In] IAudioSessionEvents client);

		[PreserveSig]
		int UnregisterAudioSessionNotification([In] IAudioSessionEvents client);
	}
}
