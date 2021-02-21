using System;
using System.Runtime.InteropServices;
using System.Security;

namespace SAEA.Audio.Base.NAudio.Dmo
{
	[Guid("59eff8b9-938c-4a26-82f2-95cb84cdc837"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
	[ComImport]
	public interface IMediaBuffer
	{
		[PreserveSig]
		int SetLength(int length);

		[PreserveSig]
		int GetMaxLength(out int maxLength);

		[PreserveSig]
		int GetBufferAndLength(IntPtr bufferPointerPointer, IntPtr validDataLengthPointer);
	}
}
