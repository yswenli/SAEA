using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.CoreAudioApi.Interfaces
{
	[Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IAudioSessionControl2 : IAudioSessionControl
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

		[PreserveSig]
		int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string retVal);

		[PreserveSig]
		int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string retVal);

		[PreserveSig]
		int GetProcessId(out uint retVal);

		[PreserveSig]
		int IsSystemSoundsSession();

		[PreserveSig]
		int SetDuckingPreference(bool optOut);
	}
}
