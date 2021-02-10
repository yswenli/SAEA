using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.Wave.Compression
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 4)]
	internal struct AcmFormatChoose
	{
		public int structureSize;

		public AcmFormatChooseStyleFlags styleFlags;

		public IntPtr ownerWindowHandle;

		public IntPtr selectedWaveFormatPointer;

		public int selectedWaveFormatByteSize;

		[MarshalAs(UnmanagedType.LPTStr)]
		public string title;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 48)]
		public string formatTagDescription;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
		public string formatDescription;

		[MarshalAs(UnmanagedType.LPTStr)]
		public string name;

		public int nameByteSize;

		public AcmFormatEnumFlags formatEnumFlags;

		public IntPtr waveFormatEnumPointer;

		public IntPtr instanceHandle;

		[MarshalAs(UnmanagedType.LPTStr)]
		public string templateName;

		public IntPtr customData;

		public AcmInterop.AcmFormatChooseHookProc windowCallbackFunction;
	}
}
