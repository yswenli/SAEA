using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.Wave
{
	internal class WaveOutBuffer : IDisposable
	{
		private readonly WaveHeader header;

		private readonly int bufferSize;

		private readonly byte[] buffer;

		private readonly IWaveProvider waveStream;

		private readonly object waveOutLock;

		private GCHandle hBuffer;

		private IntPtr hWaveOut;

		private GCHandle hHeader;

		private GCHandle hThis;

		public bool InQueue
		{
			get
			{
				return (this.header.flags & WaveHeaderFlags.InQueue) == WaveHeaderFlags.InQueue;
			}
		}

		public int BufferSize
		{
			get
			{
				return this.bufferSize;
			}
		}

		public WaveOutBuffer(IntPtr hWaveOut, int bufferSize, IWaveProvider bufferFillStream, object waveOutLock)
		{
			this.bufferSize = bufferSize;
			this.buffer = new byte[bufferSize];
			this.hBuffer = GCHandle.Alloc(this.buffer, GCHandleType.Pinned);
			this.hWaveOut = hWaveOut;
			this.waveStream = bufferFillStream;
			this.waveOutLock = waveOutLock;
			this.header = new WaveHeader();
			this.hHeader = GCHandle.Alloc(this.header, GCHandleType.Pinned);
			this.header.dataBuffer = this.hBuffer.AddrOfPinnedObject();
			this.header.bufferLength = bufferSize;
			this.header.loops = 1;
			this.hThis = GCHandle.Alloc(this);
			this.header.userData = (IntPtr)this.hThis;
			lock (waveOutLock)
			{
				MmException.Try(WaveInterop.waveOutPrepareHeader(hWaveOut, this.header, Marshal.SizeOf(this.header)), "waveOutPrepareHeader");
			}
		}

		~WaveOutBuffer()
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
			if (this.hWaveOut != IntPtr.Zero)
			{
				object obj = this.waveOutLock;
				lock (obj)
				{
					WaveInterop.waveOutUnprepareHeader(this.hWaveOut, this.header, Marshal.SizeOf(this.header));
				}
				this.hWaveOut = IntPtr.Zero;
			}
		}

		internal bool OnDone()
		{
			IWaveProvider obj = this.waveStream;
			int num;
			lock (obj)
			{
				num = this.waveStream.Read(this.buffer, 0, this.buffer.Length);
			}
			if (num == 0)
			{
				return false;
			}
			for (int i = num; i < this.buffer.Length; i++)
			{
				this.buffer[i] = 0;
			}
			this.WriteToWaveOut();
			return true;
		}

		private void WriteToWaveOut()
		{
			object obj = this.waveOutLock;
			MmResult mmResult;
			lock (obj)
			{
				mmResult = WaveInterop.waveOutWrite(this.hWaveOut, this.header, Marshal.SizeOf(this.header));
			}
			if (mmResult != MmResult.NoError)
			{
				throw new MmException(mmResult, "waveOutWrite");
			}
			GC.KeepAlive(this);
		}
	}
}
