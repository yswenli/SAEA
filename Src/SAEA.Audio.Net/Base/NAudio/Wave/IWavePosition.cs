using System;

namespace SAEA.Audio.Base.NAudio.Wave
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
