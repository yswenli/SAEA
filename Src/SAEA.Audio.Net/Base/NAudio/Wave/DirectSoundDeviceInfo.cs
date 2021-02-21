using System;

namespace SAEA.Audio.Base.NAudio.Wave
{
	public class DirectSoundDeviceInfo
	{
		public Guid Guid
		{
			get;
			set;
		}

		public string Description
		{
			get;
			set;
		}

		public string ModuleName
		{
			get;
			set;
		}
	}
}
