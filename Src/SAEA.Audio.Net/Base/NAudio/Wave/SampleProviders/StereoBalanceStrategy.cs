using System;

namespace SAEA.Audio.NAudio.Wave.SampleProviders
{
	public class StereoBalanceStrategy : IPanStrategy
	{
		public StereoSamplePair GetMultipliers(float pan)
		{
			float left = (pan <= 0f) ? 1f : ((1f - pan) / 2f);
			float right = (pan >= 0f) ? 1f : ((pan + 1f) / 2f);
			return new StereoSamplePair
			{
				Left = left,
				Right = right
			};
		}
	}
}
