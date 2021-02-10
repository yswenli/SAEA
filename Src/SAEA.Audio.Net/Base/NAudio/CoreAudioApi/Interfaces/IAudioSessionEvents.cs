using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.CoreAudioApi.Interfaces
{
	[Guid("24918ACC-64B3-37C1-8CA9-74A66E9957A8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IAudioSessionEvents
	{
		[PreserveSig]
		int OnDisplayNameChanged([MarshalAs(UnmanagedType.LPWStr)] [In] string displayName, [In] ref Guid eventContext);

		[PreserveSig]
		int OnIconPathChanged([MarshalAs(UnmanagedType.LPWStr)] [In] string iconPath, [In] ref Guid eventContext);

		[PreserveSig]
		int OnSimpleVolumeChanged([MarshalAs(UnmanagedType.R4)] [In] float volume, [MarshalAs(UnmanagedType.Bool)] [In] bool isMuted, [In] ref Guid eventContext);

		[PreserveSig]
		int OnChannelVolumeChanged([MarshalAs(UnmanagedType.U4)] [In] uint channelCount, [MarshalAs(UnmanagedType.SysInt)] [In] IntPtr newVolumes, [MarshalAs(UnmanagedType.U4)] [In] uint channelIndex, [In] ref Guid eventContext);

		[PreserveSig]
		int OnGroupingParamChanged([In] ref Guid groupingId, [In] ref Guid eventContext);

		[PreserveSig]
		int OnStateChanged([In] AudioSessionState state);

		[PreserveSig]
		int OnSessionDisconnected([In] AudioSessionDisconnectReason disconnectReason);
	}
}
