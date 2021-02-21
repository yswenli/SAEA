using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Wave.Compression
{
	internal struct AcmFormatTagDetails
	{
		public int structureSize;

		public int formatTagIndex;

		public int formatTag;

		public int formatSize;

		public AcmDriverDetailsSupportFlags supportFlags;

		public int standardFormatsCount;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 48)]
		public string formatDescription;

		public const int FormatTagDescriptionChars = 48;
	}
}
