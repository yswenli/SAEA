using System;

namespace SAEA.Audio.Base.NAudio.Wave
{
	public class WaveOffsetStream : WaveStream
	{
		private WaveStream sourceStream;

		private long audioStartPosition;

		private long sourceOffsetBytes;

		private long sourceLengthBytes;

		private long length;

		private readonly int bytesPerSample;

		private long position;

		private TimeSpan startTime;

		private TimeSpan sourceOffset;

		private TimeSpan sourceLength;

		private readonly object lockObject = new object();

		public TimeSpan StartTime
		{
			get
			{
				return this.startTime;
			}
			set
			{
				object obj = this.lockObject;
				lock (obj)
				{
					this.startTime = value;
					this.audioStartPosition = (long)(this.startTime.TotalSeconds * (double)this.sourceStream.WaveFormat.SampleRate) * (long)this.bytesPerSample;
					this.length = this.audioStartPosition + this.sourceLengthBytes;
					this.Position = this.Position;
				}
			}
		}

		public TimeSpan SourceOffset
		{
			get
			{
				return this.sourceOffset;
			}
			set
			{
				object obj = this.lockObject;
				lock (obj)
				{
					this.sourceOffset = value;
					this.sourceOffsetBytes = (long)(this.sourceOffset.TotalSeconds * (double)this.sourceStream.WaveFormat.SampleRate) * (long)this.bytesPerSample;
					this.Position = this.Position;
				}
			}
		}

		public TimeSpan SourceLength
		{
			get
			{
				return this.sourceLength;
			}
			set
			{
				object obj = this.lockObject;
				lock (obj)
				{
					this.sourceLength = value;
					this.sourceLengthBytes = (long)(this.sourceLength.TotalSeconds * (double)this.sourceStream.WaveFormat.SampleRate) * (long)this.bytesPerSample;
					this.length = this.audioStartPosition + this.sourceLengthBytes;
					this.Position = this.Position;
				}
			}
		}

		public override int BlockAlign
		{
			get
			{
				return this.sourceStream.BlockAlign;
			}
		}

		public override long Length
		{
			get
			{
				return this.length;
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
					value -= value % (long)this.BlockAlign;
					if (value < this.audioStartPosition)
					{
						this.sourceStream.Position = this.sourceOffsetBytes;
					}
					else
					{
						this.sourceStream.Position = this.sourceOffsetBytes + (value - this.audioStartPosition);
					}
					this.position = value;
				}
			}
		}

		public override WaveFormat WaveFormat
		{
			get
			{
				return this.sourceStream.WaveFormat;
			}
		}

		public WaveOffsetStream(WaveStream sourceStream, TimeSpan startTime, TimeSpan sourceOffset, TimeSpan sourceLength)
		{
			if (sourceStream.WaveFormat.Encoding != WaveFormatEncoding.Pcm)
			{
				throw new ArgumentException("Only PCM supported");
			}
			this.sourceStream = sourceStream;
			this.position = 0L;
			this.bytesPerSample = sourceStream.WaveFormat.BitsPerSample / 8 * sourceStream.WaveFormat.Channels;
			this.StartTime = startTime;
			this.SourceOffset = sourceOffset;
			this.SourceLength = sourceLength;
		}

		public WaveOffsetStream(WaveStream sourceStream) : this(sourceStream, TimeSpan.Zero, TimeSpan.Zero, sourceStream.TotalTime)
		{
		}

		public override int Read(byte[] destBuffer, int offset, int numBytes)
		{
			object obj = this.lockObject;
			lock (obj)
			{
				int num = 0;
				if (this.position < this.audioStartPosition)
				{
					num = (int)Math.Min((long)numBytes, this.audioStartPosition - this.position);
					for (int i = 0; i < num; i++)
					{
						destBuffer[i + offset] = 0;
					}
				}
				if (num < numBytes)
				{
					int count = (int)Math.Min((long)(numBytes - num), this.sourceLengthBytes + this.sourceOffsetBytes - this.sourceStream.Position);
					int num2 = this.sourceStream.Read(destBuffer, num + offset, count);
					num += num2;
				}
				for (int j = num; j < numBytes; j++)
				{
					destBuffer[offset + j] = 0;
				}
				this.position += (long)numBytes;
			}
			return numBytes;
		}

		public override bool HasData(int count)
		{
			return this.position + (long)count >= this.audioStartPosition && this.position < this.length && this.sourceStream.HasData(count);
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
	}
}
