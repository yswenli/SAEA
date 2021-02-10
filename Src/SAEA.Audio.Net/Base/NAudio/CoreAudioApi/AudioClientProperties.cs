using System;

namespace SAEA.Audio.NAudio.CoreAudioApi
{
	public struct AudioClientProperties
	{
		public uint cbSize;

		public int bIsOffload;

		public AudioStreamCategory eCategory;

		public AudioClientStreamOptions Options;
	}
}
