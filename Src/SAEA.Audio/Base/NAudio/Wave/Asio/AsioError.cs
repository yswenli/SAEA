using System;

namespace SAEA.Audio.Base.NAudio.Wave.Asio
{
	public enum AsioError
	{
		ASE_OK,
		ASE_SUCCESS = 1061701536,
		ASE_NotPresent = -1000,
		ASE_HWMalfunction,
		ASE_InvalidParameter,
		ASE_InvalidMode,
		ASE_SPNotAdvancing,
		ASE_NoClock,
		ASE_NoMemory
	}
}
