using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.MediaFoundation
{
	[Guid("70ae66f2-c809-4e4f-8915-bdcb406b7993"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[ComImport]
	public interface IMFSourceReader
	{
		void GetStreamSelection([In] int dwStreamIndex, [MarshalAs(UnmanagedType.Bool)] out bool pSelected);

		void SetStreamSelection([In] int dwStreamIndex, [MarshalAs(UnmanagedType.Bool)] [In] bool pSelected);

		void GetNativeMediaType([In] int dwStreamIndex, [In] int dwMediaTypeIndex, out IMFMediaType ppMediaType);

		void GetCurrentMediaType([In] int dwStreamIndex, out IMFMediaType ppMediaType);

		void SetCurrentMediaType([In] int dwStreamIndex, IntPtr pdwReserved, [In] IMFMediaType pMediaType);

		void SetCurrentPosition([MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidTimeFormat, [In] IntPtr varPosition);

		void ReadSample([In] int dwStreamIndex, [In] int dwControlFlags, out int pdwActualStreamIndex, out MF_SOURCE_READER_FLAG pdwStreamFlags, out ulong pllTimestamp, out IMFSample ppSample);

		void Flush([In] int dwStreamIndex);

		void GetServiceForStream([In] int dwStreamIndex, [MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidService, [MarshalAs(UnmanagedType.LPStruct)] [In] Guid riid, out IntPtr ppvObject);

		[PreserveSig]
		int GetPresentationAttribute([In] int dwStreamIndex, [MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidAttribute, [Out] IntPtr pvarAttribute);
	}
}
