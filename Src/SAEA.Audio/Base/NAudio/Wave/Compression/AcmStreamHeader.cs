using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Wave.Compression
{
	internal class AcmStreamHeader : IDisposable
	{
		private AcmStreamHeaderStruct streamHeader;

		private byte[] sourceBuffer;

		private GCHandle hSourceBuffer;

		private byte[] destBuffer;

		private GCHandle hDestBuffer;

		private IntPtr streamHandle;

		private bool firstTime;

		private bool disposed;

		public byte[] SourceBuffer
		{
			get
			{
				return this.sourceBuffer;
			}
		}

		public byte[] DestBuffer
		{
			get
			{
				return this.destBuffer;
			}
		}

		public AcmStreamHeader(IntPtr streamHandle, int sourceBufferLength, int destBufferLength)
		{
			this.streamHeader = new AcmStreamHeaderStruct();
			this.sourceBuffer = new byte[sourceBufferLength];
			this.hSourceBuffer = GCHandle.Alloc(this.sourceBuffer, GCHandleType.Pinned);
			this.destBuffer = new byte[destBufferLength];
			this.hDestBuffer = GCHandle.Alloc(this.destBuffer, GCHandleType.Pinned);
			this.streamHandle = streamHandle;
			this.firstTime = true;
		}

		private void Prepare()
		{
			this.streamHeader.cbStruct = Marshal.SizeOf(this.streamHeader);
			this.streamHeader.sourceBufferLength = this.sourceBuffer.Length;
			this.streamHeader.sourceBufferPointer = this.hSourceBuffer.AddrOfPinnedObject();
			this.streamHeader.destBufferLength = this.destBuffer.Length;
			this.streamHeader.destBufferPointer = this.hDestBuffer.AddrOfPinnedObject();
			MmException.Try(AcmInterop.acmStreamPrepareHeader(this.streamHandle, this.streamHeader, 0), "acmStreamPrepareHeader");
		}

		private void Unprepare()
		{
			this.streamHeader.sourceBufferLength = this.sourceBuffer.Length;
			this.streamHeader.sourceBufferPointer = this.hSourceBuffer.AddrOfPinnedObject();
			this.streamHeader.destBufferLength = this.destBuffer.Length;
			this.streamHeader.destBufferPointer = this.hDestBuffer.AddrOfPinnedObject();
			MmResult mmResult = AcmInterop.acmStreamUnprepareHeader(this.streamHandle, this.streamHeader, 0);
			if (mmResult != MmResult.NoError)
			{
				throw new MmException(mmResult, "acmStreamUnprepareHeader");
			}
		}

		public void Reposition()
		{
			this.firstTime = true;
		}

		public int Convert(int bytesToConvert, out int sourceBytesConverted)
		{
			this.Prepare();
			try
			{
				this.streamHeader.sourceBufferLength = bytesToConvert;
				this.streamHeader.sourceBufferLengthUsed = bytesToConvert;
				AcmStreamConvertFlags streamConvertFlags = this.firstTime ? (AcmStreamConvertFlags.BlockAlign | AcmStreamConvertFlags.Start) : AcmStreamConvertFlags.BlockAlign;
				MmException.Try(AcmInterop.acmStreamConvert(this.streamHandle, this.streamHeader, streamConvertFlags), "acmStreamConvert");
				this.firstTime = false;
				sourceBytesConverted = this.streamHeader.sourceBufferLengthUsed;
			}
			finally
			{
				this.Unprepare();
			}
			return this.streamHeader.destBufferLengthUsed;
		}

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			this.Dispose(true);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!this.disposed)
			{
				this.sourceBuffer = null;
				this.destBuffer = null;
				this.hSourceBuffer.Free();
				this.hDestBuffer.Free();
			}
			this.disposed = true;
		}

		~AcmStreamHeader()
		{
			this.Dispose(false);
		}
	}
}
