using System;

namespace SAEA.Audio.Base.NSpeex
{
	public class JitterBuffer
	{
		private class TimingBuffer
		{
			public int filled;

			public int curr_count;

			public int[] timing = new int[40];

			public short[] counts = new short[40];

			internal void Init()
			{
				this.filled = 0;
				this.curr_count = 0;
			}

			internal void Add(short timing)
			{
				if (this.filled >= 40 && (int)timing >= this.timing[this.filled - 1])
				{
					this.curr_count++;
					return;
				}
				int num = 0;
				while (num < this.filled && (int)timing >= this.timing[num])
				{
					num++;
				}
				if (num < this.filled)
				{
					int num2 = this.filled - num;
					if (this.filled == 40)
					{
						num2--;
					}
					Array.Copy(this.timing, num, this.timing, num + 1, num2);
					Array.Copy(this.counts, num, this.counts, num + 1, num2);
				}
				this.timing[num] = (int)timing;
				this.counts[num] = (short)this.curr_count;
				this.curr_count++;
				if (this.filled < 40)
				{
					this.filled++;
				}
			}
		}

		public struct JitterBufferPacket
		{
			public byte[] data;

			public int len;

			public long timestamp;

			public long span;

			public long sequence;

			public long user_data;
		}

		private const int MAX_BUFFER_SIZE = 200;

		private const int MAX_TIMINGS = 40;

		private const int MAX_BUFFERS = 3;

		private const int TOP_DELAY = 40;

		public const int JITTER_BUFFER_OK = 0;

		public const int JITTER_BUFFER_MISSING = 1;

		public const int JITTER_BUFFER_INSERTION = 2;

		public const int JITTER_BUFFER_INTERNAL_ERROR = -1;

		public const int JITTER_BUFFER_BAD_ARGUMENT = -2;

		private long pointer_timestamp;

		private long last_returned_timestamp;

		private long next_stop;

		private long buffered;

		private JitterBuffer.JitterBufferPacket[] packets = new JitterBuffer.JitterBufferPacket[200];

		private long[] arrival = new long[200];

		public Action<byte[]> DestroyBufferCallback;

		public int delay_step;

		private int concealment_size;

		private bool reset_state;

		private int buffer_margin;

		private int late_cutoff;

		private int interp_requested;

		private bool auto_adjust;

		private JitterBuffer.TimingBuffer[] _tb = new JitterBuffer.TimingBuffer[3];

		private JitterBuffer.TimingBuffer[] timeBuffers = new JitterBuffer.TimingBuffer[3];

		public int window_size;

		private int subwindow_size;

		private int max_late_rate;

		public int latency_tradeoff;

		public int auto_tradeoff;

		private int lost_count;

		private static int RoundDown(int x, int step)
		{
			if (x < 0)
			{
				return (x - step + 1) / step * step;
			}
			return x / step * step;
		}

		private void FreeBuffer(byte[] buffer)
		{
		}

		private byte[] AllocBuffer(long size)
		{
			return new byte[size];
		}

		public void Init(int step_size)
		{
			if (step_size <= 0)
			{
				throw new ArgumentOutOfRangeException("step_size");
			}
			for (int i = 0; i < 200; i++)
			{
				this.packets[i].data = null;
			}
			for (int i = 0; i < 3; i++)
			{
				this._tb[i] = new JitterBuffer.TimingBuffer();
			}
			this.delay_step = step_size;
			this.concealment_size = step_size;
			this.buffer_margin = 0;
			this.late_cutoff = 50;
			this.DestroyBufferCallback = null;
			this.latency_tradeoff = 0;
			this.auto_adjust = true;
			int maxLateRate = 4;
			this.SetMaxLateRate(maxLateRate);
			this.Reset();
		}

		private void SetMaxLateRate(int maxLateRate)
		{
			this.max_late_rate = maxLateRate;
			this.window_size = 4000 / this.max_late_rate;
			this.subwindow_size = this.window_size / 3;
		}

