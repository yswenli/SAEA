using System;
using System.IO;

namespace SAEA.Audio.Base.NAudio.Midi
{
	public class NoteOnEvent : NoteEvent
	{
		private NoteEvent offEvent;

		public NoteEvent OffEvent
		{
			get
			{
				return this.offEvent;
			}
			set
			{
				if (!MidiEvent.IsNoteOff(value))
				{
					throw new ArgumentException("OffEvent must be a valid MIDI note off event");
				}
				if (value.NoteNumber != this.NoteNumber)
				{
					throw new ArgumentException("Note Off Event must be for the same note number");
				}
				if (value.Channel != this.Channel)
				{
					throw new ArgumentException("Note Off Event must be for the same channel");
				}
				this.offEvent = value;
			}
		}

		public override int NoteNumber
		{
			get
			{
				return base.NoteNumber;
			}
			set
			{
				base.NoteNumber = value;
				if (this.OffEvent != null)
				{
					this.OffEvent.NoteNumber = this.NoteNumber;
				}
			}
		}

		public override int Channel
		{
			get
			{
				return base.Channel;
			}
			set
			{
				base.Channel = value;
				if (this.OffEvent != null)
				{
					this.OffEvent.Channel = this.Channel;
				}
			}
		}

		public int NoteLength
		{
			get
			{
				return (int)(this.offEvent.AbsoluteTime - base.AbsoluteTime);
			}
			set
			{
				if (value < 0)
				{
					throw new ArgumentException("NoteLength must be 0 or greater");
				}
				this.offEvent.AbsoluteTime = base.AbsoluteTime + (long)value;
			}
		}

		public NoteOnEvent(BinaryReader br) : base(br)
		{
		}

		public NoteOnEvent(long absoluteTime, int channel, int noteNumber, int velocity, int duration) : base(absoluteTime, channel, MidiCommandCode.NoteOn, noteNumber, velocity)
		{
			this.OffEvent = new NoteEvent(absoluteTime, channel, MidiCommandCode.NoteOff, noteNumber, 0);
			this.NoteLength = duration;
		}

		public override MidiEvent Clone()
		{
			return new NoteOnEvent(base.AbsoluteTime, this.Channel, this.NoteNumber, base.Velocity, this.NoteLength);
		}

		public override string ToString()
		{
			if (base.Velocity == 0 && this.OffEvent == null)
			{
				return string.Format("{0} (Note Off)", base.ToString());
			}
			return string.Format("{0} Len: {1}", base.ToString(), (this.OffEvent == null) ? "?" : this.NoteLength.ToString());
		}
	}
}
