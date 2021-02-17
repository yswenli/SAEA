using System;
using System.IO;

namespace SAEA.Audio.Base.NAudio.FileFormats.Map
{
	internal class MapBlockHeader
	{
		private int length;

		private int value2;

		private short value3;

		private short value4;

		public int Length
		{
			get
			{
				return this.length;
			}
		}

		public static MapBlockHeader Read(BinaryReader reader)
		{
			return new MapBlockHeader
			{
				length = reader.ReadInt32(),
				value2 = reader.ReadInt32(),
				value3 = reader.ReadInt16(),
				value4 = reader.ReadInt16()
			};
		}

		public override string ToString()
		{
			return string.Format("{0} {1:X8} {2:X4} {3:X4}", new object[]
			{
				this.length,
				this.value2,
				this.value3,
				this.value4
			});
		}
	}
}
