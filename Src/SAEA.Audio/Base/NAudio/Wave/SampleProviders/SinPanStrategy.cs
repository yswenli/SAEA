using System;

namespace SAEA.Audio.Base.NAudio.Wave.SampleProviders
{
	public class SinPanStrategy : IPanStrategy
	{
		private const float HalfPi = 1.57079637f;

		public StereoSamplePair GetMultipliers(float pan)
		{
			float expr_0E = (-pan + 1f) / 2f;
			float left = (float)Math.Sin((double)(expr_0E * 1.57079637f));
			float right = (float)Math.Cos((double)(expr_0E * 1.57079637f));
			return new StereoSamplePair
			{
				Left = left,
				Right = right
			};
		}
	}
}
