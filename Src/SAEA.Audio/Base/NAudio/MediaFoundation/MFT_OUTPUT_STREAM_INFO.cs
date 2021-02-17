using System;

namespace SAEA.Audio.Base.NAudio.MediaFoundation
{
	public struct MFT_OUTPUT_STREAM_INFO
	{
		public _MFT_OUTPUT_STREAM_INFO_FLAGS dwFlags;

		public int cbSize;

		public int cbAlignment;
	}
}
