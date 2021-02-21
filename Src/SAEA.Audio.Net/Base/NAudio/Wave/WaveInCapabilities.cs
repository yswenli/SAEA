using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Wave
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	public struct WaveInCapabilities
	{
		private short manufacturerId;

		private short productId;

		private int driverVersion;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		private string productName;

		private SupportedWaveFormat supportedFormats;

		private short channels;

		private short reserved;

		private Guid manufacturerGuid;

		private Guid productGuid;

		private Guid nameGuid;

		private const int MaxProductNameLength = 32;

		public int Channels
		{
			get
			{
				return (int)this.channels;
			}
		}

		public string ProductName
		{
			get
			{
				return this.productName;
			}
		}

		public Guid NameGuid
		{
			get
			{
				return this.nameGuid;
			}
		}

		public Guid ProductGuid
		{
			get
			{
				return this.productGuid;
			}
		}

		public Guid ManufacturerGuid
		{
			get
			{
				return this.manufacturerGuid;
			}
		}

		public bool SupportsWaveFormat(SupportedWaveFormat waveFormat)
		{
			return (this.supportedFormats & waveFormat) == waveFormat;
		}
	}
}
