using SAEA.Audio.Base.NAudio.Wave;
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace SAEA.Audio.Base.NAudio.MediaFoundation
{
    public static class MediaFoundationInterop
	{
		public const int MF_SOURCE_READER_ALL_STREAMS = -2;

		public const int MF_SOURCE_READER_FIRST_AUDIO_STREAM = -3;

		public const int MF_SOURCE_READER_FIRST_VIDEO_STREAM = -4;

		public const int MF_SOURCE_READER_MEDIASOURCE = -1;

		public const int MF_SDK_VERSION = 2;

		public const int MF_API_VERSION = 112;

		public const int MF_VERSION = 131184;

		[DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)]
		public static extern void MFStartup(int version, int dwFlags = 0);

		[DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)]
		public static extern void MFShutdown();

		[DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)]
		internal static extern void MFCreateMediaType(out IMFMediaType ppMFType);

		[DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)]
		internal static extern void MFInitMediaTypeFromWaveFormatEx([In] IMFMediaType pMFType, [In] WaveFormat pWaveFormat, [In] int cbBufSize);

		[DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)]
		internal static extern void MFCreateWaveFormatExFromMFMediaType(IMFMediaType pMFType, ref IntPtr ppWF, ref int pcbSize, int flags = 0);

		[DllImport("mfreadwrite.dll", ExactSpelling = true, PreserveSig = false)]
		public static extern void MFCreateSourceReaderFromURL([MarshalAs(UnmanagedType.LPWStr)] [In] string pwszURL, [In] IMFAttributes pAttributes, [MarshalAs(UnmanagedType.Interface)] out IMFSourceReader ppSourceReader);

		[DllImport("mfreadwrite.dll", ExactSpelling = true, PreserveSig = false)]
		public static extern void MFCreateSourceReaderFromByteStream([In] IMFByteStream pByteStream, [In] IMFAttributes pAttributes, [MarshalAs(UnmanagedType.Interface)] out IMFSourceReader ppSourceReader);

		[DllImport("mfreadwrite.dll", ExactSpelling = true, PreserveSig = false)]
		public static extern void MFCreateSinkWriterFromURL([MarshalAs(UnmanagedType.LPWStr)] [In] string pwszOutputURL, [In] IMFByteStream pByteStream, [In] IMFAttributes pAttributes, out IMFSinkWriter ppSinkWriter);

		[DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)]
		public static extern void MFCreateMFByteStreamOnStream([In] IStream punkStream, out IMFByteStream ppByteStream);

		[DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)]
		public static extern void MFTEnumEx([In] Guid guidCategory, [In] _MFT_ENUM_FLAG flags, [In] MFT_REGISTER_TYPE_INFO pInputType, [In] MFT_REGISTER_TYPE_INFO pOutputType, out IntPtr pppMFTActivate, out int pcMFTActivate);

		[DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)]
		internal static extern void MFCreateSample(out IMFSample ppIMFSample);

		[DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)]
		internal static extern void MFCreateMemoryBuffer(int cbMaxLength, out IMFMediaBuffer ppBuffer);

		[DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)]
		internal static extern void MFCreateAttributes([MarshalAs(UnmanagedType.Interface)] out IMFAttributes ppMFAttributes, [In] int cInitialSize);

		[DllImport("mf.dll", ExactSpelling = true, PreserveSig = false)]
		public static extern void MFTranscodeGetAudioOutputAvailableTypes([MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidSubType, [In] _MFT_ENUM_FLAG dwMFTFlags, [In] IMFAttributes pCodecConfig, [MarshalAs(UnmanagedType.Interface)] out IMFCollection ppAvailableTypes);
	}
}
