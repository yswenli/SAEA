using SAEA.Audio.Base.NAudio.Utils;
using System;

namespace SAEA.Audio.Base.NAudio.Wave
{
    public class BlockAlignReductionStream : WaveStream
	{
		private WaveStream sourceStream;

		private long position;

		private readonly CircularBuffer circularBuffer;

		private long bufferStartPosition;

		private byte[] sourceBuffer;

		private readonly object lockObject = new object();

		public override int BlockAlign
		{
			get
			{
				return this.WaveFormat.BitsPerSample / 8 * this.WaveFormat.Channels;
			}
		}

		public override WaveFormat WaveFormat
		{
			get
			{
				return this.sourceStream.WaveFormat;
			}
		}

		public override long Length
		{
			get
			{
				return this.sourceStream.Length;
			}
		}

		public override long Position
		{
			get
			{
				return this.position;
			}
			set
			{
				object obj = this.lockObject;
				lock (obj)
				{
					if (this.position != value)
					{
						if (this.position % (long)this.BlockAlign != 0L)
						{
							throw new ArgumentException("Position must be block aligned");
						}
						long num = value - value % (long)this.sourceStream.BlockAlign;
						if (this.sourceStream.Position != num)
						{
							this.sourceStream.Position = num;
							this.circularBuffer.Reset();
							this.bufferStartPosition = this.sourceStream.Position;
						}
						this.position = value;
					}
				}
			}
		}

		private long BufferEndPosition
		{
			get
			{
				return this.bufferStartPosition + (long)this.circularBuffer.Count;
			}
		}

		public BlockAlignReductionStream(WaveStream sourceStream)
		{
			this.sourceStream = sourceStream;
			this.circularBuffer = new CircularBuffer(sourceStream.WaveFormat.AverageBytesPerSecond * 4);
		}

		private byte[] GetSourceBuffer(int size)
		{
			if (this.sourceBuffer == null || this.sourceBuffer.Length < size)
			{
				this.sourceBuffer = new byte[size * 2];
			}
			return this.sourceBuffer;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && this.sourceStream != null)
			{
				this.sourceStream.Dispose();
				this.sourceStream = null;
			}
			base.Dispose(disposing);
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			object obj = this.lockObject;
			int result;
			lock (obj)
			{
				while (this.BufferEndPosition < this.position + (long)count)
				{
					int num = count;
					if (num % this.sourceStream.BlockAlign != 0)
					{
						num = count + this.sourceStream.BlockAlign - count % this.sourceStream.BlockAlign;
					}
					int num2 = this.sourceStream.Read(this.GetSourceBuffer(num), 0, num);
					this.circularBuffer.Write(this.GetSourceBuffer(num), 0, num2);
					if (num2 == 0)
					{
						break;
					}
				}
				if (this.bufferStartPosition < this.position)
				{
					this.circularBuffer.Advance((int)(this.position - this.bufferStartPosition));
					this.bufferStartPosition = this.position;
				}
				int num3 = this.circularBuffer.Read(buffer, offset, count);
				this.position += (long)num3;
				this.bufferStartPosition = this.position;
				result = num3;
			}
			return result;
		}
	}
}
