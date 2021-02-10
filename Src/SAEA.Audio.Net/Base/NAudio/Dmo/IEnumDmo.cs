using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.Dmo
{
	[Guid("2c3cd98a-2bfa-4a53-9c27-5249ba64ba0f"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IEnumDmo
	{
		int Next(int itemsToFetch, out Guid clsid, out IntPtr name, out int itemsFetched);

		int Skip(int itemsToSkip);

		int Reset();

		int Clone(out IEnumDmo enumPointer);
	}
}
