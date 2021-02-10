using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.CoreAudioApi.Interfaces
{
	[Guid("1BE09788-6894-4089-8586-9A2A6C265AC5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IMMEndpoint
	{
		int GetDataFlow(out DataFlow dataFlow);
	}
}
