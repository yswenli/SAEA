using System;

namespace SAEA.Audio.Base.NAudio.SoundFont
{
	public class Instrument
	{
		private string name;

		internal ushort startInstrumentZoneIndex;

		internal ushort endInstrumentZoneIndex;

		private Zone[] zones;

		public string Name
		{
			get
			{
				return this.name;
			}
			set
			{
				this.name = value;
			}
		}

		public Zone[] Zones
		{
			get
			{
				return this.zones;
			}
			set
			{
				this.zones = value;
			}
		}

		public override string ToString()
		{
			return this.name;
		}
	}
}
