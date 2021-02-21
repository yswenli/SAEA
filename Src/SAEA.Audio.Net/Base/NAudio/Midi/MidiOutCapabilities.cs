using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Midi
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	public struct MidiOutCapabilities
	{
		[Flags]
		private enum MidiOutCapabilityFlags
		{
			Volume = 1,
			LeftRightVolume = 2,
			PatchCaching = 4,
			Stream = 8
		}

		private short manufacturerId;

		private short productId;

		private int driverVersion;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		private string productName;

		private short wTechnology;

		private short wVoices;

		private short wNotes;

		private ushort wChannelMask;

		private MidiOutCapabilities.MidiOutCapabilityFlags dwSupport;

		private const int MaxProductNameLength = 32;

		public Manufacturers Manufacturer
		{
			get
			{
				return (Manufacturers)this.manufacturerId;
			}
		}

		public short ProductId
		{
			get
			{
				return this.productId;
			}
		}

		public string ProductName
		{
			get
			{
				return this.productName;
			}
		}

		public int Voices
		{
			get
			{
				return (int)this.wVoices;
			}
		}

		public int Notes
		{
			get
			{
				return (int)this.wNotes;
			}
		}

		public bool SupportsAllChannels
		{
			get
			{
				return this.wChannelMask == 65535;
			}
		}

		public bool SupportsPatchCaching
		{
			get
			{
				return (this.dwSupport & MidiOutCapabilities.MidiOutCapabilityFlags.PatchCaching) > (MidiOutCapabilities.MidiOutCapabilityFlags)0;
			}
		}

		public bool SupportsSeparateLeftAndRightVolume
		{
			get
			{
				return (this.dwSupport & MidiOutCapabilities.MidiOutCapabilityFlags.LeftRightVolume) > (MidiOutCapabilities.MidiOutCapabilityFlags)0;
			}
		}

		public bool SupportsMidiStreamOut
		{
			get
			{
				return (this.dwSupport & MidiOutCapabilities.MidiOutCapabilityFlags.Stream) > (MidiOutCapabilities.MidiOutCapabilityFlags)0;
			}
		}

		public bool SupportsVolumeControl
		{
			get
			{
				return (this.dwSupport & MidiOutCapabilities.MidiOutCapabilityFlags.Volume) > (MidiOutCapabilities.MidiOutCapabilityFlags)0;
			}
		}

		public MidiOutTechnology Technology
		{
			get
			{
				return (MidiOutTechnology)this.wTechnology;
			}
		}

		public bool SupportsChannel(int channel)
		{
			return ((int)this.wChannelMask & 1 << channel - 1) > 0;
		}
	}
}
