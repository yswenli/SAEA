using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.Wave
{
	[StructLayout(LayoutKind.Explicit, Pack = 2)]
	public class WaveBuffer : IWaveBuffer
	{
		[FieldOffset(0)]
		public int numberOfBytes;

		[FieldOffset(8)]
		private byte[] byteBuffer;

		[FieldOffset(8)]
		private float[] floatBuffer;

		[FieldOffset(8)]
		private short[] shortBuffer;

		[FieldOffset(8)]
		private int[] intBuffer;

		public byte[] ByteBuffer
		{
			get
			{
				return this.byteBuffer;
			}
		}

		public float[] FloatBuffer
		{
			get
			{
				return this.floatBuffer;
			}
		}

		public short[] ShortBuffer
		{
			get
			{
				return this.shortBuffer;
			}
		}

		public int[] IntBuffer
		{
			get
			{
				return this.intBuffer;
			}
		}

		public int MaxSize
		{
			get
			{
				return this.byteBuffer.Length;
			}
		}

		public int ByteBufferCount
		{
			get
			{
				return this.numberOfBytes;
			}
			set
			{
				this.numberOfBytes = this.CheckValidityCount("ByteBufferCount", value, 1);
			}
		}

		public int FloatBufferCount
		{
			get
			{
				return this.numberOfBytes / 4;
			}
			set
			{
				this.numberOfBytes = this.CheckValidityCount("FloatBufferCount", value, 4);
			}
		}

		public int ShortBufferCount
		{
			get
			{
				return this.numberOfBytes / 2;
			}
			set
			{
				this.numberOfBytes = this.CheckValidityCount("ShortBufferCount", value, 2);
			}
		}

		public int IntBufferCount
		{
			get
			{
				return this.numberOfBytes / 4;
			}
			set
			{
				this.numberOfBytes = this.CheckValidityCount("IntBufferCount", value, 4);
			}
		}

		public WaveBuffer(int sizeToAllocateInBytes)
		{
			int num = sizeToAllocateInBytes % 4;
			sizeToAllocateInBytes = ((num == 0) ? sizeToAllocateInBytes : (sizeToAllocateInBytes + 4 - num));
			this.byteBuffer = new byte[sizeToAllocateInBytes];
			this.numberOfBytes = 0;
		}

		public WaveBuffer(byte[] bufferToBoundTo)
		{
			this.BindTo(bufferToBoundTo);
		}

		public void BindTo(byte[] bufferToBoundTo)
		{
			this.byteBuffer = bufferToBoundTo;
			this.numberOfBytes = 0;
		}

		public static implicit operator byte[](WaveBuffer waveBuffer)
		{
			return waveBuffer.byteBuffer;
		}

		public static implicit operator float[](WaveBuffer waveBuffer)
		{
			return waveBuffer.floatBuffer;
		}

		public static implicit operator int[](WaveBuffer waveBuffer)
		{
			return waveBuffer.intBuffer;
		}

		public static implicit operator short[](WaveBuffer waveBuffer)
		{
			return waveBuffer.shortBuffer;
		}

		public void Clear()
		{
			Array.Clear(this.byteBuffer, 0, this.byteBuffer.Length);
		}

		public void Copy(Array destinationArray)
		{
			Array.Copy(this.byteBuffer, destinationArray, this.numberOfBytes);
		}

		private int CheckValidityCount(string argName, int value, int sizeOfValue)
		{
			int num = value * sizeOfValue;
			if (num % 4 != 0)
			{
				throw new ArgumentOutOfRangeException(argName, string.Format("{0} cannot set a count ({1}) that is not 4 bytes aligned ", argName, num));
			}
			if (value < 0 || value > this.byteBuffer.Length / sizeOfValue)
			{
				throw new ArgumentOutOfRangeException(argName, string.Format("{0} cannot set a count that exceed max count {1}", argName, this.byteBuffer.Length / sizeOfValue));
			}
			return num;
		}
	}
}
