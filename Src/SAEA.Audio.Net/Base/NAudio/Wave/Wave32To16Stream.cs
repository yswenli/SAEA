using SAEA.Audio.NAudio.Utils;
using System;

namespace SAEA.Audio.NAudio.Wave
{
	public class Wave32To16Stream : WaveStream
	{
		private WaveStream sourceStream;

		private readonly WaveFormat waveFormat;

		private readonly long length;

		private long position;

		private bool clip;

		private float volume;

		private readonly object lockObject = new object();

		private byte[] sourceBuffer;

		public float Volume
		{
			get
			{
				return this.volume;
			}
			set
			{
				this.volume = value;
			}
		}

		public override int BlockAlign
		{
			get
			{
				return this.sourceStream.BlockAlign / 2;
			}
		}

		public override long Length
		{
			get
			{
				return this.length;
			}
		}

		public override long Position
		{
			get
			{
				return this.position;
			}
			set
			{
				object obj = this.lockObject;
				lock (obj)
				{
					value -= value % (long)this.BlockAlign;
					this.sourceStream.Position = value * 2L;
					this.position = value;
				}
			}
		}

		public override WaveFormat WaveFormat
		{
			get
			{
				return this.waveFormat;
			}
		}

		public bool Clip
		{
			get
			{
				return this.clip;
			}
			set
			{
				this.clip = value;
			}
		}

		public Wave32To16Stream(WaveStream sourceStream)
		{
			if (sourceStream.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
			{
				throw new ArgumentException("Only 32 bit Floating point supported");
			}
			if (sourceStream.WaveFormat.BitsPerSample != 32)
			{
				throw new ArgumentException("Only 32 bit Floating point supported");
			}
			this.waveFormat = new WaveFormat(sourceStream.WaveFormat.SampleRate, 16, sourceStream.WaveFormat.Channels);
			this.volume = 1f;
			this.sourceStream = sourceStream;
			this.length = sourceStream.Length / 2L;
			this.position = sourceStream.Position / 2L;
		}

		public override int Read(byte[] destBuffer, int offset, int numBytes)
		{
			object obj = this.lockObject;
			int result;
			lock (obj)
			{
				int num = numBytes * 2;
				this.sourceBuffer = BufferHelpers.Ensure(this.sourceBuffer, num);
				int num2 = this.sourceStream.Read(this.sourceBuffer, 0, num);
				this.Convert32To16(destBuffer, offset, this.sourceBuffer, num2);
				this.position += (long)(num2 / 2);
				result = num2 / 2;
			}
			return result;
		}

		private unsafe void Convert32To16(byte[] destBuffer, int offset, byte[] source, int bytesRead)
		{
			fixed (byte* ptr = &destBuffer[offset], ptr2 = &source[0])
			{
				short* ptr3 = (short*)ptr;
				float* ptr4 = (float*)ptr2;
				int num = bytesRead / 4;
				for (int i = 0; i < num; i++)
				{
					float num2 = ptr4[i] * this.volume;
					if (num2 > 1f)
					{
						ptr3[i] = 32767;
						this.clip = true;
					}
					else if (num2 < -1f)
					{
						ptr3[i] = -32768;
						this.clip = true;
					}
					else
					{
						ptr3[i] = (short)(num2 * 32767f);
					}
				}
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && this.sourceStream != null)
			{
				this.sourceStream.Dispose();
				this.sourceStream = null;
			}
			base.Dispose(disposing);
		}
	}
}
