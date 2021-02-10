using System;

namespace SAEA.Audio.NAudio.Utils
{
	public class CircularBuffer
	{
		private readonly byte[] buffer;

		private readonly object lockObject;

		private int writePosition;

		private int readPosition;

		private int byteCount;

		public int MaxLength
		{
			get
			{
				return this.buffer.Length;
			}
		}

		public int Count
		{
			get
			{
				object obj = this.lockObject;
				int result;
				lock (obj)
				{
					result = this.byteCount;
				}
				return result;
			}
		}

		public CircularBuffer(int size)
		{
			this.buffer = new byte[size];
			this.lockObject = new object();
		}

		public int Write(byte[] data, int offset, int count)
		{
			object obj = this.lockObject;
			int result;
			lock (obj)
			{
				int num = 0;
				if (count > this.buffer.Length - this.byteCount)
				{
					count = this.buffer.Length - this.byteCount;
				}
				int num2 = Math.Min(this.buffer.Length - this.writePosition, count);
				Array.Copy(data, offset, this.buffer, this.writePosition, num2);
				this.writePosition += num2;
				this.writePosition %= this.buffer.Length;
				num += num2;
				if (num < count)
				{
					Array.Copy(data, offset + num, this.buffer, this.writePosition, count - num);
					this.writePosition += count - num;
					num = count;
				}
				this.byteCount += num;
				result = num;
			}
			return result;
		}

		public int Read(byte[] data, int offset, int count)
		{
			object obj = this.lockObject;
			int result;
			lock (obj)
			{
				if (count > this.byteCount)
				{
					count = this.byteCount;
				}
				int num = 0;
				int num2 = Math.Min(this.buffer.Length - this.readPosition, count);
				Array.Copy(this.buffer, this.readPosition, data, offset, num2);
				num += num2;
				this.readPosition += num2;
				this.readPosition %= this.buffer.Length;
				if (num < count)
				{
					Array.Copy(this.buffer, this.readPosition, data, offset + num, count - num);
					this.readPosition += count - num;
					num = count;
				}
				this.byteCount -= num;
				result = num;
			}
			return result;
		}

		public void Reset()
		{
			object obj = this.lockObject;
			lock (obj)
			{
				this.ResetInner();
			}
		}

		private void ResetInner()
		{
			this.byteCount = 0;
			this.readPosition = 0;
			this.writePosition = 0;
		}

		public void Advance(int count)
		{
			object obj = this.lockObject;
			lock (obj)
			{
				if (count >= this.byteCount)
				{
					this.ResetInner();
				}
				else
				{
					this.byteCount -= count;
					this.readPosition += count;
					this.readPosition %= this.MaxLength;
				}
			}
		}
	}
}
