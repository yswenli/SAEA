using System;

namespace SAEA.Audio.Base.NAudio.Wave.SampleProviders
{
	public interface IPanStrategy
	{
		StereoSamplePair GetMultipliers(float pan);
	}
}
