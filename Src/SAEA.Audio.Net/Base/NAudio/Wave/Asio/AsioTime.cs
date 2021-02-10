using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.Wave.Asio
{
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	internal struct AsioTime
	{
		public int reserved1;

		public int reserved2;

		public int reserved3;

		public int reserved4;

		public AsioTimeInfo timeInfo;

		public AsioTimeCode timeCode;
	}
}
