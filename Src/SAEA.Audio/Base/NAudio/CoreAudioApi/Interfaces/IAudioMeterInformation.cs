using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.CoreAudioApi.Interfaces
{
	[Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IAudioMeterInformation
	{
		int GetPeakValue(out float pfPeak);

		int GetMeteringChannelCount(out int pnChannelCount);

		int GetChannelsPeakValues(int u32ChannelCount, [In] IntPtr afPeakValues);

		int QueryHardwareSupport(out int pdwHardwareSupportMask);
	}
}
