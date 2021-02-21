using System;

namespace SAEA.Audio.Base.NAudio.Wave.SampleProviders
{
	public class SquareRootPanStrategy : IPanStrategy
	{
		public StereoSamplePair GetMultipliers(float pan)
		{
			float num = (-pan + 1f) / 2f;
			float left = (float)Math.Sqrt((double)num);
			float right = (float)Math.Sqrt((double)(1f - num));
			return new StereoSamplePair
			{
				Left = left,
				Right = right
			};
		}
	}
}
