using System;

namespace SAEA.Audio.NAudio.Wave.Compression
{
	[Flags]
	public enum AcmFormatEnumFlags
	{
		None = 0,
		Convert = 1048576,
		Hardware = 4194304,
		Input = 8388608,
		Channels = 131072,
		SamplesPerSecond = 262144,
		Output = 16777216,
		Suggest = 2097152,
		BitsPerSample = 524288,
		FormatTag = 65536
	}
}
