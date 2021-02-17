using System;

namespace SAEA.Audio.Base.NAudio.Utils
{
	public static class BufferHelpers
	{
		public static byte[] Ensure(byte[] buffer, int bytesRequired)
		{
			if (buffer == null || buffer.Length < bytesRequired)
			{
				buffer = new byte[bytesRequired];
			}
			return buffer;
		}

		public static float[] Ensure(float[] buffer, int samplesRequired)
		{
			if (buffer == null || buffer.Length < samplesRequired)
			{
				buffer = new float[samplesRequired];
			}
			return buffer;
		}
	}
}
