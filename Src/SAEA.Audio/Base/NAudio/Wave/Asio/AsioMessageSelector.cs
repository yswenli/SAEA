using System;

namespace SAEA.Audio.Base.NAudio.Wave.Asio
{
	public enum AsioMessageSelector
	{
		kAsioSelectorSupported = 1,
		kAsioEngineVersion,
		kAsioResetRequest,
		kAsioBufferSizeChange,
		kAsioResyncRequest,
		kAsioLatenciesChanged,
		kAsioSupportsTimeInfo,
		kAsioSupportsTimeCode,
		kAsioMMCCommand,
		kAsioSupportsInputMonitor,
		kAsioSupportsInputGain,
		kAsioSupportsInputMeter,
		kAsioSupportsOutputGain,
		kAsioSupportsOutputMeter,
		kAsioOverload
	}
}
