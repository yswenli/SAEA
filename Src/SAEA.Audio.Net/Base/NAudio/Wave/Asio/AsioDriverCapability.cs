using System;

namespace SAEA.Audio.NAudio.Wave.Asio
{
	public class AsioDriverCapability
	{
		public string DriverName;

		public int NbInputChannels;

		public int NbOutputChannels;

		public int InputLatency;

		public int OutputLatency;

		public int BufferMinSize;

		public int BufferMaxSize;

		public int BufferPreferredSize;

		public int BufferGranularity;

		public double SampleRate;

		public AsioChannelInfo[] InputChannelInfos;

		public AsioChannelInfo[] OutputChannelInfos;
	}
}
