using System;
using System.IO;

namespace SAEA.Audio.Base.NAudio.Midi
{
	public class ChannelAfterTouchEvent : MidiEvent
	{
		private byte afterTouchPressure;

		public int AfterTouchPressure
		{
			get
			{
				return (int)this.afterTouchPressure;
			}
			set
			{
				if (value < 0 || value > 127)
				{
					throw new ArgumentOutOfRangeException("value", "After touch pressure must be in the range 0-127");
				}
				this.afterTouchPressure = (byte)value;
			}
		}

		public ChannelAfterTouchEvent(BinaryReader br)
		{
			this.afterTouchPressure = br.ReadByte();
			if ((this.afterTouchPressure & 128) != 0)
			{
				throw new FormatException("Invalid afterTouchPressure");
			}
		}

		public ChannelAfterTouchEvent(long absoluteTime, int channel, int afterTouchPressure) : base(absoluteTime, channel, MidiCommandCode.ChannelAfterTouch)
		{
			this.AfterTouchPressure = afterTouchPressure;
		}

		public override void Export(ref long absoluteTime, BinaryWriter writer)
		{
			base.Export(ref absoluteTime, writer);
			writer.Write(this.afterTouchPressure);
		}
	}
}
