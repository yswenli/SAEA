using System;
using System.IO;

namespace SAEA.Audio.Base.NAudio.Midi
{
	public class NoteEvent : MidiEvent
	{
		private int noteNumber;

		private int velocity;

		private static readonly string[] NoteNames = new string[]
		{
			"C",
			"C#",
			"D",
			"D#",
			"E",
			"F",
			"F#",
			"G",
			"G#",
			"A",
			"A#",
			"B"
		};

		public virtual int NoteNumber
		{
			get
			{
				return this.noteNumber;
			}
			set
			{
				if (value < 0 || value > 127)
				{
					throw new ArgumentOutOfRangeException("value", "Note number must be in the range 0-127");
				}
				this.noteNumber = value;
			}
		}

		public int Velocity
		{
			get
			{
				return this.velocity;
			}
			set
			{
				if (value < 0 || value > 127)
				{
					throw new ArgumentOutOfRangeException("value", "Velocity must be in the range 0-127");
				}
				this.velocity = value;
			}
		}

		public string NoteName
		{
			get
			{
				if (this.Channel != 16 && this.Channel != 10)
				{
					int num = this.noteNumber / 12;
					return string.Format("{0}{1}", NoteEvent.NoteNames[this.noteNumber % 12], num);
				}
				switch (this.noteNumber)
				{
				case 35:
					return "Acoustic Bass Drum";
				case 36:
					return "Bass Drum 1";
				case 37:
					return "Side Stick";
				case 38:
					return "Acoustic Snare";
				case 39:
					return "Hand Clap";
				case 40:
					return "Electric Snare";
				case 41:
					return "Low Floor Tom";
				case 42:
					return "Closed Hi-Hat";
				case 43:
					return "High Floor Tom";
				case 44:
					return "Pedal Hi-Hat";
				case 45:
					return "Low Tom";
				case 46:
					return "Open Hi-Hat";
				case 47:
					return "Low-Mid Tom";
				case 48:
					return "Hi-Mid Tom";
				case 49:
					return "Crash Cymbal 1";
				case 50:
					return "High Tom";
				case 51:
					return "Ride Cymbal 1";
				case 52:
					return "Chinese Cymbal";
				case 53:
					return "Ride Bell";
				case 54:
					return "Tambourine";
				case 55:
					return "Splash Cymbal";
				case 56:
					return "Cowbell";
				case 57:
					return "Crash Cymbal 2";
				case 58:
					return "Vibraslap";
				case 59:
					return "Ride Cymbal 2";
				case 60:
					return "Hi Bongo";
				case 61:
					return "Low Bongo";
				case 62:
					return "Mute Hi Conga";
				case 63:
					return "Open Hi Conga";
				case 64:
					return "Low Conga";
				case 65:
					return "High Timbale";
				case 66:
					return "Low Timbale";
				case 67:
					return "High Agogo";
				case 68:
					return "Low Agogo";
				case 69:
					return "Cabasa";
				case 70:
					return "Maracas";
				case 71:
					return "Short Whistle";
				case 72:
					return "Long Whistle";
				case 73:
					return "Short Guiro";
				case 74:
					return "Long Guiro";
				case 75:
					return "Claves";
				case 76:
					return "Hi Wood Block";
				case 77:
					return "Low Wood Block";
				case 78:
					return "Mute Cuica";
				case 79:
					return "Open Cuica";
				case 80:
					return "Mute Triangle";
				case 81:
					return "Open Triangle";
				default:
					return string.Format("Drum {0}", this.noteNumber);
				}
			}
		}

		public NoteEvent(BinaryReader br)
		{
			this.NoteNumber = (int)br.ReadByte();
			this.velocity = (int)br.ReadByte();
			if (this.velocity > 127)
			{
				this.velocity = 127;
			}
		}

		public NoteEvent(long absoluteTime, int channel, MidiCommandCode commandCode, int noteNumber, int velocity) : base(absoluteTime, channel, commandCode)
		{
			this.NoteNumber = noteNumber;
			this.Velocity = velocity;
		}

		public override int GetAsShortMessage()
		{
			return base.GetAsShortMessage() + (this.noteNumber << 8) + (this.velocity << 16);
		}

		public override string ToString()
		{
			return string.Format("{0} {1} Vel:{2}", base.ToString(), this.NoteName, this.Velocity);
		}

		public override void Export(ref long absoluteTime, BinaryWriter writer)
		{
			base.Export(ref absoluteTime, writer);
			writer.Write((byte)this.noteNumber);
			writer.Write((byte)this.velocity);
		}
	}
}
