using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Mixer
{
	public class Mixer
	{
		private MixerInterop.MIXERCAPS caps;

		private IntPtr mixerHandle;

		private MixerFlags mixerHandleType;

		public static int NumberOfDevices
		{
			get
			{
				return MixerInterop.mixerGetNumDevs();
			}
		}

		public int DestinationCount
		{
			get
			{
				return (int)this.caps.cDestinations;
			}
		}

		public string Name
		{
			get
			{
				return this.caps.szPname;
			}
		}

		public Manufacturers Manufacturer
		{
			get
			{
				return (Manufacturers)this.caps.wMid;
			}
		}

		public int ProductID
		{
			get
			{
				return (int)this.caps.wPid;
			}
		}

		public IEnumerable<MixerLine> Destinations
		{
			get
			{
				int num;
				for (int i = 0; i < this.DestinationCount; i = num + 1)
				{
					yield return this.GetDestination(i);
					num = i;
				}
				yield break;
			}
		}

		public static IEnumerable<Mixer> Mixers
		{
			get
			{
				int num;
				for (int i = 0; i < Mixer.NumberOfDevices; i = num + 1)
				{
					yield return new Mixer(i);
					num = i;
				}
				yield break;
			}
		}

		public Mixer(int mixerIndex)
		{
			if (mixerIndex < 0 || mixerIndex >= Mixer.NumberOfDevices)
			{
				throw new ArgumentOutOfRangeException("mixerID");
			}
			this.caps = default(MixerInterop.MIXERCAPS);
			MmException.Try(MixerInterop.mixerGetDevCaps((IntPtr)mixerIndex, ref this.caps, Marshal.SizeOf(this.caps)), "mixerGetDevCaps");
			this.mixerHandle = (IntPtr)mixerIndex;
			this.mixerHandleType = MixerFlags.Mixer;
		}

		public MixerLine GetDestination(int destinationIndex)
		{
			if (destinationIndex < 0 || destinationIndex >= this.DestinationCount)
			{
				throw new ArgumentOutOfRangeException("destinationIndex");
			}
			return new MixerLine(this.mixerHandle, destinationIndex, this.mixerHandleType);
		}
	}
}
