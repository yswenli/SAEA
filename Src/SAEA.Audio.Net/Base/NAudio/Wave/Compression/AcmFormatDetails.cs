using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.Wave.Compression
{
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	internal struct AcmFormatDetails
	{
		public int structSize;

		public int formatIndex;

		public int formatTag;

		public AcmDriverDetailsSupportFlags supportFlags;

		public IntPtr waveFormatPointer;

		public int waveFormatByteSize;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
		public string formatDescription;

		public const int FormatDescriptionChars = 128;
	}
}
