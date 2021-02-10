using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.Wave.Asio
{
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	internal struct AsioBufferInfo
	{
		public bool isInput;

		public int channelNum;

		public IntPtr pBuffer0;

		public IntPtr pBuffer1;

		public IntPtr Buffer(int bufferIndex)
		{
			if (bufferIndex != 0)
			{
				return this.pBuffer1;
			}
			return this.pBuffer0;
		}
	}
}
