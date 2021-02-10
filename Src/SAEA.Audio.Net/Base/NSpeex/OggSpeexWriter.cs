using System;
using System.IO;
using System.Text;

namespace SAEA.Audio.NSpeex
{
	public class OggSpeexWriter : AudioFileWriter
	{
		public const int PACKETS_PER_OGG_PAGE = 250;

		private BinaryWriter xout;

		private readonly int mode;

		private readonly int sampleRate;

		private readonly int channels;

		private readonly int nframes;

		private readonly bool vbr;

		private int streamSerialNumber;

		private byte[] dataBuffer;

		private int dataBufferPtr;

		private byte[] headerBuffer;

		private int headerBufferPtr;

		private int pageCount;

		private int packetCount;

		private long granulepos;

		public int SerialNumber
		{
			set
			{
				this.streamSerialNumber = value;
			}
		}

		public OggSpeexWriter(int mode, int sampleRate, int channels, int nframes, bool vbr)
		{
			this.streamSerialNumber = new Random().Next();
			this.dataBuffer = new byte[65565];
			this.dataBufferPtr = 0;
			this.headerBuffer = new byte[255];
			this.headerBufferPtr = 0;
			this.pageCount = 0;
			this.packetCount = 0;
			this.granulepos = 0L;
			this.mode = mode;
			this.sampleRate = sampleRate;
			this.channels = channels;
			this.nframes = nframes;
			this.vbr = vbr;
		}

		public override void Close()
		{
			this.Flush(true);
			this.xout.Close();
		}

		public override void Open(Stream stream)
		{
			this.xout = new BinaryWriter(stream);
		}

		private static int WriteOggPageHeader(BinaryWriter buf, int headerType, long granulepos, int streamSerialNumber, int pageCount, int packetCount, byte[] packetSizes)
		{
			buf.Write(Encoding.UTF8.GetBytes("OggS"));
			buf.Write(0);
			buf.Write((byte)headerType);
			buf.Write(granulepos);
			buf.Write(streamSerialNumber);
			buf.Write(pageCount);
			buf.Write(0);
			buf.Write((byte)packetCount);
			buf.Write(packetSizes, 0, packetCount);
			return packetCount + 27;
		}

		private static byte[] BuildOggPageHeader(int headerType, long granulepos, int streamSerialNumber, int pageCount, int packetCount, byte[] packetSizes)
		{
			byte[] array = new byte[packetCount + 27];
			OggSpeexWriter.WriteOggPageHeader(new BinaryWriter(new MemoryStream(array)), headerType, granulepos, streamSerialNumber, pageCount, packetCount, packetSizes);
			return array;
		}

		public override void WriteHeader(string comment)
		{
			OggCrc oggCrc = new OggCrc();
			byte[] array = OggSpeexWriter.BuildOggPageHeader(2, 0L, this.streamSerialNumber, this.pageCount++, 1, new byte[]
			{
				80
			});
			byte[] array2 = AudioFileWriter.BuildSpeexHeader(this.sampleRate, this.mode, this.channels, this.vbr, this.nframes);
			oggCrc.Initialize();
			oggCrc.TransformBlock(array, 0, array.Length, array, 0);
			oggCrc.TransformFinalBlock(array2, 0, array2.Length);
			this.xout.Write(array, 0, 22);
			this.xout.Write(oggCrc.Hash, 0, oggCrc.HashSize / 8);
			this.xout.Write(array, 26, array.Length - 26);
			this.xout.Write(array2, 0, array2.Length);
			array = OggSpeexWriter.BuildOggPageHeader(0, 0L, this.streamSerialNumber, this.pageCount++, 1, new byte[]
			{
				(byte)(comment.Length + 8)
			});
			array2 = AudioFileWriter.BuildSpeexComment(comment);
			oggCrc.Initialize();
			oggCrc.TransformBlock(array, 0, array.Length, array, 0);
			oggCrc.TransformFinalBlock(array2, 0, array2.Length);
			this.xout.Write(array, 0, 22);
			this.xout.Write(oggCrc.Hash, 0, oggCrc.HashSize / 8);
			this.xout.Write(array, 26, array.Length - 26);
			this.xout.Write(array2, 0, array2.Length);
		}

		public override void WritePacket(byte[] data, int offset, int len)
		{
			if (len <= 0)
			{
				return;
			}
			if (this.packetCount > 250)
			{
				this.Flush(false);
			}
			Array.Copy(data, offset, this.dataBuffer, this.dataBufferPtr, len);
			this.dataBufferPtr += len;
			this.headerBuffer[this.headerBufferPtr++] = (byte)len;
			this.packetCount++;
			this.granulepos += (long)(this.nframes * ((this.mode == 2) ? 640 : ((this.mode == 1) ? 320 : 160)));
		}

		private void Flush(bool eos)
		{
			OggCrc oggCrc = new OggCrc();
			byte[] array = OggSpeexWriter.BuildOggPageHeader(eos ? 4 : 0, this.granulepos, this.streamSerialNumber, this.pageCount++, this.packetCount, this.headerBuffer);
			oggCrc.Initialize();
			oggCrc.TransformBlock(array, 0, array.Length, array, 0);
			oggCrc.TransformFinalBlock(this.dataBuffer, 0, this.dataBufferPtr);
			this.xout.Write(array, 0, 22);
			this.xout.Write(oggCrc.Hash, 0, oggCrc.HashSize / 8);
			this.xout.Write(array, 26, array.Length - 26);
			this.xout.Write(this.dataBuffer, 0, this.dataBufferPtr);
			this.dataBufferPtr = 0;
			this.headerBufferPtr = 0;
			this.packetCount = 0;
		}
	}
}
