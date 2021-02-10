using System;

namespace SAEA.Audio.NAudio.Wave
{
	internal class Mp3Index
	{
		public long FilePosition
		{
			get;
			set;
		}

		public long SamplePosition
		{
			get;
			set;
		}

		public int SampleCount
		{
			get;
			set;
		}

		public int ByteCount
		{
			get;
			set;
		}
	}
}
