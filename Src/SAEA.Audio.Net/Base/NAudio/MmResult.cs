using System;

namespace SAEA.Audio.NAudio
{
	public enum MmResult
	{
		NoError,
		UnspecifiedError,
		BadDeviceId,
		NotEnabled,
		AlreadyAllocated,
		InvalidHandle,
		NoDriver,
		MemoryAllocationError,
		NotSupported,
		BadErrorNumber,
		InvalidFlag,
		InvalidParameter,
		HandleBusy,
		InvalidAlias,
		BadRegistryDatabase,
		RegistryKeyNotFound,
		RegistryReadError,
		RegistryWriteError,
		RegistryDeleteError,
		RegistryValueNotFound,
		NoDriverCallback,
		MoreData,
		WaveBadFormat = 32,
		WaveStillPlaying,
		WaveHeaderUnprepared,
		WaveSync,
		AcmNotPossible = 512,
		AcmBusy,
		AcmHeaderUnprepared,
		AcmCancelled,
		MixerInvalidLine = 1024,
		MixerInvalidControl,
		MixerInvalidValue
	}
}
