using System;

namespace SAEA.Audio.NAudio.CoreAudioApi
{
	public struct PropertyKey
	{
		public Guid formatId;

		public int propertyId;

		public PropertyKey(Guid formatId, int propertyId)
		{
			this.formatId = formatId;
			this.propertyId = propertyId;
		}
	}
}
