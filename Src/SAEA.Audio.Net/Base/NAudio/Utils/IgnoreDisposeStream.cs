using System;
using System.IO;

namespace SAEA.Audio.Base.NAudio.Utils
{
	public class IgnoreDisposeStream : Stream
	{
		public Stream SourceStream
		{
			get;
			private set;
		}

		public bool IgnoreDispose
		{
			get;
			set;
		}

		public override bool CanRead
		{
			get
			{
				return this.SourceStream.CanRead;
			}
		}

		public override bool CanSeek
		{
			get
			{
				return this.SourceStream.CanSeek;
			}
		}

		public override bool CanWrite
		{
			get
			{
				return this.SourceStream.CanWrite;
			}
		}

		public override long Length
		{
			get
			{
				return this.SourceStream.Length;
			}
		}

		public override long Position
		{
			get
			{
				return this.SourceStream.Position;
			}
			set
			{
				this.SourceStream.Position = value;
			}
		}

		public IgnoreDisposeStream(Stream sourceStream)
		{
			this.SourceStream = sourceStream;
			this.IgnoreDispose = true;
		}

		public override void Flush()
		{
			this.SourceStream.Flush();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			return this.SourceStream.Read(buffer, offset, count);
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			return this.SourceStream.Seek(offset, origin);
		}

		public override void SetLength(long value)
		{
			this.SourceStream.SetLength(value);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			this.SourceStream.Write(buffer, offset, count);
		}

		protected override void Dispose(bool disposing)
		{
			if (!this.IgnoreDispose)
			{
				this.SourceStream.Dispose();
				this.SourceStream = null;
			}
		}
	}
}
