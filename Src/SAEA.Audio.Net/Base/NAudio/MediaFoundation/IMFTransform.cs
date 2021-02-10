using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.MediaFoundation
{
	[Guid("bf94c121-5b05-4e6f-8000-ba598961414d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[ComImport]
	public interface IMFTransform
	{
		void GetStreamLimits(out int pdwInputMinimum, out int pdwInputMaximum, out int pdwOutputMinimum, out int pdwOutputMaximum);

		void GetStreamCount(out int pcInputStreams, out int pcOutputStreams);

		void GetStreamIds([In] int dwInputIdArraySize, [In] [Out] IntPtr pdwInputIDs, [In] int dwOutputIdArraySize, [In] [Out] IntPtr pdwOutputIDs);

		void GetInputStreamInfo([In] int dwInputStreamId, out MFT_INPUT_STREAM_INFO pStreamInfo);

		void GetOutputStreamInfo([In] int dwOutputStreamId, out MFT_OUTPUT_STREAM_INFO pStreamInfo);

		void GetAttributes(out IMFAttributes pAttributes);

		void GetInputStreamAttributes([In] int dwInputStreamId, out IMFAttributes pAttributes);

		void GetOutputStreamAttributes([In] int dwOutputStreamId, out IMFAttributes pAttributes);

		void DeleteInputStream([In] int dwOutputStreamId);

		void AddInputStreams([In] int cStreams, [In] IntPtr adwStreamIDs);

		void GetInputAvailableType([In] int dwInputStreamId, [In] int dwTypeIndex, out IMFMediaType ppType);

		void GetOutputAvailableType([In] int dwOutputStreamId, [In] int dwTypeIndex, out IMFMediaType ppType);

		void SetInputType([In] int dwInputStreamId, [In] IMFMediaType pType, [In] _MFT_SET_TYPE_FLAGS dwFlags);

		void SetOutputType([In] int dwOutputStreamId, [In] IMFMediaType pType, [In] _MFT_SET_TYPE_FLAGS dwFlags);

		void GetInputCurrentType([In] int dwInputStreamId, out IMFMediaType ppType);

		void GetOutputCurrentType([In] int dwOutputStreamId, out IMFMediaType ppType);

		void GetInputStatus([In] int dwInputStreamId, out _MFT_INPUT_STATUS_FLAGS pdwFlags);

		void GetOutputStatus([In] int dwInputStreamId, out _MFT_OUTPUT_STATUS_FLAGS pdwFlags);

		void SetOutputBounds([In] long hnsLowerBound, [In] long hnsUpperBound);

		void ProcessEvent([In] int dwInputStreamId, [In] IMFMediaEvent pEvent);

		void ProcessMessage([In] MFT_MESSAGE_TYPE eMessage, [In] IntPtr ulParam);

		void ProcessInput([In] int dwInputStreamId, [In] IMFSample pSample, int dwFlags);

		[PreserveSig]
		int ProcessOutput([In] _MFT_PROCESS_OUTPUT_FLAGS dwFlags, [In] int cOutputBufferCount, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] [In] [Out] MFT_OUTPUT_DATA_BUFFER[] pOutputSamples, out _MFT_PROCESS_OUTPUT_STATUS pdwStatus);
	}
}
