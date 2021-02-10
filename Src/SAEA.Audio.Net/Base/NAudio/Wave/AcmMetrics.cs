using System;

namespace SAEA.Audio.NAudio.Wave
{
	internal enum AcmMetrics
	{
		CountDrivers = 1,
		CountCodecs,
		CountConverters,
		CountFilters,
		CountDisabled,
		CountHardware,
		CountLocalDrivers = 20,
		CountLocalCodecs,
		CountLocalConverters,
		CountLocalFilters,
		CountLocalDisabled,
		HardwareWaveInput = 30,
		HardwareWaveOutput,
		MaxSizeFormat = 50,
		MaxSizeFilter,
		DriverSupport = 100,
		DriverPriority
	}
}
