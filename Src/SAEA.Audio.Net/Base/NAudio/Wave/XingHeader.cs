using System;

namespace SAEA.Audio.NAudio.Wave
{
	public class XingHeader
	{
		[Flags]
		private enum XingHeaderOptions
		{
			Frames = 1,
			Bytes = 2,
			Toc = 4,
			VbrScale = 8
		}

		private static int[] sr_table = new int[]
		{
			44100,
			48000,
			32000,
			99999
		};

		private int vbrScale = -1;

		private int startOffset;

		private int endOffset;

		private int tocOffset = -1;

		private int framesOffset = -1;

		private int bytesOffset = -1;

		private Mp3Frame frame;

		public int Frames
		{
			get
			{
				if (this.framesOffset == -1)
				{
					return -1;
				}
				return XingHeader.ReadBigEndian(this.frame.RawData, this.framesOffset);
			}
			set
			{
				if (this.framesOffset == -1)
				{
					throw new InvalidOperationException("Frames flag is not set");
				}
				this.WriteBigEndian(this.frame.RawData, this.framesOffset, value);
			}
		}

		public int Bytes
		{
			get
			{
				if (this.bytesOffset == -1)
				{
					return -1;
				}
				return XingHeader.ReadBigEndian(this.frame.RawData, this.bytesOffset);
			}
			set
			{
				if (this.framesOffset == -1)
				{
					throw new InvalidOperationException("Bytes flag is not set");
				}
				this.WriteBigEndian(this.frame.RawData, this.bytesOffset, value);
			}
		}

		public int VbrScale
		{
			get
			{
				return this.vbrScale;
			}
		}

		public Mp3Frame Mp3Frame
		{
			get
			{
				return this.frame;
			}
		}

		private static int ReadBigEndian(byte[] buffer, int offset)
		{
			return (((int)buffer[offset] << 8 | (int)buffer[offset + 1]) << 8 | (int)buffer[offset + 2]) << 8 | (int)buffer[offset + 3];
		}

		private void WriteBigEndian(byte[] buffer, int offset, int value)
		{
			byte[] bytes = BitConverter.GetBytes(value);
			for (int i = 0; i < 4; i++)
			{
				buffer[offset + 3 - i] = bytes[i];
			}
		}

		public static XingHeader LoadXingHeader(Mp3Frame frame)
		{
			XingHeader xingHeader = new XingHeader();
			xingHeader.frame = frame;
			int num;
			if (frame.MpegVersion == MpegVersion.Version1)
			{
				if (frame.ChannelMode != ChannelMode.Mono)
				{
					num = 36;
				}
				else
				{
					num = 21;
				}
			}
			else
			{
				if (frame.MpegVersion != MpegVersion.Version2)
				{
					return null;
				}
				if (frame.ChannelMode != ChannelMode.Mono)
				{
					num = 21;
				}
				else
				{
					num = 13;
				}
			}
			if (frame.RawData[num] == 88 && frame.RawData[num + 1] == 105 && frame.RawData[num + 2] == 110 && frame.RawData[num + 3] == 103)
			{
				xingHeader.startOffset = num;
				num += 4;
				int arg_9E_0 = XingHeader.ReadBigEndian(frame.RawData, num);
				num += 4;
				if ((arg_9E_0 & 1) != 0)
				{
					xingHeader.framesOffset = num;
					num += 4;
				}
				if ((arg_9E_0 & 2) != 0)
				{
					xingHeader.bytesOffset = num;
					num += 4;
				}
				if ((arg_9E_0 & 4) != 0)
				{
					xingHeader.tocOffset = num;
					num += 100;
				}
				if ((arg_9E_0 & 8) != 0)
				{
					xingHeader.vbrScale = XingHeader.ReadBigEndian(frame.RawData, num);
					num += 4;
				}
				xingHeader.endOffset = num;
				return xingHeader;
			}
			return null;
		}

		private XingHeader()
		{
		}
	}
}
