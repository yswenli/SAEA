using System;

namespace SAEA.Audio.NSpeex
{
	public class InvalidFormatException : Exception
	{
		public InvalidFormatException(string message) : base(message)
		{
		}
	}
}
