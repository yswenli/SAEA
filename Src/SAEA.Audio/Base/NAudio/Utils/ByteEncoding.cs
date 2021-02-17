using System;
using System.Text;

namespace SAEA.Audio.Base.NAudio.Utils
{
	public class ByteEncoding : Encoding
	{
		public static readonly ByteEncoding Instance = new ByteEncoding();

		private ByteEncoding()
		{
		}

		public override int GetByteCount(char[] chars, int index, int count)
		{
			return count;
		}

		public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
		{
			for (int i = 0; i < charCount; i++)
			{
				bytes[byteIndex + i] = (byte)chars[charIndex + i];
			}
			return charCount;
		}

		public override int GetCharCount(byte[] bytes, int index, int count)
		{
			for (int i = 0; i < count; i++)
			{
				if (bytes[index + i] == 0)
				{
					return i;
				}
			}
			return count;
		}

		public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
		{
			for (int i = 0; i < byteCount; i++)
			{
				byte b = bytes[byteIndex + i];
				if (b == 0)
				{
					return i;
				}
				chars[charIndex + i] = (char)b;
			}
			return byteCount;
		}

		public override int GetMaxCharCount(int byteCount)
		{
			return byteCount;
		}

		public override int GetMaxByteCount(int charCount)
		{
			return charCount;
		}
	}
}
