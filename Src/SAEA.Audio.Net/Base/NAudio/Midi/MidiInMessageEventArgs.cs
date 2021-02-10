using System;

namespace SAEA.Audio.NAudio.Midi
{
	public class MidiInMessageEventArgs : EventArgs
	{
		public int RawMessage
		{
			get;
			private set;
		}

		public MidiEvent MidiEvent
		{
			get;
			private set;
		}

		public int Timestamp
		{
			get;
			private set;
		}

		public MidiInMessageEventArgs(int message, int timestamp)
		{
			this.RawMessage = message;
			this.Timestamp = timestamp;
			try
			{
				this.MidiEvent = MidiEvent.FromRawMessage(message);
			}
			catch (Exception)
			{
			}
		}
	}
}
