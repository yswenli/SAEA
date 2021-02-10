using System;
using System.IO;
using System.Text;

namespace SAEA.Audio.NAudio.Midi
{
	public class SequencerSpecificEvent : MetaEvent
	{
		private byte[] data;

		public byte[] Data
		{
			get
			{
				return this.data;
			}
			set
			{
				this.data = value;
				this.metaDataLength = this.data.Length;
			}
		}

		public SequencerSpecificEvent(BinaryReader br, int length)
		{
			this.data = br.ReadBytes(length);
		}

		public SequencerSpecificEvent(byte[] data, long absoluteTime) : base(MetaEventType.SequencerSpecific, data.Length, absoluteTime)
		{
			this.data = data;
		}

		public override MidiEvent Clone()
		{
			return new SequencerSpecificEvent((byte[])this.data.Clone(), base.AbsoluteTime);
		}

		public override string ToString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(base.ToString());
			stringBuilder.Append(" ");
			byte[] array = this.data;
			int i;
			for (i = 0; i < array.Length; i++)
			{
				byte b = array[i];
				stringBuilder.AppendFormat("{0:X2} ", b);
			}
			StringBuilder expr_4B = stringBuilder;
			i = expr_4B.Length;
			expr_4B.Length = i - 1;
			return stringBuilder.ToString();
		}

		public override void Export(ref long absoluteTime, BinaryWriter writer)
		{
			base.Export(ref absoluteTime, writer);
			writer.Write(this.data);
		}
	}
}
