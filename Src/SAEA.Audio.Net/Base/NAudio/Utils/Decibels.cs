using System;

namespace SAEA.Audio.NAudio.Utils
{
	public class Decibels
	{
		private const double LOG_2_DB = 8.6858896380650368;

		private const double DB_2_LOG = 0.11512925464970228;

		public static double LinearToDecibels(double lin)
		{
			return Math.Log(lin) * 8.6858896380650368;
		}

		public static double DecibelsToLinear(double dB)
		{
			return Math.Exp(dB * 0.11512925464970228);
		}
	}
}
