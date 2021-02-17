using System;

namespace SAEA.Audio.Base.NAudio.SoundFont
{
	public enum SFSampleLink : ushort
	{
		MonoSample = 1,
		RightSample,
		LeftSample = 4,
		LinkedSample = 8,
		RomMonoSample = 32769,
		RomRightSample,
		RomLeftSample = 32772,
		RomLinkedSample = 32776
	}
}
