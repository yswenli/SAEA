using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace SAEA.Audio.Base.NAudio.Wave
{
	internal class ComStream : Stream, IStream
	{
		private Stream stream;

		public override bool CanRead
		{
			get
			{
				return this.stream.CanRead;
			}
		}

		public override bool CanSeek
		{
			get
			{
				return this.stream.CanSeek;
			}
		}

		public override bool CanWrite
		{
			get
			{
				return this.stream.CanWrite;
			}
		}

		public override long Length
		{
			get
			{
				return this.stream.Length;
			}
		}

		public override long Position
		{
			get
			{
				return this.stream.Position;
			}
			set
			{
				this.stream.Position = value;
			}
		}

		public ComStream(Stream stream) : this(stream, true)
		{
		}

		internal ComStream(Stream stream, bool synchronizeStream)
		{
			if (stream == null)
			{
				throw new ArgumentNullException("stream");
			}
			if (synchronizeStream)
			{
				stream = Stream.Synchronized(stream);
			}
			this.stream = stream;
		}

		void IStream.Clone(out IStream ppstm)
		{
			ppstm = null;
		}

		void IStream.Commit(int grfCommitFlags)
		{
			this.stream.Flush();
		}

		void IStream.CopyTo(IStream pstm, long cb, IntPtr pcbRead, IntPtr pcbWritten)
		{
		}

		void IStream.LockRegion(long libOffset, long cb, int dwLockType)
		{
		}

		void IStream.Read(byte[] pv, int cb, IntPtr pcbRead)
		{
			if (!this.CanRead)
			{
				throw new InvalidOperationException("Stream is not readable.");
			}
			int val = this.Read(pv, 0, cb);
			if (pcbRead != IntPtr.Zero)
			{
				Marshal.WriteInt32(pcbRead, val);
			}
		}

		void IStream.Revert()
		{
		}

		void IStream.Seek(long dlibMove, int dwOrigin, IntPtr plibNewPosition)
		{
			long val = this.Seek(dlibMove, (SeekOrigin)dwOrigin);
			if (plibNewPosition != IntPtr.Zero)
			{
				Marshal.WriteInt64(plibNewPosition, val);
			}
		}

		void IStream.SetSize(long libNewSize)
		{
			this.SetLength(libNewSize);
		}

		void IStream.Stat(out System.Runtime.InteropServices.ComTypes.STATSTG pstatstg, int grfStatFlag)
		{
			System.Runtime.InteropServices.ComTypes.STATSTG sTATSTG = new System.Runtime.InteropServices.ComTypes.STATSTG
			{
				type = 2,
				cbSize = this.Length,
				grfMode = 0
			};
			if (this.CanWrite && this.CanRead)
			{
				sTATSTG.grfMode |= 2;
			}
			else if (this.CanRead)
			{
				sTATSTG.grfMode |= 0;
			}
			else
			{
				if (!this.CanWrite)
				{
					throw new ObjectDisposedException("Stream");
				}
				sTATSTG.grfMode |= 1;
			}
			pstatstg = sTATSTG;
		}

		void IStream.UnlockRegion(long libOffset, long cb, int dwLockType)
		{
		}

		void IStream.Write(byte[] pv, int cb, IntPtr pcbWritten)
		{
			if (!this.CanWrite)
			{
				throw new InvalidOperationException("Stream is not writeable.");
			}
			this.Write(pv, 0, cb);
			if (pcbWritten != IntPtr.Zero)
			{
				Marshal.WriteInt32(pcbWritten, cb);
			}
		}

		public override void Flush()
		{
			this.stream.Flush();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			return this.stream.Read(buffer, offset, count);
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			return this.stream.Seek(offset, origin);
		}

		public override void SetLength(long value)
		{
			this.stream.SetLength(value);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			this.stream.Write(buffer, offset, count);
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if (this.stream == null)
			{
				return;
			}
			this.stream.Dispose();
			this.stream = null;
		}

		public override void Close()
		{
			base.Close();
			if (this.stream == null)
			{
				return;
			}
			this.stream.Close();
			this.stream = null;
		}
	}
}
