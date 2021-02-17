using System;

namespace SAEA.Audio.Base.NSpeex
{
	public class InvalidFormatException : Exception
	{
		public InvalidFormatException(string message) : base(message)
		{
		}
	}
}
