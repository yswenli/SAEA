using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.MediaFoundation
{
	[Guid("E7FE2E12-661C-40DA-92F9-4F002AB67627"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[ComImport]
	public interface IMFReadWriteClassFactory
	{
		void CreateInstanceFromURL([MarshalAs(UnmanagedType.LPStruct)] [In] Guid clsid, [MarshalAs(UnmanagedType.LPWStr)] [In] string pwszURL, [MarshalAs(UnmanagedType.Interface)] [In] IMFAttributes pAttributes, [MarshalAs(UnmanagedType.LPStruct)] [In] Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppvObject);

		void CreateInstanceFromObject([MarshalAs(UnmanagedType.LPStruct)] [In] Guid clsid, [MarshalAs(UnmanagedType.IUnknown)] [In] object punkObject, [MarshalAs(UnmanagedType.Interface)] [In] IMFAttributes pAttributes, [MarshalAs(UnmanagedType.LPStruct)] [In] Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppvObject);
	}
}
