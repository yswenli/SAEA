using System;
using System.IO;

namespace SAEA.Audio.NAudio.Midi
{
	public class MidiEvent : ICloneable
	{
		private MidiCommandCode commandCode;

		private int channel;

		private int deltaTime;

		private long absoluteTime;

		public virtual int Channel
		{
			get
			{
				return this.channel;
			}
			set
			{
				if (value < 1 || value > 16)
				{
					throw new ArgumentOutOfRangeException("value", value, string.Format("Channel must be 1-16 (Got {0})", value));
				}
				this.channel = value;
			}
		}

		public int DeltaTime
		{
			get
			{
				return this.deltaTime;
			}
		}

		public long AbsoluteTime
		{
			get
			{
				return this.absoluteTime;
			}
			set
			{
				this.absoluteTime = value;
			}
		}

		public MidiCommandCode CommandCode
		{
			get
			{
				return this.commandCode;
			}
		}

		public static MidiEvent FromRawMessage(int rawMessage)
		{
			long num = 0L;
			int num2 = rawMessage & 255;
			int num3 = rawMessage >> 8 & 255;
			int num4 = rawMessage >> 16 & 255;
			int num5 = 1;
			MidiCommandCode midiCommandCode;
			if ((num2 & 240) == 240)
			{
				midiCommandCode = (MidiCommandCode)num2;
			}
			else
			{
				midiCommandCode = (MidiCommandCode)(num2 & 240);
				num5 = (num2 & 15) + 1;
			}
			if (midiCommandCode <= MidiCommandCode.ControlChange)
			{
				MidiEvent result;
				if (midiCommandCode <= MidiCommandCode.NoteOn)
				{
					if (midiCommandCode != MidiCommandCode.NoteOff && midiCommandCode != MidiCommandCode.NoteOn)
					{
						goto IL_173;
					}
				}
				else if (midiCommandCode != MidiCommandCode.KeyAfterTouch)
				{
					if (midiCommandCode != MidiCommandCode.ControlChange)
					{
						goto IL_173;
					}
					result = new ControlChangeEvent(num, num5, (MidiController)num3, num4);
					return result;
				}
				if (num4 > 0 && midiCommandCode == MidiCommandCode.NoteOn)
				{
					result = new NoteOnEvent(num, num5, num3, num4, 0);
					return result;
				}
				result = new NoteEvent(num, num5, midiCommandCode, num3, num4);
				return result;
			}
			else if (midiCommandCode <= MidiCommandCode.ChannelAfterTouch)
			{
				if (midiCommandCode == MidiCommandCode.PatchChange)
				{
					MidiEvent result = new PatchChangeEvent(num, num5, num3);
					return result;
				}
				if (midiCommandCode == MidiCommandCode.ChannelAfterTouch)
				{
					MidiEvent result = new ChannelAfterTouchEvent(num, num5, num3);
					return result;
				}
			}
			else
			{
				if (midiCommandCode == MidiCommandCode.PitchWheelChange)
				{
					MidiEvent result = new PitchWheelChangeEvent(num, num5, num3 + (num4 << 7));
					return result;
				}
				if (midiCommandCode != MidiCommandCode.Sysex)
				{
					switch (midiCommandCode)
					{
					case MidiCommandCode.TimingClock:
					case MidiCommandCode.StartSequence:
					case MidiCommandCode.ContinueSequence:
					case MidiCommandCode.StopSequence:
					case MidiCommandCode.AutoSensing:
					{
						MidiEvent result = new MidiEvent(num, num5, midiCommandCode);
						return result;
					}
					}
				}
			}
			IL_173:
			throw new FormatException(string.Format("Unsupported MIDI Command Code for Raw Message {0}", midiCommandCode));
		}

