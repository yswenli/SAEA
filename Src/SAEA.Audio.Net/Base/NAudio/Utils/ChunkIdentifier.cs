using System;
using System.Text;

namespace SAEA.Audio.NAudio.Utils
{
	public class ChunkIdentifier
	{
		public static int ChunkIdentifierToInt32(string s)
		{
			if (s.Length != 4)
			{
				throw new ArgumentException("Must be a four character string");
			}
			byte[] expr_1F = Encoding.UTF8.GetBytes(s);
			if (expr_1F.Length != 4)
			{
				throw new ArgumentException("Must encode to exactly four bytes");
			}
			return BitConverter.ToInt32(expr_1F, 0);
		}
	}
}
