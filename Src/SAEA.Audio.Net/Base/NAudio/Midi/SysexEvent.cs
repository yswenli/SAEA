using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SAEA.Audio.NAudio.Midi
{
	public class SysexEvent : MidiEvent
	{
		private byte[] data;

		public static SysexEvent ReadSysexEvent(BinaryReader br)
		{
			SysexEvent sysexEvent = new SysexEvent();
			List<byte> list = new List<byte>();
			bool flag = true;
			while (flag)
			{
				byte b = br.ReadByte();
				if (b == 247)
				{
					flag = false;
				}
				else
				{
					list.Add(b);
				}
			}
			sysexEvent.data = list.ToArray();
			return sysexEvent;
		}

		public override MidiEvent Clone()
		{
			SysexEvent expr_05 = new SysexEvent();
			byte[] expr_0C = this.data;
			expr_05.data = (byte[])((expr_0C != null) ? expr_0C.Clone() : null);
			return expr_05;
		}

		public override string ToString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			byte[] array = this.data;
			for (int i = 0; i < array.Length; i++)
			{
				byte b = array[i];
				stringBuilder.AppendFormat("{0:X2} ", b);
			}
			return string.Format("{0} Sysex: {1} bytes\r\n{2}", base.AbsoluteTime, this.data.Length, stringBuilder.ToString());
		}

		public override void Export(ref long absoluteTime, BinaryWriter writer)
		{
			base.Export(ref absoluteTime, writer);
			writer.Write(this.data, 0, this.data.Length);
			writer.Write(247);
		}
	}
}