		public static MidiEvent ReadNextEvent(BinaryReader br, MidiEvent previous)
		{
			int num = MidiEvent.ReadVarInt(br);
			int num2 = 1;
			byte b = br.ReadByte();
			MidiCommandCode midiCommandCode;
			if ((b & 128) == 0)
			{
				midiCommandCode = previous.CommandCode;
				num2 = previous.Channel;
				Stream expr_2D = br.BaseStream;
				long position = expr_2D.Position;
				expr_2D.Position = position - 1L;
			}
			else if ((b & 240) == 240)
			{
				midiCommandCode = (MidiCommandCode)b;
			}
			else
			{
				midiCommandCode = (MidiCommandCode)(b & 240);
				num2 = (int)((b & 15) + 1);
			}
			MidiEvent midiEvent;
			if (midiCommandCode <= MidiCommandCode.ControlChange)
			{
				if (midiCommandCode <= MidiCommandCode.NoteOn)
				{
					if (midiCommandCode != MidiCommandCode.NoteOff)
					{
						if (midiCommandCode != MidiCommandCode.NoteOn)
						{
							goto IL_154;
						}
						midiEvent = new NoteOnEvent(br);
						goto IL_16A;
					}
				}
				else if (midiCommandCode != MidiCommandCode.KeyAfterTouch)
				{
					if (midiCommandCode != MidiCommandCode.ControlChange)
					{
						goto IL_154;
					}
					midiEvent = new ControlChangeEvent(br);
					goto IL_16A;
				}
				midiEvent = new NoteEvent(br);
				goto IL_16A;
			}
			if (midiCommandCode <= MidiCommandCode.ChannelAfterTouch)
			{
				if (midiCommandCode == MidiCommandCode.PatchChange)
				{
					midiEvent = new PatchChangeEvent(br);
					goto IL_16A;
				}
				if (midiCommandCode == MidiCommandCode.ChannelAfterTouch)
				{
					midiEvent = new ChannelAfterTouchEvent(br);
					goto IL_16A;
				}
			}
			else
			{
				if (midiCommandCode == MidiCommandCode.PitchWheelChange)
				{
					midiEvent = new PitchWheelChangeEvent(br);
					goto IL_16A;
				}
				if (midiCommandCode == MidiCommandCode.Sysex)
				{
					midiEvent = SysexEvent.ReadSysexEvent(br);
					goto IL_16A;
				}
				switch (midiCommandCode)
				{
				case MidiCommandCode.TimingClock:
				case MidiCommandCode.StartSequence:
				case MidiCommandCode.ContinueSequence:
				case MidiCommandCode.StopSequence:
					midiEvent = new MidiEvent();
					goto IL_16A;
				case MidiCommandCode.MetaEvent:
					midiEvent = MetaEvent.ReadMetaEvent(br);
					goto IL_16A;
				}
			}
			IL_154:
			throw new FormatException(string.Format("Unsupported MIDI Command Code {0:X2}", (byte)midiCommandCode));
			IL_16A:
			midiEvent.channel = num2;
			midiEvent.deltaTime = num;
			midiEvent.commandCode = midiCommandCode;
			return midiEvent;
		}

		public virtual int GetAsShortMessage()
		{
			return (int)((byte)(this.channel - 1) + this.commandCode);
		}

		protected MidiEvent()
		{
		}

		public MidiEvent(long absoluteTime, int channel, MidiCommandCode commandCode)
		{
			this.absoluteTime = absoluteTime;
			this.Channel = channel;
			this.commandCode = commandCode;
		}

		public virtual MidiEvent Clone()
		{
			return (MidiEvent)base.MemberwiseClone();
		}

		object ICloneable.Clone()
		{
			return this.Clone();
		}

		public static bool IsNoteOff(MidiEvent midiEvent)
		{
			if (midiEvent == null)
			{
				return false;
			}
			if (midiEvent.CommandCode == MidiCommandCode.NoteOn)
			{
				return ((NoteEvent)midiEvent).Velocity == 0;
			}
			return midiEvent.CommandCode == MidiCommandCode.NoteOff;
		}

		public static bool IsNoteOn(MidiEvent midiEvent)
		{
			return midiEvent != null && midiEvent.CommandCode == MidiCommandCode.NoteOn && ((NoteEvent)midiEvent).Velocity > 0;
		}

		public static bool IsEndTrack(MidiEvent midiEvent)
		{
			if (midiEvent != null)
			{
				MetaEvent metaEvent = midiEvent as MetaEvent;
				if (metaEvent != null)
				{
					return metaEvent.MetaEventType == MetaEventType.EndTrack;
				}
			}
			return false;
		}

		public override string ToString()
		{
			if (this.commandCode >= MidiCommandCode.Sysex)
			{
				return string.Format("{0} {1}", this.absoluteTime, this.commandCode);
			}
			return string.Format("{0} {1} Ch: {2}", this.absoluteTime, this.commandCode, this.channel);
		}

		public static int ReadVarInt(BinaryReader br)
		{
			int num = 0;
			for (int i = 0; i < 4; i++)
			{
				byte b = br.ReadByte();
				num <<= 7;
				num += (int)(b & 127);
				if ((b & 128) == 0)
				{
					return num;
				}
			}
			throw new FormatException("Invalid Var Int");
		}

		public static void WriteVarInt(BinaryWriter writer, int value)
		{
			if (value < 0)
			{
				throw new ArgumentOutOfRangeException("value", value, "Cannot write a negative Var Int");
			}
			if (value > 268435455)
			{
				throw new ArgumentOutOfRangeException("value", value, "Maximum allowed Var Int is 0x0FFFFFFF");
			}
			int i = 0;
			byte[] array = new byte[4];
			do
			{
				array[i++] = (byte)(value & 127);
				value >>= 7;
			}
			while (value > 0);
			while (i > 0)
			{
				i--;
				if (i > 0)
				{
					writer.Write(array[i] | 128);
				}
				else
				{
					writer.Write(array[i]);
				}
			}
		}

		public virtual void Export(ref long absoluteTime, BinaryWriter writer)
		{
			if (this.absoluteTime < absoluteTime)
			{
				throw new FormatException("Can't export unsorted MIDI events");
			}
			MidiEvent.WriteVarInt(writer, (int)(this.absoluteTime - absoluteTime));
			absoluteTime = this.absoluteTime;
			int num = (int)this.commandCode;
			if (this.commandCode != MidiCommandCode.MetaEvent)
			{
				num += this.channel - 1;
			}
			writer.Write((byte)num);
		}
	}
}
