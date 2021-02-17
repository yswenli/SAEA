using System;
using System.IO;

namespace SAEA.Audio.Base.NAudio.Wave
{
	public abstract class WaveStream : Stream, IWaveProvider
	{
		public abstract WaveFormat WaveFormat
		{
			get;
		}

		public override bool CanRead
		{
			get
			{
				return true;
			}
		}

		public override bool CanSeek
		{
			get
			{
				return true;
			}
		}

		public override bool CanWrite
		{
			get
			{
				return false;
			}
		}

		public virtual int BlockAlign
		{
			get
			{
				return this.WaveFormat.BlockAlign;
			}
		}

		public virtual TimeSpan CurrentTime
		{
			get
			{
				return TimeSpan.FromSeconds((double)this.Position / (double)this.WaveFormat.AverageBytesPerSecond);
			}
			set
			{
				this.Position = (long)(value.TotalSeconds * (double)this.WaveFormat.AverageBytesPerSecond);
			}
		}

		public virtual TimeSpan TotalTime
		{
			get
			{
				return TimeSpan.FromSeconds((double)this.Length / (double)this.WaveFormat.AverageBytesPerSecond);
			}
		}

		public override void Flush()
		{
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			if (origin == SeekOrigin.Begin)
			{
				this.Position = offset;
			}
			else if (origin == SeekOrigin.Current)
			{
				this.Position += offset;
			}
			else
			{
				this.Position = this.Length + offset;
			}
			return this.Position;
		}

		public override void SetLength(long length)
		{
			throw new NotSupportedException("Can't set length of a WaveFormatString");
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException("Can't write to a WaveFormatString");
		}

		public void Skip(int seconds)
		{
			long num = this.Position + (long)(this.WaveFormat.AverageBytesPerSecond * seconds);
			if (num > this.Length)
			{
				this.Position = this.Length;
				return;
			}
			if (num < 0L)
			{
				this.Position = 0L;
				return;
			}
			this.Position = num;
		}

		public virtual bool HasData(int count)
		{
			return this.Position < this.Length;
		}
	}
}
