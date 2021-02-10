using System;
using System.IO;

namespace SAEA.Audio.NSpeex
{
	public class RawWriter : AudioFileWriter
	{
		private Stream xout;

		public override void Close()
		{
			this.xout.Close();
		}

		public override void Open(Stream stream)
		{
			this.xout = stream;
		}

		public override void WriteHeader(string comment)
		{
		}

		public override void WritePacket(byte[] data, int offset, int len)
		{
			this.xout.Write(data, offset, len);
		}
	}
}
