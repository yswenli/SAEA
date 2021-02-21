using System;
using System.IO;

namespace SAEA.Audio.Base.NAudio.SoundFont
{
	internal class SFVersionBuilder : StructureBuilder<SFVersion>
	{
		public override int Length
		{
			get
			{
				return 4;
			}
		}

		public override SFVersion Read(BinaryReader br)
		{
			SFVersion sFVersion = new SFVersion();
			sFVersion.Major = br.ReadInt16();
			sFVersion.Minor = br.ReadInt16();
			this.data.Add(sFVersion);
			return sFVersion;
		}

		public override void Write(BinaryWriter bw, SFVersion v)
		{
			bw.Write(v.Major);
			bw.Write(v.Minor);
		}
	}
}
