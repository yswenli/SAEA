using System;
using System.IO;
using System.Text;

namespace SAEA.Audio.NSpeex
{
	public abstract class AudioFileWriter
	{
		public abstract void Close();

		public abstract void Open(Stream stream);

		public void Open(string filename)
		{
			this.Open(new FileStream(filename, FileMode.Create));
		}

		public abstract void WriteHeader(string comment);

		public abstract void WritePacket(byte[] data, int offset, int len);

		protected static int WriteSpeexHeader(BinaryWriter buf, int sampleRate, int mode, int channels, bool vbr, int nframes)
		{
			buf.Write(Encoding.UTF8.GetBytes("Speex   "));
			buf.Write(Encoding.UTF8.GetBytes("speex-1.0"));
			for (int i = 0; i < 11; i++)
			{
				buf.Write(0);
			}
			buf.Write(1);
			buf.Write(80);
			buf.Write(sampleRate);
			buf.Write(mode);
			buf.Write(4);
			buf.Write(channels);
			buf.Write(-1);
			buf.Write(160 << mode);
			buf.Write(vbr ? 1 : 0);
			buf.Write(nframes);
			buf.Write(0);
			buf.Write(0);
			buf.Write(0);
			return 80;
		}

		protected static byte[] BuildSpeexHeader(int sampleRate, int mode, int channels, bool vbr, int nframes)
		{
			byte[] array = new byte[80];
			AudioFileWriter.WriteSpeexHeader(new BinaryWriter(new MemoryStream(array)), sampleRate, mode, channels, vbr, nframes);
			return array;
		}

		protected static int WriteSpeexComment(BinaryWriter buf, string comment)
		{
			int length = comment.Length;
			buf.Write(length);
			buf.Write(Encoding.UTF8.GetBytes(comment), 0, length);
			buf.Write(0);
			return length + 8;
		}

		protected static byte[] BuildSpeexComment(string comment)
		{
			byte[] array = new byte[comment.Length + 8];
			AudioFileWriter.WriteSpeexComment(new BinaryWriter(new MemoryStream(array)), comment);
			return array;
		}
	}
}
