using System;

namespace SAEA.Audio.Base.NAudio.MediaFoundation
{
	[Flags]
	public enum _MFT_OUTPUT_STATUS_FLAGS
	{
		None = 0,
		MFT_OUTPUT_STATUS_SAMPLE_READY = 1
	}
}
