using System;
using System.IO;

namespace SAEA.Audio.NAudio.Midi
{
	public class MetaEvent : MidiEvent
	{
		private MetaEventType metaEvent;

		internal int metaDataLength;

		public MetaEventType MetaEventType
		{
			get
			{
				return this.metaEvent;
			}
		}

		protected MetaEvent()
		{
		}

		public MetaEvent(MetaEventType metaEventType, int metaDataLength, long absoluteTime) : base(absoluteTime, 1, MidiCommandCode.MetaEvent)
		{
			this.metaEvent = metaEventType;
			this.metaDataLength = metaDataLength;
		}

		public override MidiEvent Clone()
		{
			return new MetaEvent(this.metaEvent, this.metaDataLength, base.AbsoluteTime);
		}

		public static MetaEvent ReadMetaEvent(BinaryReader br)
		{
			MetaEventType metaEventType = (MetaEventType)br.ReadByte();
			int num = MidiEvent.ReadVarInt(br);
			MetaEvent metaEvent = new MetaEvent();
			if (metaEventType <= MetaEventType.SetTempo)
			{
				switch (metaEventType)
				{
				case MetaEventType.TrackSequenceNumber:
					metaEvent = new TrackSequenceNumberEvent(br, num);
					goto IL_E7;
				case MetaEventType.TextEvent:
				case MetaEventType.Copyright:
				case MetaEventType.SequenceTrackName:
				case MetaEventType.TrackInstrumentName:
				case MetaEventType.Lyric:
				case MetaEventType.Marker:
				case MetaEventType.CuePoint:
				case MetaEventType.ProgramName:
				case MetaEventType.DeviceName:
					metaEvent = new TextEvent(br, num);
					goto IL_E7;
				default:
					if (metaEventType != MetaEventType.EndTrack)
					{
						if (metaEventType == MetaEventType.SetTempo)
						{
							metaEvent = new TempoEvent(br, num);
							goto IL_E7;
						}
					}
					else
					{
						if (num != 0)
						{
							throw new FormatException("End track length");
						}
						goto IL_E7;
					}
					break;
				}
			}
			else if (metaEventType <= MetaEventType.TimeSignature)
			{
				if (metaEventType == MetaEventType.SmpteOffset)
				{
					metaEvent = new SmpteOffsetEvent(br, num);
					goto IL_E7;
				}
				if (metaEventType == MetaEventType.TimeSignature)
				{
					metaEvent = new TimeSignatureEvent(br, num);
					goto IL_E7;
				}
			}
			else
			{
				if (metaEventType == MetaEventType.KeySignature)
				{
					metaEvent = new KeySignatureEvent(br, num);
					goto IL_E7;
				}
				if (metaEventType == MetaEventType.SequencerSpecific)
				{
					metaEvent = new SequencerSpecificEvent(br, num);
					goto IL_E7;
				}
			}
			byte[] array = br.ReadBytes(num);
			if (array.Length != num)
			{
				throw new FormatException("Failed to read metaevent's data fully");
			}
			return new RawMetaEvent(metaEventType, 0L, array);
			IL_E7:
			metaEvent.metaEvent = metaEventType;
			metaEvent.metaDataLength = num;
			return metaEvent;
		}

		public override string ToString()
		{
			return string.Format("{0} {1}", base.AbsoluteTime, this.metaEvent);
		}

		public override void Export(ref long absoluteTime, BinaryWriter writer)
		{
			base.Export(ref absoluteTime, writer);
			writer.Write((byte)this.metaEvent);
			MidiEvent.WriteVarInt(writer, this.metaDataLength);
		}
	}
}
