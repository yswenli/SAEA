using System;
using System.Text;

namespace SAEA.Audio.NAudio.Utils
{
	public static class ByteArrayExtensions
	{
		public static bool IsEntirelyNull(byte[] buffer)
		{
			for (int i = 0; i < buffer.Length; i++)
			{
				if (buffer[i] != 0)
				{
					return false;
				}
			}
			return true;
		}

		public static string DescribeAsHex(byte[] buffer, string separator, int bytesPerLine)
		{
			StringBuilder stringBuilder = new StringBuilder();
			int num = 0;
			for (int i = 0; i < buffer.Length; i++)
			{
				byte b = buffer[i];
				stringBuilder.AppendFormat("{0:X2}{1}", b, separator);
				if (++num % bytesPerLine == 0)
				{
					stringBuilder.Append("\r\n");
				}
			}
			stringBuilder.Append("\r\n");
			return stringBuilder.ToString();
		}

		public static string DecodeAsString(byte[] buffer, int offset, int length, Encoding encoding)
		{
			for (int i = 0; i < length; i++)
			{
				if (buffer[offset + i] == 0)
				{
					length = i;
				}
			}
			return encoding.GetString(buffer, offset, length);
		}

		public static byte[] Concat(params byte[][] byteArrays)
		{
			int num = 0;
			for (int i = 0; i < byteArrays.Length; i++)
			{
				byte[] array = byteArrays[i];
				num += array.Length;
			}
			if (num <= 0)
			{
				return new byte[0];
			}
			byte[] array2 = new byte[num];
			int num2 = 0;
			for (int i = 0; i < byteArrays.Length; i++)
			{
				byte[] array3 = byteArrays[i];
				Array.Copy(array3, 0, array2, num2, array3.Length);
				num2 += array3.Length;
			}
			return array2;
		}
	}
}
