using System;
using System.IO;

namespace SAEA.Audio.NAudio.Midi
{
	public class TempoEvent : MetaEvent
	{
		private int microsecondsPerQuarterNote;

		public int MicrosecondsPerQuarterNote
		{
			get
			{
				return this.microsecondsPerQuarterNote;
			}
			set
			{
				this.microsecondsPerQuarterNote = value;
			}
		}

		public double Tempo
		{
			get
			{
				return 60000000.0 / (double)this.microsecondsPerQuarterNote;
			}
			set
			{
				this.microsecondsPerQuarterNote = (int)(60000000.0 / value);
			}
		}

		public TempoEvent(BinaryReader br, int length)
		{
			if (length != 3)
			{
				throw new FormatException("Invalid tempo length");
			}
			this.microsecondsPerQuarterNote = ((int)br.ReadByte() << 16) + ((int)br.ReadByte() << 8) + (int)br.ReadByte();
		}

		public TempoEvent(int microsecondsPerQuarterNote, long absoluteTime) : base(MetaEventType.SetTempo, 3, absoluteTime)
		{
			this.microsecondsPerQuarterNote = microsecondsPerQuarterNote;
		}

		public override MidiEvent Clone()
		{
			return (TempoEvent)base.MemberwiseClone();
		}

		public override string ToString()
		{
			return string.Format("{0} {2}bpm ({1})", base.ToString(), this.microsecondsPerQuarterNote, 60000000 / this.microsecondsPerQuarterNote);
		}

		public override void Export(ref long absoluteTime, BinaryWriter writer)
		{
			base.Export(ref absoluteTime, writer);
			writer.Write((byte)(this.microsecondsPerQuarterNote >> 16 & 255));
			writer.Write((byte)(this.microsecondsPerQuarterNote >> 8 & 255));
			writer.Write((byte)(this.microsecondsPerQuarterNote & 255));
		}
	}
}
