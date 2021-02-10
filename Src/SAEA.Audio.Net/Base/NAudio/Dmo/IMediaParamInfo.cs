using System;
using System.Runtime.InteropServices;
using System.Security;

namespace SAEA.Audio.NAudio.Dmo
{
	[Guid("6d6cbb60-a223-44aa-842f-a2f06750be6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
	[ComImport]
	internal interface IMediaParamInfo
	{
		[PreserveSig]
		int GetParamCount(out int paramCount);

		[PreserveSig]
		int GetParamInfo(int paramIndex, ref MediaParamInfo paramInfo);

		[PreserveSig]
		int GetParamText(int paramIndex, out IntPtr paramText);

		[PreserveSig]
		int GetNumTimeFormats(out int numTimeFormats);

		[PreserveSig]
		int GetSupportedTimeFormat(int formatIndex, out Guid guidTimeFormat);

		[PreserveSig]
		int GetCurrentTimeFormat(out Guid guidTimeFormat, out int mediaTimeData);
	}
}
