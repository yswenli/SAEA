using SAEA.Audio.NAudio.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SAEA.Audio.NAudio.Wave
{
	public class Id3v2Tag
	{
		private long tagStartPosition;

		private long tagEndPosition;

		private byte[] rawData;

		public byte[] RawData
		{
			get
			{
				return this.rawData;
			}
		}

		public static Id3v2Tag ReadTag(Stream input)
		{
			Id3v2Tag result;
			try
			{
				result = new Id3v2Tag(input);
			}
			catch (FormatException)
			{
				result = null;
			}
			return result;
		}

		public static Id3v2Tag Create(IEnumerable<KeyValuePair<string, string>> tags)
		{
			return Id3v2Tag.ReadTag(Id3v2Tag.CreateId3v2TagStream(tags));
		}

		private static byte[] FrameSizeToBytes(int n)
		{
			byte[] expr_06 = BitConverter.GetBytes(n);
			Array.Reverse(expr_06);
			return expr_06;
		}

		private static byte[] CreateId3v2Frame(string key, string value)
		{
			if (string.IsNullOrEmpty(key))
			{
				throw new ArgumentNullException("key");
			}
			if (string.IsNullOrEmpty(value))
			{
				throw new ArgumentNullException("value");
			}
			if (key.Length != 4)
			{
				throw new ArgumentOutOfRangeException("key", "key " + key + " must be 4 characters long");
			}
			byte[] array = new byte[]
			{
				255,
				254
			};
			byte[] array2 = new byte[3];
			byte[] array3 = new byte[2];
			byte[] array4;
			if (key == "COMM")
			{
				array4 = ByteArrayExtensions.Concat(new byte[][]
				{
					new byte[]
					{
						1
					},
					array2,
					array3,
					array,
					Encoding.Unicode.GetBytes(value)
				});
			}
			else
			{
				array4 = ByteArrayExtensions.Concat(new byte[][]
				{
					new byte[]
					{
						1
					},
					array,
					Encoding.Unicode.GetBytes(value)
				});
			}
			return ByteArrayExtensions.Concat(new byte[][]
			{
				Encoding.UTF8.GetBytes(key),
				Id3v2Tag.FrameSizeToBytes(array4.Length),
				new byte[2],
				array4
			});
		}

		private static byte[] GetId3TagHeaderSize(int size)
		{
			byte[] array = new byte[4];
			for (int i = array.Length - 1; i >= 0; i--)
			{
				array[i] = (byte)(size % 128);
				size /= 128;
			}
			return array;
		}

		private static byte[] CreateId3v2TagHeader(IEnumerable<byte[]> frames)
		{
			int num = 0;
			foreach (byte[] current in frames)
			{
				num += current.Length;
			}
			byte[][] expr_32 = new byte[4][];
			expr_32[0] = Encoding.UTF8.GetBytes("ID3");
			int arg_50_1 = 1;
			byte[] expr_4C = new byte[2];
			expr_4C[0] = 3;
			expr_32[arg_50_1] = expr_4C;
			expr_32[2] = new byte[1];
			expr_32[3] = Id3v2Tag.GetId3TagHeaderSize(num);
			return ByteArrayExtensions.Concat(expr_32);
		}

		private static Stream CreateId3v2TagStream(IEnumerable<KeyValuePair<string, string>> tags)
		{
			List<byte[]> list = new List<byte[]>();
			foreach (KeyValuePair<string, string> current in tags)
			{
				list.Add(Id3v2Tag.CreateId3v2Frame(current.Key, current.Value));
			}
			byte[] array = Id3v2Tag.CreateId3v2TagHeader(list);
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.Write(array, 0, array.Length);
			foreach (byte[] current2 in list)
			{
				memoryStream.Write(current2, 0, current2.Length);
			}
			memoryStream.Position = 0L;
			return memoryStream;
		}

		private Id3v2Tag(Stream input)
		{
			this.tagStartPosition = input.Position;
			BinaryReader binaryReader = new BinaryReader(input);
			byte[] array = binaryReader.ReadBytes(10);
			if (array.Length >= 3 && array[0] == 73 && array[1] == 68 && array[2] == 51)
			{
				if ((array[5] & 64) == 64)
				{
					byte[] array2 = binaryReader.ReadBytes(4);
					int arg_75_0 = (int)array2[0] * 2097152 + (int)array2[1] * 16384 + (int)(array2[2] * 128);
					byte arg_79_0 = array2[3];
				}
				int num = (int)array[6] * 2097152;
				num += (int)array[7] * 16384;
				num += (int)(array[8] * 128);
				num += (int)array[9];
				binaryReader.ReadBytes(num);
				if ((array[5] & 16) == 16)
				{
					binaryReader.ReadBytes(10);
				}
				this.tagEndPosition = input.Position;
				input.Position = this.tagStartPosition;
				this.rawData = binaryReader.ReadBytes((int)(this.tagEndPosition - this.tagStartPosition));
				return;
			}
			input.Position = this.tagStartPosition;
			throw new FormatException("Not an ID3v2 tag");
		}
	}
}
