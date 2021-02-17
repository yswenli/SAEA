using System;

namespace SAEA.Audio.Base.NSpeex
{
	internal class Bits
	{
		public const int DefaultBufferSize = 1024;

		private byte[] bytes;

		private int bytePtr;

		private int bitPtr;

		private int nbBits;

		public int BufferSize
		{
			get
			{
				return this.bytePtr + ((this.bitPtr > 0) ? 1 : 0);
			}
		}

		public Bits()
		{
			this.bytes = new byte[1024];
			this.bytePtr = 0;
			this.bitPtr = 0;
		}

		public void Advance(int n)
		{
			this.bytePtr += n >> 3;
			this.bitPtr += (n & 7);
			if (this.bitPtr > 7)
			{
				this.bitPtr -= 8;
				this.bytePtr++;
			}
		}

		public int Peek()
		{
			return (this.bytes[this.bytePtr] & 255) >> 7 - this.bitPtr & 1;
		}

		public void ReadFrom(byte[] newbytes, int offset, int len)
		{
			if (this.bytes.Length < len)
			{
				this.bytes = new byte[len];
			}
			for (int i = 0; i < len; i++)
			{
				this.bytes[i] = newbytes[offset + i];
			}
			this.bytePtr = 0;
			this.bitPtr = 0;
			this.nbBits = len * 8;
		}

		public int BitsRemaining()
		{
			return this.nbBits - (this.bytePtr * 8 + this.bitPtr);
		}

		public int Unpack(int nbBits)
		{
			int num = 0;
			while (nbBits != 0)
			{
				num <<= 1;
				num |= ((this.bytes[this.bytePtr] & 255) >> 7 - this.bitPtr & 1);
				this.bitPtr++;
				if (this.bitPtr == 8)
				{
					this.bitPtr = 0;
					this.bytePtr++;
				}
				nbBits--;
			}
			return num;
		}

		public void Pack(int data, int nbBits)
		{
			while (this.bytePtr + (nbBits + this.bitPtr >> 3) >= this.bytes.Length)
			{
				int num = this.bytes.Length * 2;
				byte[] destinationArray = new byte[num];
				Array.Copy(this.bytes, 0, destinationArray, 0, this.bytes.Length);
				this.bytes = destinationArray;
			}
			while (nbBits > 0)
			{
				int num2 = data >> nbBits - 1 & 1;
				byte[] expr_6C_cp_0 = this.bytes;
				int expr_6C_cp_1 = this.bytePtr;
				expr_6C_cp_0[expr_6C_cp_1] |= (byte)(num2 << 7 - this.bitPtr);
				this.bitPtr++;
				if (this.bitPtr == 8)
				{
					this.bitPtr = 0;
					this.bytePtr++;
				}
				nbBits--;
			}
		}

		public void InsertTerminator()
		{
			if (this.bitPtr > 0)
			{
				this.Pack(0, 1);
			}
			while (this.bitPtr != 0)
			{
				this.Pack(1, 1);
			}
		}

		public int Write(byte[] buffer, int offset, int maxBytes)
		{
			int num = this.bitPtr;
			int num2 = this.bytePtr;
			byte[] array = this.bytes;
			this.InsertTerminator();
			this.bitPtr = num;
			this.bytePtr = num2;
			this.bytes = array;
			if (maxBytes > this.BufferSize)
			{
				maxBytes = this.BufferSize;
			}
			Array.Copy(this.bytes, 0, buffer, offset, maxBytes);
			return maxBytes;
		}

		public void Reset()
		{
			Array.Clear(this.bytes, 0, this.bytes.Length);
			this.bytePtr = 0;
			this.bitPtr = 0;
		}
	}
}
