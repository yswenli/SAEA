using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Wave.Asio
{
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct AsioChannelInfo
	{
		public int channel;

		public bool isInput;

		public bool isActive;

		public int channelGroup;

		[MarshalAs(UnmanagedType.U4)]
		public AsioSampleType type;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string name;
	}
}
