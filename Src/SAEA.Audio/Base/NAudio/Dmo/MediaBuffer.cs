using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Dmo
{
	public class MediaBuffer : IMediaBuffer, IDisposable
	{
		private IntPtr buffer;

		private int length;

		private readonly int maxLength;

		public int Length
		{
			get
			{
				return this.length;
			}
			set
			{
				if (this.length > this.maxLength)
				{
					throw new ArgumentException("Cannot be greater than maximum buffer size");
				}
				this.length = value;
			}
		}

		public MediaBuffer(int maxLength)
		{
			this.buffer = Marshal.AllocCoTaskMem(maxLength);
			this.maxLength = maxLength;
		}

		public void Dispose()
		{
			if (this.buffer != IntPtr.Zero)
			{
				Marshal.FreeCoTaskMem(this.buffer);
				this.buffer = IntPtr.Zero;
				GC.SuppressFinalize(this);
			}
		}

		~MediaBuffer()
		{
			this.Dispose();
		}

		int IMediaBuffer.SetLength(int length)
		{
			if (length > this.maxLength)
			{
				return -2147483645;
			}
			this.length = length;
			return 0;
		}

		int IMediaBuffer.GetMaxLength(out int maxLength)
		{
			maxLength = this.maxLength;
			return 0;
		}

		int IMediaBuffer.GetBufferAndLength(IntPtr bufferPointerPointer, IntPtr validDataLengthPointer)
		{
			if (bufferPointerPointer != IntPtr.Zero)
			{
				Marshal.WriteIntPtr(bufferPointerPointer, this.buffer);
			}
			if (validDataLengthPointer != IntPtr.Zero)
			{
				Marshal.WriteInt32(validDataLengthPointer, this.length);
			}
			return 0;
		}

		public void LoadData(byte[] data, int bytes)
		{
			this.Length = bytes;
			Marshal.Copy(data, 0, this.buffer, bytes);
		}

		public void RetrieveData(byte[] data, int offset)
		{
			Marshal.Copy(this.buffer, data, offset, this.Length);
		}
	}
}
