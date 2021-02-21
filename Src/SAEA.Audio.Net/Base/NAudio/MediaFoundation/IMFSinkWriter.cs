using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.MediaFoundation
{
	[Guid("3137f1cd-fe5e-4805-a5d8-fb477448cb3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[ComImport]
	public interface IMFSinkWriter
	{
		void AddStream([MarshalAs(UnmanagedType.Interface)] [In] IMFMediaType pTargetMediaType, out int pdwStreamIndex);

		void SetInputMediaType([In] int dwStreamIndex, [MarshalAs(UnmanagedType.Interface)] [In] IMFMediaType pInputMediaType, [MarshalAs(UnmanagedType.Interface)] [In] IMFAttributes pEncodingParameters);

		void BeginWriting();

		void WriteSample([In] int dwStreamIndex, [MarshalAs(UnmanagedType.Interface)] [In] IMFSample pSample);

		void SendStreamTick([In] int dwStreamIndex, [In] long llTimestamp);

		void PlaceMarker([In] int dwStreamIndex, [In] IntPtr pvContext);

		void NotifyEndOfSegment([In] int dwStreamIndex);

		void Flush([In] int dwStreamIndex);

		void DoFinalize();

		void GetServiceForStream([In] int dwStreamIndex, [In] ref Guid guidService, [In] ref Guid riid, out IntPtr ppvObject);

		void GetStatistics([In] int dwStreamIndex, [In] [Out] MF_SINK_WRITER_STATISTICS pStats);
	}
}
