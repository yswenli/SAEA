using System;

namespace SAEA.Audio.Base.NSpeex
{
	public class SpeexJitterBuffer
	{
		private readonly SpeexDecoder decoder;

		private readonly JitterBuffer buffer = new JitterBuffer();

		private JitterBuffer.JitterBufferPacket outPacket;

		private JitterBuffer.JitterBufferPacket inPacket;

		public SpeexJitterBuffer(SpeexDecoder decoder)
		{
			if (decoder == null)
			{
				throw new ArgumentNullException("decoder");
			}
			this.decoder = decoder;
			this.inPacket.sequence = 0L;
			this.inPacket.span = 1L;
			this.inPacket.timestamp = 1L;
			this.buffer.DestroyBufferCallback = delegate(byte[] x)
			{
			};
			this.buffer.Init(1);
		}

		public void Get(short[] decodedFrame)
		{
			if (decodedFrame == null)
			{
				throw new ArgumentNullException("decodedFrame");
			}
			if (this.outPacket.data == null)
			{
				this.outPacket.data = new byte[decodedFrame.Length * 2];
			}
			else
			{
				Array.Clear(this.outPacket.data, 0, this.outPacket.data.Length);
			}
			this.outPacket.len = this.outPacket.data.Length;
			int num;
			if (this.buffer.Get(ref this.outPacket, 1, out num) != 0)
			{
				this.decoder.Decode(null, 0, 0, decodedFrame, 0, true);
			}
			else
			{
				this.decoder.Decode(this.outPacket.data, 0, this.outPacket.len, decodedFrame, 0, false);
			}
			this.buffer.Tick();
		}

		public void Put(byte[] frameData)
		{
			if (frameData == null)
			{
				throw new ArgumentNullException("frameData");
			}
			this.inPacket.data = frameData;
			this.inPacket.len = frameData.Length;
			this.inPacket.timestamp = this.inPacket.timestamp + 1L;
			this.buffer.Put(this.inPacket);
		}
	}
}
