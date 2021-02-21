using System;

namespace SAEA.Audio.Base.NAudio.Wave
{
	public interface IWaveBuffer
	{
		byte[] ByteBuffer
		{
			get;
		}

		float[] FloatBuffer
		{
			get;
		}

		short[] ShortBuffer
		{
			get;
		}

		int[] IntBuffer
		{
			get;
		}

		int MaxSize
		{
			get;
		}

		int ByteBufferCount
		{
			get;
		}

		int FloatBufferCount
		{
			get;
		}

		int ShortBufferCount
		{
			get;
		}

		int IntBufferCount
		{
			get;
		}
	}
}