		private void Reset()
		{
			for (int i = 0; i < 200; i++)
			{
				if (this.packets[i].data != null)
				{
					if (this.DestroyBufferCallback != null)
					{
						this.DestroyBufferCallback(this.packets[i].data);
					}
					else
					{
						this.FreeBuffer(this.packets[i].data);
					}
					this.packets[i].data = null;
				}
			}
			this.pointer_timestamp = 0L;
			this.next_stop = 0L;
			this.reset_state = true;
			this.lost_count = 0;
			this.buffered = 0L;
			this.auto_tradeoff = 32000;
			for (int i = 0; i < 3; i++)
			{
				this._tb[i].Init();
				this.timeBuffers[i] = this._tb[i];
			}
		}

		private short ComputeOptDelay()
		{
			short num = 0;
			int num2 = 2147483647;
			int num3 = 0;
			int[] array = new int[3];
			bool flag = false;
			int num4 = 0;
			int num5 = 0;
			JitterBuffer.TimingBuffer[] tb = this._tb;
			int num6 = 0;
			for (int i = 0; i < 3; i++)
			{
				num6 += tb[i].curr_count;
			}
			if (num6 == 0)
			{
				return 0;
			}
			float num7;
			if (this.latency_tradeoff != 0)
			{
				num7 = (float)this.latency_tradeoff * 100f / (float)num6;
			}
			else
			{
				num7 = (float)(this.auto_tradeoff * this.window_size / num6);
			}
			for (int i = 0; i < 3; i++)
			{
				array[i] = 0;
			}
			for (int i = 0; i < 40; i++)
			{
				int num8 = -1;
				int num9 = 32767;
				for (int j = 0; j < 3; j++)
				{
					if (array[j] < tb[j].filled && tb[j].timing[array[j]] < num9)
					{
						num8 = j;
						num9 = tb[j].timing[array[j]];
					}
				}
				if (num8 == -1)
				{
					break;
				}
				if (i == 0)
				{
					num5 = num9;
				}
				num4 = num9;
				num9 = JitterBuffer.RoundDown(num9, this.delay_step);
				array[num8]++;
				int num10 = (int)((float)(-(float)num9) + num7 * (float)num3);
				if (num10 < num2)
				{
					num2 = num10;
					num = (short)num9;
				}
				num3++;
				if (num9 >= 0 && !flag)
				{
					flag = true;
					num3 += 4;
				}
			}
			int num11 = num4 - num5;
			this.auto_tradeoff = 1 + num11 / 40;
			if (num6 < 40 && num > 0)
			{
				return 0;
			}
			return num;
		}

		private void UpdateTimings(int timing)
		{
			if (timing < -32768)
			{
				timing = -32768;
			}
			if (timing > 32767)
			{
				timing = 32767;
			}
			short timing2 = (short)timing;
			if (this.timeBuffers[0].curr_count >= this.subwindow_size)
			{
				JitterBuffer.TimingBuffer timingBuffer = this.timeBuffers[2];
				for (int i = 2; i >= 1; i--)
				{
					this.timeBuffers[i] = this.timeBuffers[i - 1];
				}
				this.timeBuffers[0] = timingBuffer;
				this.timeBuffers[0].Init();
			}
			this.timeBuffers[0].Add(timing2);
		}

