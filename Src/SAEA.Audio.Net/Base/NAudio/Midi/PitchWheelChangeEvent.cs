using System;
using System.IO;

namespace SAEA.Audio.NAudio.Midi
{
	public class PitchWheelChangeEvent : MidiEvent
	{
		private int pitch;

		public int Pitch
		{
			get
			{
				return this.pitch;
			}
			set
			{
				if (value < 0 || value >= 16384)
				{
					throw new ArgumentOutOfRangeException("value", "Pitch value must be in the range 0 - 0x3FFF");
				}
				this.pitch = value;
			}
		}

		public PitchWheelChangeEvent(BinaryReader br)
		{
			byte b = br.ReadByte();
			byte b2 = br.ReadByte();
			if ((b & 128) != 0)
			{
				throw new FormatException("Invalid pitchwheelchange byte 1");
			}
			if ((b2 & 128) != 0)
			{
				throw new FormatException("Invalid pitchwheelchange byte 2");
			}
			this.pitch = (int)b + ((int)b2 << 7);
		}

		public PitchWheelChangeEvent(long absoluteTime, int channel, int pitchWheel) : base(absoluteTime, channel, MidiCommandCode.PitchWheelChange)
		{
			this.Pitch = pitchWheel;
		}

		public override string ToString()
		{
			return string.Format("{0} Pitch {1} ({2})", base.ToString(), this.pitch, this.pitch - 8192);
		}

		public override int GetAsShortMessage()
		{
			return base.GetAsShortMessage() + ((this.pitch & 127) << 8) + ((this.pitch >> 7 & 127) << 16);
		}

		public override void Export(ref long absoluteTime, BinaryWriter writer)
		{
			base.Export(ref absoluteTime, writer);
			writer.Write((byte)(this.pitch & 127));
			writer.Write((byte)(this.pitch >> 7 & 127));
		}
	}
}
