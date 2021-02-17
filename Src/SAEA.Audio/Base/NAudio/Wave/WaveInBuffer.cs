using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Wave
{
	internal class WaveInBuffer : IDisposable
	{
		private readonly WaveHeader header;

		private readonly int bufferSize;

		private readonly byte[] buffer;

		private GCHandle hBuffer;

		private IntPtr waveInHandle;

		private GCHandle hHeader;

		private GCHandle hThis;

		public byte[] Data
		{
			get
			{
				return this.buffer;
			}
		}

		public bool Done
		{
			get
			{
				return (this.header.flags & WaveHeaderFlags.Done) == WaveHeaderFlags.Done;
			}
		}

		public bool InQueue
		{
			get
			{
				return (this.header.flags & WaveHeaderFlags.InQueue) == WaveHeaderFlags.InQueue;
			}
		}

		public int BytesRecorded
		{
			get
			{
				return this.header.bytesRecorded;
			}
		}

		public int BufferSize
		{
			get
			{
				return this.bufferSize;
			}
		}

		public WaveInBuffer(IntPtr waveInHandle, int bufferSize)
		{
			this.bufferSize = bufferSize;
			this.buffer = new byte[bufferSize];
			this.hBuffer = GCHandle.Alloc(this.buffer, GCHandleType.Pinned);
			this.waveInHandle = waveInHandle;
			this.header = new WaveHeader();
			this.hHeader = GCHandle.Alloc(this.header, GCHandleType.Pinned);
			this.header.dataBuffer = this.hBuffer.AddrOfPinnedObject();
			this.header.bufferLength = bufferSize;
			this.header.loops = 1;
			this.hThis = GCHandle.Alloc(this);
			this.header.userData = (IntPtr)this.hThis;
			MmException.Try(WaveInterop.waveInPrepareHeader(waveInHandle, this.header, Marshal.SizeOf(this.header)), "waveInPrepareHeader");
		}

		public void Reuse()
		{
			MmException.Try(WaveInterop.waveInUnprepareHeader(this.waveInHandle, this.header, Marshal.SizeOf(this.header)), "waveUnprepareHeader");
			MmException.Try(WaveInterop.waveInPrepareHeader(this.waveInHandle, this.header, Marshal.SizeOf(this.header)), "waveInPrepareHeader");
			MmException.Try(WaveInterop.waveInAddBuffer(this.waveInHandle, this.header, Marshal.SizeOf(this.header)), "waveInAddBuffer");
		}

		~WaveInBuffer()
		{
			this.Dispose(false);
		}

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			this.Dispose(true);
		}

		protected void Dispose(bool disposing)
		{
			if (this.waveInHandle != IntPtr.Zero)
			{
				WaveInterop.waveInUnprepareHeader(this.waveInHandle, this.header, Marshal.SizeOf(this.header));
				this.waveInHandle = IntPtr.Zero;
			}
			if (this.hHeader.IsAllocated)
			{
				this.hHeader.Free();
			}
			if (this.hBuffer.IsAllocated)
			{
				this.hBuffer.Free();
			}
			if (this.hThis.IsAllocated)
			{
				this.hThis.Free();
			}
		}
	}
}
