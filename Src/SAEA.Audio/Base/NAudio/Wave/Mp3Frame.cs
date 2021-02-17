using System;
using System.IO;

namespace SAEA.Audio.Base.NAudio.Wave
{
	public class Mp3Frame
	{
		private static readonly int[,,] bitRates = new int[,,]
		{
			{
				{
					0,
					32,
					64,
					96,
					128,
					160,
					192,
					224,
					256,
					288,
					320,
					352,
					384,
					416,
					448
				},
				{
					0,
					32,
					48,
					56,
					64,
					80,
					96,
					112,
					128,
					160,
					192,
					224,
					256,
					320,
					384
				},
				{
					0,
					32,
					40,
					48,
					56,
					64,
					80,
					96,
					112,
					128,
					160,
					192,
					224,
					256,
					320
				}
			},
			{
				{
					0,
					32,
					48,
					56,
					64,
					80,
					96,
					112,
					128,
					144,
					160,
					176,
					192,
					224,
					256
				},
				{
					0,
					8,
					16,
					24,
					32,
					40,
					48,
					56,
					64,
					80,
					96,
					112,
					128,
					144,
					160
				},
				{
					0,
					8,
					16,
					24,
					32,
					40,
					48,
					56,
					64,
					80,
					96,
					112,
					128,
					144,
					160
				}
			}
		};

		private static readonly int[,] samplesPerFrame = new int[,]
		{
			{
				384,
				1152,
				1152
			},
			{
				384,
				1152,
				576
			}
		};

		private static readonly int[] sampleRatesVersion1 = new int[]
		{
			44100,
			48000,
			32000
		};

		private static readonly int[] sampleRatesVersion2 = new int[]
		{
			22050,
			24000,
			16000
		};

		private static readonly int[] sampleRatesVersion25 = new int[]
		{
			11025,
			12000,
			8000
		};

		private const int MaxFrameLength = 16384;

		public int SampleRate
		{
			get;
			private set;
		}

		public int FrameLength
		{
			get;
			private set;
		}

		public int BitRate
		{
			get;
			private set;
		}

		public byte[] RawData
		{
			get;
			private set;
		}

		public MpegVersion MpegVersion
		{
			get;
			private set;
		}

		public MpegLayer MpegLayer
		{
			get;
			private set;
		}

		public ChannelMode ChannelMode
		{
			get;
			private set;
		}

		public int SampleCount
		{
			get;
			private set;
		}

		public int ChannelExtension
		{
			get;
			private set;
		}

		public int BitRateIndex
		{
			get;
			private set;
		}

		public bool Copyright
		{
			get;
			private set;
		}

		public bool CrcPresent
		{
			get;
			private set;
		}

		public long FileOffset
		{
			get;
			private set;
		}

		public static Mp3Frame LoadFromStream(Stream input)
		{
			return Mp3Frame.LoadFromStream(input, true);
		}

		public static Mp3Frame LoadFromStream(Stream input, bool readData)
		{
			Mp3Frame mp3Frame = new Mp3Frame();
			mp3Frame.FileOffset = input.Position;
			byte[] array = new byte[4];
			if (input.Read(array, 0, array.Length) < array.Length)
			{
				return null;
			}
			while (!Mp3Frame.IsValidHeader(array, mp3Frame))
			{
				array[0] = array[1];
				array[1] = array[2];
				array[2] = array[3];
				if (input.Read(array, 3, 1) < 1)
				{
					return null;
				}
				Mp3Frame expr_4C = mp3Frame;
				long fileOffset = expr_4C.FileOffset;
				expr_4C.FileOffset = fileOffset + 1L;
			}
			int num = mp3Frame.FrameLength - 4;
			if (readData)
			{
				mp3Frame.RawData = new byte[mp3Frame.FrameLength];
				Array.Copy(array, mp3Frame.RawData, 4);
				if (input.Read(mp3Frame.RawData, 4, num) < num)
				{
					throw new EndOfStreamException("Unexpected end of stream before frame complete");
				}
			}
			else
			{
				input.Position += (long)num;
			}
			return mp3Frame;
		}

		private Mp3Frame()
		{
		}

		private static bool IsValidHeader(byte[] headerBytes, Mp3Frame frame)
		{
			if (headerBytes[0] != 255 || (headerBytes[1] & 224) != 224)
			{
				return false;
			}
			frame.MpegVersion = (MpegVersion)((headerBytes[1] & 24) >> 3);
			if (frame.MpegVersion == MpegVersion.Reserved)
			{
				return false;
			}
			frame.MpegLayer = (MpegLayer)((headerBytes[1] & 6) >> 1);
			if (frame.MpegLayer == MpegLayer.Reserved)
			{
				return false;
			}
			int num = (frame.MpegLayer == MpegLayer.Layer1) ? 0 : ((frame.MpegLayer == MpegLayer.Layer2) ? 1 : 2);
			frame.CrcPresent = ((headerBytes[1] & 1) == 0);
			frame.BitRateIndex = (headerBytes[2] & 240) >> 4;
			if (frame.BitRateIndex == 15)
			{
				return false;
			}
			int num2 = (frame.MpegVersion == MpegVersion.Version1) ? 0 : 1;
			frame.BitRate = Mp3Frame.bitRates[num2, num, frame.BitRateIndex] * 1000;
			if (frame.BitRate == 0)
			{
				return false;
			}
			int num3 = (headerBytes[2] & 12) >> 2;
			if (num3 == 3)
			{
				return false;
			}
			if (frame.MpegVersion == MpegVersion.Version1)
			{
				frame.SampleRate = Mp3Frame.sampleRatesVersion1[num3];
			}
			else if (frame.MpegVersion == MpegVersion.Version2)
			{
				frame.SampleRate = Mp3Frame.sampleRatesVersion2[num3];
			}
			else
			{
				frame.SampleRate = Mp3Frame.sampleRatesVersion25[num3];
			}
			bool flag = (headerBytes[2] & 2) == 2;
			byte arg_123_0 = headerBytes[2];
			frame.ChannelMode = (ChannelMode)((headerBytes[3] & 192) >> 6);
			frame.ChannelExtension = (headerBytes[3] & 48) >> 4;
			if (frame.ChannelExtension != 0 && frame.ChannelMode != ChannelMode.JointStereo)
			{
				return false;
			}
			frame.Copyright = ((headerBytes[3] & 8) == 8);
			byte arg_167_0 = headerBytes[3];
			byte arg_16B_0 = headerBytes[3];
			int num4 = flag ? 1 : 0;
			frame.SampleCount = Mp3Frame.samplesPerFrame[num2, num];
			int num5 = frame.SampleCount / 8;
			if (frame.MpegLayer == MpegLayer.Layer1)
			{
				frame.FrameLength = (num5 * frame.BitRate / frame.SampleRate + num4) * 4;
			}
			else
			{
				frame.FrameLength = num5 * frame.BitRate / frame.SampleRate + num4;
			}
			return frame.FrameLength <= 16384;
		}
	}
}
