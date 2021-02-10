using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.Wave.Asio
{
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	internal struct AsioTimeInfo
	{
		public double speed;

		public Asio64Bit systemTime;

		public Asio64Bit samplePosition;

		public double sampleRate;

		public AsioTimeInfoFlags flags;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 12)]
		public string reserved;
	}
}
