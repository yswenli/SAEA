using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.CoreAudioApi.Interfaces
{
	[Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IAudioEndpointVolume
	{
		int RegisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);

		int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);

		int GetChannelCount(out int pnChannelCount);

		int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);

		int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);

		int GetMasterVolumeLevel(out float pfLevelDB);

		int GetMasterVolumeLevelScalar(out float pfLevel);

		int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);

		int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);

		int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);

		int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);

		int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);

		int GetMute(out bool pbMute);

		int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);

		int VolumeStepUp(ref Guid pguidEventContext);

		int VolumeStepDown(ref Guid pguidEventContext);

		int QueryHardwareSupport(out uint pdwHardwareSupportMask);

		int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
	}
}
