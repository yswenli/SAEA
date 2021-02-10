using System;
using System.Security.Cryptography;

namespace SAEA.Audio.NSpeex
{
	public class OggCrc : HashAlgorithm
	{
		private const uint Polynomial = 79764919u;

		private const uint Seed = 0u;

		private static uint[] lookupTable;

		private uint hash;

		public override int HashSize
		{
			get
			{
				return 32;
			}
		}

		static OggCrc()
		{
			OggCrc.lookupTable = new uint[256];
			uint num = 0u;
			while ((ulong)num < (ulong)((long)OggCrc.lookupTable.Length))
			{
				uint num2 = num << 24;
				for (int i = 0; i < 8; i++)
				{
					if ((num2 & 2147483648u) != 0u)
					{
						num2 = (num2 << 1 ^ 79764919u);
					}
					else
					{
						num2 <<= 1;
					}
				}
				OggCrc.lookupTable[(int)((UIntPtr)num)] = num2;
				num += 1u;
			}
		}

		public override void Initialize()
		{
			this.hash = 0u;
		}

		protected override void HashCore(byte[] array, int ibStart, int cbSize)
		{
			for (int i = 0; i < cbSize; i++)
			{
				this.hash = (this.hash << 8 ^ OggCrc.lookupTable[(int)((UIntPtr)(((uint)array[i + ibStart] ^ this.hash >> 24) & 255u))]);
			}
		}

		protected override byte[] HashFinal()
		{
			return new byte[]
			{
				(byte)(this.hash & 255u),
				(byte)(this.hash >> 8 & 255u),
				(byte)(this.hash >> 16 & 255u),
				(byte)(this.hash >> 24 & 255u)
			};
		}
	}
}
