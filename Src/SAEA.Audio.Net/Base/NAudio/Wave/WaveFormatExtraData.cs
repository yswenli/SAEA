using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Wave
{
	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public class WaveFormatExtraData : WaveFormat
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 100)]
		private byte[] extraData = new byte[100];

		public byte[] ExtraData
		{
			get
			{
				return this.extraData;
			}
		}

		internal WaveFormatExtraData()
		{
		}

		public WaveFormatExtraData(BinaryReader reader) : base(reader)
		{
			this.ReadExtraData(reader);
		}

		internal void ReadExtraData(BinaryReader reader)
		{
			if (this.extraSize > 0)
			{
				reader.Read(this.extraData, 0, (int)this.extraSize);
			}
		}

		public override void Serialize(BinaryWriter writer)
		{
			base.Serialize(writer);
			if (this.extraSize > 0)
			{
				writer.Write(this.extraData, 0, (int)this.extraSize);
			}
		}
	}
}
