using System;

namespace SAEA.Audio.NAudio.Wave.Compression
{
	[Flags]
	internal enum AcmFormatSuggestFlags
	{
		FormatTag = 65536,
		Channels = 131072,
		SamplesPerSecond = 262144,
		BitsPerSample = 524288,
		TypeMask = 16711680
	}
}
