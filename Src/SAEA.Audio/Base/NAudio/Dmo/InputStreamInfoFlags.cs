using System;

namespace SAEA.Audio.Base.NAudio.Dmo
{
	[Flags]
	internal enum InputStreamInfoFlags
	{
		None = 0,
		DMO_INPUT_STREAMF_WHOLE_SAMPLES = 1,
		DMO_INPUT_STREAMF_SINGLE_SAMPLE_PER_BUFFER = 2,
		DMO_INPUT_STREAMF_FIXED_SAMPLE_SIZE = 4,
		DMO_INPUT_STREAMF_HOLDS_BUFFERS = 8
	}
}
