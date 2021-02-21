using System;

namespace SAEA.Audio.Base.NAudio.Wave.Compression
{
	[Flags]
	public enum AcmDriverDetailsSupportFlags
	{
		Codec = 1,
		Converter = 2,
		Filter = 4,
		Hardware = 8,
		Async = 16,
		Local = 1073741824,
		Disabled = -2147483648
	}
}
