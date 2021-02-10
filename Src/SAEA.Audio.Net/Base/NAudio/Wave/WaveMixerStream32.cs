using System;
using System.Collections.Generic;

namespace SAEA.Audio.NAudio.Wave
{
	public class WaveMixerStream32 : WaveStream
	{
		private readonly List<WaveStream> inputStreams;

		private readonly object inputsLock;

		private WaveFormat waveFormat;

		private long length;

		private long position;

		private readonly int bytesPerSample;

		public int InputCount
		{
			get
			{
				return this.inputStreams.Count;
			}
		}

		public bool AutoStop
		{
			get;
			set;
		}

		public override int BlockAlign
		{
			get
			{
				return this.waveFormat.BlockAlign;
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
				object obj = this.inputsLock;
				lock (obj)
				{
					value = Math.Min(value, this.Length);
					foreach (WaveStream current in this.inputStreams)
					{
						current.Position = Math.Min(value, current.Length);
					}
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

		public WaveMixerStream32()
		{
			this.AutoStop = true;
			this.waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
			this.bytesPerSample = 4;
			this.inputStreams = new List<WaveStream>();
			this.inputsLock = new object();
		}

		public WaveMixerStream32(IEnumerable<WaveStream> inputStreams, bool autoStop) : this()
		{
			this.AutoStop = autoStop;
			foreach (WaveStream current in inputStreams)
			{
				this.AddInputStream(current);
			}
		}

		public void AddInputStream(WaveStream waveStream)
		{
			if (waveStream.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
			{
				throw new ArgumentException("Must be IEEE floating point", "waveStream");
			}
			if (waveStream.WaveFormat.BitsPerSample != 32)
			{
				throw new ArgumentException("Only 32 bit audio currently supported", "waveStream");
			}
			if (this.inputStreams.Count == 0)
			{
				int sampleRate = waveStream.WaveFormat.SampleRate;
				int channels = waveStream.WaveFormat.Channels;
				this.waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
			}
			else if (!waveStream.WaveFormat.Equals(this.waveFormat))
			{
				throw new ArgumentException("All incoming channels must have the same format", "waveStream");
			}
			object obj = this.inputsLock;
			lock (obj)
			{
				this.inputStreams.Add(waveStream);
				this.length = Math.Max(this.length, waveStream.Length);
				waveStream.Position = this.Position;
			}
		}

		public void RemoveInputStream(WaveStream waveStream)
		{
			object obj = this.inputsLock;
			lock (obj)
			{
				if (this.inputStreams.Remove(waveStream))
				{
					long val = 0L;
					foreach (WaveStream current in this.inputStreams)
					{
						val = Math.Max(val, current.Length);
					}
					this.length = val;
				}
			}
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (this.AutoStop && this.position + (long)count > this.length)
			{
				count = (int)(this.length - this.position);
			}
			if (count % this.bytesPerSample != 0)
			{
				throw new ArgumentException("Must read an whole number of samples", "count");
			}
			Array.Clear(buffer, offset, count);
			int val = 0;
			byte[] array = new byte[count];
			object obj = this.inputsLock;
			lock (obj)
			{
				foreach (WaveStream current in this.inputStreams)
				{
					if (current.HasData(count))
					{
						int num = current.Read(array, 0, count);
						val = Math.Max(val, num);
						if (num > 0)
						{
							WaveMixerStream32.Sum32BitAudio(buffer, offset, array, num);
						}
					}
					else
					{
						val = Math.Max(val, count);
						current.Position += (long)count;
					}
				}
			}
			this.position += (long)count;
			return count;
		}

		private unsafe static void Sum32BitAudio(byte[] destBuffer, int offset, byte[] sourceBuffer, int bytesRead)
		{
			fixed (byte* ptr = &destBuffer[offset], ptr2 = &sourceBuffer[0])
			{
				float* ptr3 = (float*)ptr;
				float* ptr4 = (float*)ptr2;
				int num = bytesRead / 4;
				for (int i = 0; i < num; i++)
				{
					ptr3[i] += ptr4[i];
				}
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				object obj = this.inputsLock;
				lock (obj)
				{
					using (List<WaveStream>.Enumerator enumerator = this.inputStreams.GetEnumerator())
					{
						while (enumerator.MoveNext())
						{
							enumerator.Current.Dispose();
						}
					}
				}
			}
			base.Dispose(disposing);
		}
	}
}
