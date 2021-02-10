using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.MediaFoundation
{
	[Guid("5BC8A76B-869A-46A3-9B03-FA218A66AEBE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[ComImport]
	public interface IMFCollection
	{
		void GetElementCount(out int pcElements);

		void GetElement([In] int dwElementIndex, [MarshalAs(UnmanagedType.IUnknown)] out object ppUnkElement);

		void AddElement([MarshalAs(UnmanagedType.IUnknown)] [In] object pUnkElement);

		void RemoveElement([In] int dwElementIndex, [MarshalAs(UnmanagedType.IUnknown)] out object ppUnkElement);

		void InsertElementAt([In] int dwIndex, [MarshalAs(UnmanagedType.IUnknown)] [In] object pUnknown);

		void RemoveAllElements();
	}
}
