using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.CoreAudioApi.Interfaces
{
	[Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface ISimpleAudioVolume
	{
		[PreserveSig]
		int SetMasterVolume([MarshalAs(UnmanagedType.R4)] [In] float levelNorm, [MarshalAs(UnmanagedType.LPStruct)] [In] Guid eventContext);

		[PreserveSig]
		int GetMasterVolume([MarshalAs(UnmanagedType.R4)] out float levelNorm);

		[PreserveSig]
		int SetMute([MarshalAs(UnmanagedType.Bool)] [In] bool isMuted, [MarshalAs(UnmanagedType.LPStruct)] [In] Guid eventContext);

		[PreserveSig]
		int GetMute([MarshalAs(UnmanagedType.Bool)] out bool isMuted);
	}
}
