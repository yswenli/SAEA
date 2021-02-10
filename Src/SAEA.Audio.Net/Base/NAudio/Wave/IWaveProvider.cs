using System;

namespace SAEA.Audio.NAudio.Wave
{
	public interface IWaveProvider
	{
		WaveFormat WaveFormat
		{
			get;
		}

		int Read(byte[] buffer, int offset, int count);
	}
}
