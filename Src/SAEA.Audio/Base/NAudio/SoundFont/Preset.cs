using System;

namespace SAEA.Audio.Base.NAudio.SoundFont
{
	public class Preset
	{
		private string name;

		private ushort patchNumber;

		private ushort bank;

		internal ushort startPresetZoneIndex;

		internal ushort endPresetZoneIndex;

		internal uint library;

		internal uint genre;

		internal uint morphology;

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

		public ushort PatchNumber
		{
			get
			{
				return this.patchNumber;
			}
			set
			{
				this.patchNumber = value;
			}
		}

		public ushort Bank
		{
			get
			{
				return this.bank;
			}
			set
			{
				this.bank = value;
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
			return string.Format("{0}-{1} {2}", this.bank, this.patchNumber, this.name);
		}
	}
}
