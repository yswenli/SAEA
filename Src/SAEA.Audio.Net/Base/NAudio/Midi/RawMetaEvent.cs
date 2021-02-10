using System;
using System.IO;
using System.Text;

namespace SAEA.Audio.NAudio.Midi
{
	public class RawMetaEvent : MetaEvent
	{
		public byte[] Data
		{
			get;
			set;
		}

		public RawMetaEvent(MetaEventType metaEventType, long absoluteTime, byte[] data) : base(metaEventType, (data != null) ? data.Length : 0, absoluteTime)
		{
			this.Data = data;
		}

		public override MidiEvent Clone()
		{
			MetaEventType arg_23_0 = base.MetaEventType;
			long arg_23_1 = base.AbsoluteTime;
			byte[] expr_12 = this.Data;
			return new RawMetaEvent(arg_23_0, arg_23_1, (byte[])((expr_12 != null) ? expr_12.Clone() : null));
		}

		public override string ToString()
		{
			StringBuilder stringBuilder = new StringBuilder().Append(base.ToString());
			byte[] data = this.Data;
			for (int i = 0; i < data.Length; i++)
			{
				byte b = data[i];
				stringBuilder.AppendFormat(" {0:X2}", b);
			}
			return stringBuilder.ToString();
		}

		public override void Export(ref long absoluteTime, BinaryWriter writer)
		{
			base.Export(ref absoluteTime, writer);
			if (this.Data == null)
			{
				return;
			}
			writer.Write(this.Data, 0, this.Data.Length);
		}
	}
}