		public void Put(JitterBuffer.JitterBufferPacket packet)
		{
			if (!this.reset_state)
			{
				for (int i = 0; i < 200; i++)
				{
					if (this.packets[i].data != null && this.packets[i].timestamp + this.packets[i].span <= this.pointer_timestamp)
					{
						if (this.DestroyBufferCallback != null)
						{
							this.DestroyBufferCallback(this.packets[i].data);
						}
						else
						{
							this.FreeBuffer(this.packets[i].data);
						}
						this.packets[i].data = null;
					}
				}
			}
			bool flag;
			if (!this.reset_state && packet.timestamp < this.next_stop)
			{
				this.UpdateTimings((int)packet.timestamp - (int)this.next_stop - this.buffer_margin);
				flag = true;
			}
			else
			{
				flag = false;
			}
			if (this.lost_count > 20)
			{
				this.Reset();
			}
			if (this.reset_state || packet.timestamp + packet.span + (long)this.delay_step >= this.pointer_timestamp)
			{
				int i = 0;
				while (i < 200 && this.packets[i].data != null)
				{
					i++;
				}
				if (i == 200)
				{
					long timestamp = this.packets[0].timestamp;
					i = 0;
					for (int j = 1; j < 200; j++)
					{
						if (this.packets[i].data == null || this.packets[j].timestamp < timestamp)
						{
							timestamp = this.packets[j].timestamp;
							i = j;
						}
					}
					if (this.DestroyBufferCallback != null)
					{
						this.DestroyBufferCallback(this.packets[i].data);
					}
					else
					{
						this.FreeBuffer(this.packets[i].data);
					}
					this.packets[i].data = null;
				}
				if (this.DestroyBufferCallback != null)
				{
					this.packets[i].data = packet.data;
				}
				else
				{
					this.packets[i].data = this.AllocBuffer((long)packet.len);
					for (int j = 0; j < packet.len; j++)
					{
						this.packets[i].data[j] = packet.data[j];
					}
				}
				this.packets[i].timestamp = packet.timestamp;
				this.packets[i].span = packet.span;
				this.packets[i].len = packet.len;
				this.packets[i].sequence = packet.sequence;
				this.packets[i].user_data = packet.user_data;
				if (this.reset_state || flag)
				{
					this.arrival[i] = 0L;
					return;
				}
				this.arrival[i] = this.next_stop;
			}
		}

