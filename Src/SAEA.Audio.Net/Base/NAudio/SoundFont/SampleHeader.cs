using System;

namespace SAEA.Audio.NAudio.SoundFont
{
	public class SampleHeader
	{
		public string SampleName;

		public uint Start;

		public uint End;

		public uint StartLoop;

		public uint EndLoop;

		public uint SampleRate;

		public byte OriginalPitch;

		public sbyte PitchCorrection;

		public ushort SampleLink;

		public SFSampleLink SFSampleLink;

		public override string ToString()
		{
			return this.SampleName;
		}
	}
}
