using System;
using System.Collections.Generic;

namespace SAEA.Audio.NAudio.Midi
{
	public class MidiEventComparer : IComparer<MidiEvent>
	{
		public int Compare(MidiEvent x, MidiEvent y)
		{
			long num = x.AbsoluteTime;
			long num2 = y.AbsoluteTime;
			if (num == num2)
			{
				MetaEvent metaEvent = x as MetaEvent;
				MetaEvent metaEvent2 = y as MetaEvent;
				if (metaEvent != null)
				{
					if (metaEvent.MetaEventType == MetaEventType.EndTrack)
					{
						num = 9223372036854775807L;
					}
					else
					{
						num = -9223372036854775808L;
					}
				}
				if (metaEvent2 != null)
				{
					if (metaEvent2.MetaEventType == MetaEventType.EndTrack)
					{
						num2 = 9223372036854775807L;
					}
					else
					{
						num2 = -9223372036854775808L;
					}
				}
			}
			return num.CompareTo(num2);
		}
	}
}
