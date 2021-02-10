using SAEA.Audio.NAudio.Utils;
using System;
using System.Collections;
using System.Collections.Generic;

namespace SAEA.Audio.NAudio.Midi
{
	public class MidiEventCollection : IEnumerable<IList<MidiEvent>>, IEnumerable
	{
		private int midiFileType;

		private List<IList<MidiEvent>> trackEvents;

		private int deltaTicksPerQuarterNote;

		private long startAbsoluteTime;

		public int Tracks
		{
			get
			{
				return this.trackEvents.Count;
			}
		}

		public long StartAbsoluteTime
		{
			get
			{
				return this.startAbsoluteTime;
			}
			set
			{
				this.startAbsoluteTime = value;
			}
		}

		public int DeltaTicksPerQuarterNote
		{
			get
			{
				return this.deltaTicksPerQuarterNote;
			}
		}

		public IList<MidiEvent> this[int trackNumber]
		{
			get
			{
				return this.trackEvents[trackNumber];
			}
		}

		public int MidiFileType
		{
			get
			{
				return this.midiFileType;
			}
			set
			{
				if (this.midiFileType != value)
				{
					this.midiFileType = value;
					if (value == 0)
					{
						this.FlattenToOneTrack();
						return;
					}
					this.ExplodeToManyTracks();
				}
			}
		}

		public MidiEventCollection(int midiFileType, int deltaTicksPerQuarterNote)
		{
			this.midiFileType = midiFileType;
			this.deltaTicksPerQuarterNote = deltaTicksPerQuarterNote;
			this.startAbsoluteTime = 0L;
			this.trackEvents = new List<IList<MidiEvent>>();
		}

		public IList<MidiEvent> GetTrackEvents(int trackNumber)
		{
			return this.trackEvents[trackNumber];
		}

		public IList<MidiEvent> AddTrack()
		{
			return this.AddTrack(null);
		}

		public IList<MidiEvent> AddTrack(IList<MidiEvent> initialEvents)
		{
			List<MidiEvent> list = new List<MidiEvent>();
			if (initialEvents != null)
			{
				list.AddRange(initialEvents);
			}
			this.trackEvents.Add(list);
			return list;
		}

		public void RemoveTrack(int track)
		{
			this.trackEvents.RemoveAt(track);
		}

		public void Clear()
		{
			this.trackEvents.Clear();
		}

		public void AddEvent(MidiEvent midiEvent, int originalTrack)
		{
			if (this.midiFileType == 0)
			{
				this.EnsureTracks(1);
				this.trackEvents[0].Add(midiEvent);
				return;
			}
			if (originalTrack == 0)
			{
				MidiCommandCode commandCode = midiEvent.CommandCode;
				if (commandCode <= MidiCommandCode.KeyAfterTouch)
				{
					if (commandCode != MidiCommandCode.NoteOff && commandCode != MidiCommandCode.NoteOn && commandCode != MidiCommandCode.KeyAfterTouch)
					{
						goto IL_A1;
					}
				}
				else if (commandCode <= MidiCommandCode.PatchChange)
				{
					if (commandCode != MidiCommandCode.ControlChange && commandCode != MidiCommandCode.PatchChange)
					{
						goto IL_A1;
					}
				}
				else if (commandCode != MidiCommandCode.ChannelAfterTouch && commandCode != MidiCommandCode.PitchWheelChange)
				{
					goto IL_A1;
				}
				this.EnsureTracks(midiEvent.Channel + 1);
				this.trackEvents[midiEvent.Channel].Add(midiEvent);
				return;
				IL_A1:
				this.EnsureTracks(1);
				this.trackEvents[0].Add(midiEvent);
				return;
			}
			this.EnsureTracks(originalTrack + 1);
			this.trackEvents[originalTrack].Add(midiEvent);
		}

		private void EnsureTracks(int count)
		{
			for (int i = this.trackEvents.Count; i < count; i++)
			{
				this.trackEvents.Add(new List<MidiEvent>());
			}
		}

		private void ExplodeToManyTracks()
		{
			IEnumerable<MidiEvent> arg_12_0 = this.trackEvents[0];
			this.Clear();
			foreach (MidiEvent current in arg_12_0)
			{
				this.AddEvent(current, 0);
			}
			this.PrepareForExport();
		}

		private void FlattenToOneTrack()
		{
			bool flag = false;
			for (int i = 1; i < this.trackEvents.Count; i++)
			{
				foreach (MidiEvent current in this.trackEvents[i])
				{
					if (!MidiEvent.IsEndTrack(current))
					{
						this.trackEvents[0].Add(current);
						flag = true;
					}
				}
			}
			for (int j = this.trackEvents.Count - 1; j > 0; j--)
			{
				this.RemoveTrack(j);
			}
			if (flag)
			{
				this.PrepareForExport();
			}
		}

		public void PrepareForExport()
		{
			MidiEventComparer comparer = new MidiEventComparer();
			using (List<IList<MidiEvent>>.Enumerator enumerator = this.trackEvents.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					List<MidiEvent> list = (List<MidiEvent>)enumerator.Current;
					MergeSort.Sort<MidiEvent>(list, comparer);
					int i = 0;
					while (i < list.Count - 1)
					{
						if (MidiEvent.IsEndTrack(list[i]))
						{
							list.RemoveAt(i);
						}
						else
						{
							i++;
						}
					}
				}
			}
			int j = 0;
			while (j < this.trackEvents.Count)
			{
				IList<MidiEvent> list2 = this.trackEvents[j];
				if (list2.Count == 0)
				{
					this.RemoveTrack(j);
				}
				else if (list2.Count == 1 && MidiEvent.IsEndTrack(list2[0]))
				{
					this.RemoveTrack(j);
				}
				else
				{
					if (!MidiEvent.IsEndTrack(list2[list2.Count - 1]))
					{
						list2.Add(new MetaEvent(MetaEventType.EndTrack, 0, list2[list2.Count - 1].AbsoluteTime));
					}
					j++;
				}
			}
		}

		public IEnumerator<IList<MidiEvent>> GetEnumerator()
		{
			return this.trackEvents.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.trackEvents.GetEnumerator();
		}
	}
}