		public int Get(ref JitterBuffer.JitterBufferPacket packet, int desired_span, out int start_offset)
		{
			if (desired_span <= 0)
			{
				throw new ArgumentOutOfRangeException("desired_span");
			}
			start_offset = 0;
			int i;
			if (this.reset_state)
			{
				bool flag = false;
				long num = 0L;
				for (i = 0; i < 200; i++)
				{
					if (this.packets[i].data != null && (!flag || this.packets[i].timestamp < num))
					{
						num = this.packets[i].timestamp;
						flag = true;
					}
				}
				if (!flag)
				{
					packet.timestamp = 0L;
					packet.span = (long)this.interp_requested;
					return 1;
				}
				this.reset_state = false;
				this.pointer_timestamp = num;
				this.next_stop = num;
			}
			this.last_returned_timestamp = this.pointer_timestamp;
			if (this.interp_requested != 0)
			{
				packet.timestamp = this.pointer_timestamp;
				packet.span = (long)this.interp_requested;
				this.pointer_timestamp += (long)this.interp_requested;
				packet.len = 0;
				this.interp_requested = 0;
				this.buffered = packet.span - (long)desired_span;
				return 2;
			}
			i = 0;
			while (i < 200 && (this.packets[i].data == null || this.packets[i].timestamp != this.pointer_timestamp || this.packets[i].timestamp + this.packets[i].span < this.pointer_timestamp + (long)desired_span))
			{
				i++;
			}
			if (i == 200)
			{
				i = 0;
				while (i < 200 && (this.packets[i].data == null || this.packets[i].timestamp > this.pointer_timestamp || this.packets[i].timestamp + this.packets[i].span < this.pointer_timestamp + (long)desired_span))
				{
					i++;
				}
			}
			if (i == 200)
			{
				i = 0;
				while (i < 200 && (this.packets[i].data == null || this.packets[i].timestamp > this.pointer_timestamp || this.packets[i].timestamp + this.packets[i].span <= this.pointer_timestamp))
				{
					i++;
				}
			}
			if (i == 200)
			{
				bool flag2 = false;
				long num2 = 0L;
				long num3 = 0L;
				int num4 = 0;
				for (i = 0; i < 200; i++)
				{
					if (this.packets[i].data != null && this.packets[i].timestamp < this.pointer_timestamp + (long)desired_span && this.packets[i].timestamp >= this.pointer_timestamp && (!flag2 || this.packets[i].timestamp < num2 || (this.packets[i].timestamp == num2 && this.packets[i].span > num3)))
					{
						num2 = this.packets[i].timestamp;
						num3 = this.packets[i].span;
						num4 = i;
						flag2 = true;
					}
				}
				if (flag2)
				{
					i = num4;
				}
			}
			if (i != 200)
			{
				this.lost_count = 0;
				if (this.arrival[i] != 0L)
				{
					this.UpdateTimings((int)this.packets[i].timestamp - (int)this.arrival[i] - this.buffer_margin);
				}
				if (this.DestroyBufferCallback != null)
				{
					packet.data = this.packets[i].data;
					packet.len = this.packets[i].len;
				}
				else
				{
					if (this.packets[i].len <= packet.len)
					{
						packet.len = this.packets[i].len;
					}
					for (long num5 = 0L; num5 < (long)packet.len; num5 += 1L)
					{
						checked
						{
							packet.data[(int)((IntPtr)num5)] = this.packets[i].data[(int)((IntPtr)num5)];
						}
					}
					this.FreeBuffer(this.packets[i].data);
				}
				this.packets[i].data = null;
				int num6 = (int)this.packets[i].timestamp - (int)this.pointer_timestamp;
				if (start_offset != 0)
				{
					start_offset = num6;
				}
				packet.timestamp = this.packets[i].timestamp;
				this.last_returned_timestamp = packet.timestamp;
				packet.span = this.packets[i].span;
				packet.sequence = this.packets[i].sequence;
				packet.user_data = this.packets[i].user_data;
				packet.len = this.packets[i].len;
				this.pointer_timestamp = this.packets[i].timestamp + this.packets[i].span;
				this.buffered = packet.span - (long)desired_span;
				if (start_offset != 0)
				{
					this.buffered += (long)start_offset;
				}
				return 0;
			}
			this.lost_count++;
			short num7 = this.ComputeOptDelay();
			if (num7 < 0)
			{
				this.ShiftTimings((short)-num7);
				packet.timestamp = this.pointer_timestamp;
				packet.span = (long)(-(long)num7);
				packet.len = 0;
				this.buffered = packet.span - (long)desired_span;
				return 2;
			}
			packet.timestamp = this.pointer_timestamp;
			desired_span = JitterBuffer.RoundDown(desired_span, this.concealment_size);
			packet.span = (long)desired_span;
			this.pointer_timestamp += (long)desired_span;
			packet.len = 0;
			this.buffered = packet.span - (long)desired_span;
			return 1;
		}

		private void ShiftTimings(short amount)
		{
			for (int i = 0; i < 3; i++)
			{
				for (int j = 0; j < this.timeBuffers[i].filled; j++)
				{
					this.timeBuffers[i].timing[j] += (int)amount;
				}
			}
		}

		private int UpdateDelay()
		{
			short num = this.ComputeOptDelay();
			if (num < 0)
			{
				this.ShiftTimings((short)-num);
				this.pointer_timestamp += (long)num;
				this.interp_requested = (int)(-(int)num);
			}
			else if (num > 0)
			{
				this.ShiftTimings((short)-num);
				this.pointer_timestamp += (long)num;
			}
			return (int)num;
		}

		public void Tick()
		{
			if (this.auto_adjust)
			{
				this.UpdateDelay();
			}
			if (this.buffered >= 0L)
			{
				this.next_stop = this.pointer_timestamp - this.buffered;
			}
			else
			{
				this.next_stop = this.pointer_timestamp;
			}
			this.buffered = 0L;
		}
	}
}
