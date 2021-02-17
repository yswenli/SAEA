using System;

namespace SAEA.Audio.Base.NAudio.MediaFoundation
{
	public struct MFT_INPUT_STREAM_INFO
	{
		public long hnsMaxLatency;

		public _MFT_INPUT_STREAM_INFO_FLAGS dwFlags;

		public int cbSize;

		public int cbMaxLookahead;

		public int cbAlignment;
	}
}
