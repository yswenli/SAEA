using System;

namespace SAEA.Audio.NAudio.Wave
{
	public interface IWavePosition
	{
		WaveFormat OutputWaveFormat
		{
			get;
		}

		long GetPosition();
	}
}
