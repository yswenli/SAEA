using System;

namespace SAEA.Audio.NAudio.Wave.SampleProviders
{
	public interface IPanStrategy
	{
		StereoSamplePair GetMultipliers(float pan);
	}
}
