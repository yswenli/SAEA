using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.CoreAudioApi.Interfaces
{
	[Guid("93014887-242D-4068-8A15-CF5E93B90FE3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IAudioStreamVolume
	{
		[PreserveSig]
		int GetChannelCount(out uint dwCount);

		[PreserveSig]
		int SetChannelVolume([In] uint dwIndex, [In] float fLevel);

		[PreserveSig]
		int GetChannelVolume([In] uint dwIndex, out float fLevel);

		[PreserveSig]
		int SetAllVoumes([In] uint dwCount, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.R4, SizeParamIndex = 0)] [In] float[] fVolumes);

		[PreserveSig]
		int GetAllVolumes([In] uint dwCount, [MarshalAs(UnmanagedType.LPArray)] float[] pfVolumes);
	}
}
