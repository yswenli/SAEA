using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Wave.Compression
{
	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	internal struct AcmDriverDetails
	{
		public int structureSize;

		public uint fccType;

		public uint fccComp;

		public ushort manufacturerId;

		public ushort productId;

		public uint acmVersion;

		public uint driverVersion;

		public AcmDriverDetailsSupportFlags supportFlags;

		public int formatTagsCount;

		public int filterTagsCount;

		public IntPtr hicon;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string shortName;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
		public string longName;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
		public string copyright;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
		public string licensing;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
		public string features;

		private const int ShortNameChars = 32;

		private const int LongNameChars = 128;

		private const int CopyrightChars = 80;

		private const int LicensingChars = 128;

		private const int FeaturesChars = 512;
	}
}
