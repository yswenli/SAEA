using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SAEA.Audio.Base.NAudio.MediaFoundation
{
	[Guid("7FEE9E9A-4A89-47a6-899C-B6A53A70FB67"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[ComImport]
	public interface IMFActivate : IMFAttributes
	{
		void GetItem([MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidKey, [In] [Out] IntPtr pValue);

		void GetItemType([MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidKey, out int pType);

		void CompareItem([MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidKey, IntPtr value, [MarshalAs(UnmanagedType.Bool)] out bool pbResult);

		void Compare([MarshalAs(UnmanagedType.Interface)] IMFAttributes pTheirs, int matchType, [MarshalAs(UnmanagedType.Bool)] out bool pbResult);

		void GetUINT32([MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidKey, out int punValue);

		void GetUINT64([MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidKey, out long punValue);

		void GetDouble([MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidKey, out double pfValue);

		void GetGUID([MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidKey, out Guid pguidValue);

		void GetStringLength([MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidKey, out int pcchLength);

		void GetString([MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] [Out] StringBuilder pwszValue, int cchBufSize, out int pcchLength);

		void GetAllocatedString([MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] out string ppwszValue, out int pcchLength);

		void GetBlobSize([MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidKey, out int pcbBlobSize);

		void GetBlob([MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidKey, [MarshalAs(UnmanagedType.LPArray)] [Out] byte[] pBuf, int cbBufSize, out int pcbBlobSize);

		void GetAllocatedBlob([MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidKey, out IntPtr ip, out int pcbSize);

		void GetUnknown([MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidKey, [MarshalAs(UnmanagedType.LPStruct)] [In] Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);

		void SetItem([MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidKey, IntPtr value);

		void DeleteItem([MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidKey);

		void DeleteAllItems();

		void SetUINT32([MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidKey, int unValue);

		void SetUINT64([MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidKey, long unValue);

		void SetDouble([MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidKey, double fValue);

		void SetGUID([MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidKey, [MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidValue);

		void SetString([MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] [In] string wszValue);

		void SetBlob([MarshalAs(UnmanagedType.LPStruct)] [In] Guid guidKey, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] [In] byte[] pBuf, int cbBufSize);

		void SetUnknown([MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [MarshalAs(UnmanagedType.IUnknown)] [In] object pUnknown);

		void LockStore();

		void UnlockStore();

		void GetCount(out int pcItems);

		void GetItemByIndex(int unIndex, out Guid pGuidKey, [In] [Out] IntPtr pValue);

		void CopyAllItems([MarshalAs(UnmanagedType.Interface)] [In] IMFAttributes pDest);

		void ActivateObject([MarshalAs(UnmanagedType.LPStruct)] [In] Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);

		void ShutdownObject();

		void DetachObject();
	}
}
