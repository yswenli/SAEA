using System;

namespace SAEA.Audio.Base.NAudio.Wave.Compression
{
	[Flags]
	internal enum AcmStreamOpenFlags
	{
		Query = 1,
		Async = 2,
		NonRealTime = 4,
		CallbackTypeMask = 458752,
		CallbackNull = 0,
		CallbackWindow = 65536,
		CallbackTask = 131072,
		CallbackFunction = 196608,
		CallbackThread = 131072,
		CallbackEvent = 327680
	}
}
