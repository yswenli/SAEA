using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Wave.Compression
{
	[StructLayout(LayoutKind.Sequential)]
	public class WaveFilter
	{
		public int StructureSize = Marshal.SizeOf(typeof(WaveFilter));

		public int FilterTag;

		public int Filter;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
		public int[] Reserved;
	}
}
