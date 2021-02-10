using System;

namespace SAEA.Audio.NAudio.MediaFoundation
{
	[Flags]
	public enum _MFT_PROCESS_OUTPUT_FLAGS
	{
		None = 0,
		MFT_PROCESS_OUTPUT_DISCARD_WHEN_NO_BUFFER = 1,
		MFT_PROCESS_OUTPUT_REGENERATE_LAST_OUTPUT = 2
	}
}
