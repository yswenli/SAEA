using System;
using System.IO;

namespace SAEA.Audio.Base.NAudio.Midi
{
	public class TimeSignatureEvent : MetaEvent
	{
		private byte numerator;

		private byte denominator;

		private byte ticksInMetronomeClick;

		private byte no32ndNotesInQuarterNote;

		public int Numerator
		{
			get
			{
				return (int)this.numerator;
			}
		}

		public int Denominator
		{
			get
			{
				return (int)this.denominator;
			}
		}

		public int TicksInMetronomeClick
		{
			get
			{
				return (int)this.ticksInMetronomeClick;
			}
		}

		public int No32ndNotesInQuarterNote
		{
			get
			{
				return (int)this.no32ndNotesInQuarterNote;
			}
		}

		public string TimeSignature
		{
			get
			{
				string arg = string.Format("Unknown ({0})", this.denominator);
				switch (this.denominator)
				{
				case 1:
					arg = "2";
					break;
				case 2:
					arg = "4";
					break;
				case 3:
					arg = "8";
					break;
				case 4:
					arg = "16";
					break;
				case 5:
					arg = "32";
					break;
				}
				return string.Format("{0}/{1}", this.numerator, arg);
			}
		}

		public TimeSignatureEvent(BinaryReader br, int length)
		{
			if (length != 4)
			{
				throw new FormatException(string.Format("Invalid time signature length: Got {0}, expected 4", length));
			}
			this.numerator = br.ReadByte();
			this.denominator = br.ReadByte();
			this.ticksInMetronomeClick = br.ReadByte();
			this.no32ndNotesInQuarterNote = br.ReadByte();
		}

		public TimeSignatureEvent(long absoluteTime, int numerator, int denominator, int ticksInMetronomeClick, int no32ndNotesInQuarterNote) : base(MetaEventType.TimeSignature, 4, absoluteTime)
		{
			this.numerator = (byte)numerator;
			this.denominator = (byte)denominator;
			this.ticksInMetronomeClick = (byte)ticksInMetronomeClick;
			this.no32ndNotesInQuarterNote = (byte)no32ndNotesInQuarterNote;
		}

		public override MidiEvent Clone()
		{
			return (TimeSignatureEvent)base.MemberwiseClone();
		}

		public override string ToString()
		{
			return string.Format("{0} {1} TicksInClick:{2} 32ndsInQuarterNote:{3}", new object[]
			{
				base.ToString(),
				this.TimeSignature,
				this.ticksInMetronomeClick,
				this.no32ndNotesInQuarterNote
			});
		}

		public override void Export(ref long absoluteTime, BinaryWriter writer)
		{
			base.Export(ref absoluteTime, writer);
			writer.Write(this.numerator);
			writer.Write(this.denominator);
			writer.Write(this.ticksInMetronomeClick);
			writer.Write(this.no32ndNotesInQuarterNote);
		}
	}
}
