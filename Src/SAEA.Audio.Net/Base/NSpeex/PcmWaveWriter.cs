using System;
using System.IO;
using System.Text;

namespace SAEA.Audio.NSpeex
{
	public class PcmWaveWriter : AudioFileWriter
	{
		protected const short WAVE_FORMAT_PCM = 1;

		protected const short WAVE_FORMAT_SPEEX = -24311;

		public static readonly int[,,] WAVE_FRAME_SIZES = new int[,,]
		{
			{
				{
					8,
					8,
					8,
					1,
					1,
					2,
					2,
					2,
					2,
					2,
					2
				},
				{
					2,
					1,
					1,
					7,
					7,
					8,
					8,
					8,
					8,
					3,
					3
				}
			},
			{
				{
					8,
					8,
					8,
					2,
					1,
					1,
					2,
					2,
					2,
					2,
					2
				},
				{
					1,
					2,
					2,
					8,
					7,
					6,
					3,
					3,
					3,
					3,
					3
				}
			},
			{
				{
					8,
					8,
					8,
					1,
					2,
					2,
					1,
					1,
					1,
					1,
					1
				},
				{
					2,
					1,
					1,
					7,
					8,
					3,
					6,
					6,
					5,
					5,
					5
				}
			}
		};

		public static readonly int[,,] WAVE_BITS_PER_FRAME = new int[,,]
		{
			{
				{
					43,
					79,
					119,
					160,
					160,
					220,
					220,
					300,
					300,
					364,
					492
				},
				{
					60,
					96,
					136,
					177,
					177,
					237,
					237,
					317,
					317,
					381,
					509
				}
			},
			{
				{
					79,
					115,
					155,
					196,
					256,
					336,
					412,
					476,
					556,
					684,
					844
				},
				{
					96,
					132,
					172,
					213,
					273,
					353,
					429,
					493,
					573,
					701,
					861
				}
			},
			{
				{
					83,
					151,
					191,
					232,
					292,
					372,
					448,
					512,
					592,
					720,
					880
				},
				{
					100,
					168,
					208,
					249,
					309,
					389,
					465,
					529,
					609,
					737,
					897
				}
			}
		};

		private BinaryWriter raf;

		private readonly int mode;

		private readonly int quality;

		private readonly int sampleRate;

		private readonly int channels;

		private readonly int nframes;

		private readonly bool vbr;

		private bool isPCM;

		private int size;

		public PcmWaveWriter(int sampleRate, int channels)
		{
			this.sampleRate = sampleRate;
			this.channels = channels;
			this.isPCM = true;
		}

		public PcmWaveWriter(int mode, int quality, int sampleRate, int channels, int nframes, bool vbr)
		{
			this.mode = mode;
			this.quality = quality;
			this.sampleRate = sampleRate;
			this.channels = channels;
			this.nframes = nframes;
			this.vbr = vbr;
			this.isPCM = false;
		}

		public override void Close()
		{
			this.raf.BaseStream.Seek(4L, SeekOrigin.Begin);
			int value = (int)this.raf.BaseStream.Length - 8;
			this.raf.Write(value);
			this.raf.BaseStream.Seek(40L, SeekOrigin.Begin);
			this.raf.Write(this.size);
			this.raf.Close();
		}

		public override void Open(Stream stream)
		{
			this.raf = new BinaryWriter(stream);
			this.size = 0;
		}

		public override void WriteHeader(string comment)
		{
			byte[] bytes = Encoding.UTF8.GetBytes("RIFF");
			this.raf.Write(bytes, 0, bytes.Length);
			this.raf.Write(0);
			bytes = Encoding.UTF8.GetBytes("WAVE");
			this.raf.Write(bytes, 0, bytes.Length);
			bytes = Encoding.UTF8.GetBytes("fmt ");
			this.raf.Write(bytes, 0, bytes.Length);
			if (this.isPCM)
			{
				this.raf.Write(16);
				this.raf.Write(1);
				this.raf.Write((short)this.channels);
				this.raf.Write(this.sampleRate);
				this.raf.Write(this.sampleRate * this.channels * 2);
				this.raf.Write((short)(this.channels * 2));
				this.raf.Write(16);
			}
			else
			{
				int length = comment.Length;
				this.raf.Write((short)(100 + length));
				this.raf.Write(-24311);
				this.raf.Write((short)this.channels);
				this.raf.Write(this.sampleRate);
				this.raf.Write(PcmWaveWriter.CalculateEffectiveBitrate(this.mode, this.channels, this.quality) + 7 >> 3);
				this.raf.Write((short)PcmWaveWriter.CalculateBlockSize(this.mode, this.channels, this.quality));
				this.raf.Write((short)this.quality);
				this.raf.Write((short)(82 + length));
				this.raf.Write(1);
				this.raf.Write(0);
				this.raf.Write(AudioFileWriter.BuildSpeexHeader(this.sampleRate, this.mode, this.channels, this.vbr, this.nframes));
				this.raf.Write(comment);
			}
			bytes = Encoding.UTF8.GetBytes("data");
			this.raf.Write(bytes, 0, bytes.Length);
			this.raf.Write(0);
		}

		public override void WritePacket(byte[] data, int offset, int len)
		{
			this.raf.Write(data, offset, len);
			this.size += len;
		}

		private static int CalculateEffectiveBitrate(int mode, int channels, int quality)
		{
			return (PcmWaveWriter.WAVE_FRAME_SIZES[mode, channels - 1, quality] * PcmWaveWriter.WAVE_BITS_PER_FRAME[mode, channels - 1, quality] + 7 >> 3) * 50 * 8 / PcmWaveWriter.WAVE_BITS_PER_FRAME[mode, channels - 1, quality];
		}

		private static int CalculateBlockSize(int mode, int channels, int quality)
		{
			return PcmWaveWriter.WAVE_FRAME_SIZES[mode, channels - 1, quality] * PcmWaveWriter.WAVE_BITS_PER_FRAME[mode, channels - 1, quality] + 7 >> 3;
		}
	}
}
