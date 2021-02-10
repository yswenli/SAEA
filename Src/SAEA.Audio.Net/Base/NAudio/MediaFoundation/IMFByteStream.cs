using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.MediaFoundation
{
	[Guid("ad4c1b00-4bf7-422f-9175-756693d9130d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[ComImport]
	public interface IMFByteStream
	{
		void GetCapabilities(ref int pdwCapabiities);

		void GetLength(ref long pqwLength);

		void SetLength(long qwLength);

		void GetCurrentPosition(ref long pqwPosition);

		void SetCurrentPosition(long qwPosition);

		void IsEndOfStream([MarshalAs(UnmanagedType.Bool)] ref bool pfEndOfStream);

		void Read(IntPtr pb, int cb, ref int pcbRead);

		void BeginRead(IntPtr pb, int cb, IntPtr pCallback, IntPtr punkState);

		void EndRead(IntPtr pResult, ref int pcbRead);

		void Write(IntPtr pb, int cb, ref int pcbWritten);

		void BeginWrite(IntPtr pb, int cb, IntPtr pCallback, IntPtr punkState);

		void EndWrite(IntPtr pResult, ref int pcbWritten);

		void Seek(int SeekOrigin, long llSeekOffset, int dwSeekFlags, ref long pqwCurrentPosition);

		void Flush();

		void Close();
	}
}
