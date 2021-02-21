using SAEA.Audio.Base.NAudio.Utils;
using SAEA.Audio.Base.NAudio.Wave;
using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.MediaFoundation
{
    public abstract class MediaFoundationTransform : IWaveProvider, IDisposable
	{
		protected readonly IWaveProvider sourceProvider;

		protected readonly WaveFormat outputWaveFormat;

		private readonly byte[] sourceBuffer;

		private byte[] outputBuffer;

		private int outputBufferOffset;

		private int outputBufferCount;

		private IMFTransform transform;

		private bool disposed;

		private long inputPosition;

		private long outputPosition;

		private bool initializedForStreaming;

		public WaveFormat WaveFormat
		{
			get
			{
				return this.outputWaveFormat;
			}
		}

		public MediaFoundationTransform(IWaveProvider sourceProvider, WaveFormat outputFormat)
		{
			this.outputWaveFormat = outputFormat;
			this.sourceProvider = sourceProvider;
			this.sourceBuffer = new byte[sourceProvider.WaveFormat.AverageBytesPerSecond];
			this.outputBuffer = new byte[this.outputWaveFormat.AverageBytesPerSecond + this.outputWaveFormat.BlockAlign];
		}

		private void InitializeTransformForStreaming()
		{
			this.transform.ProcessMessage(MFT_MESSAGE_TYPE.MFT_MESSAGE_COMMAND_FLUSH, IntPtr.Zero);
			this.transform.ProcessMessage(MFT_MESSAGE_TYPE.MFT_MESSAGE_NOTIFY_BEGIN_STREAMING, IntPtr.Zero);
			this.transform.ProcessMessage(MFT_MESSAGE_TYPE.MFT_MESSAGE_NOTIFY_START_OF_STREAM, IntPtr.Zero);
			this.initializedForStreaming = true;
		}

		protected abstract IMFTransform CreateTransform();

		protected virtual void Dispose(bool disposing)
		{
			if (this.transform != null)
			{
				Marshal.ReleaseComObject(this.transform);
			}
		}

		public void Dispose()
		{
			if (!this.disposed)
			{
				this.disposed = true;
				this.Dispose(true);
				GC.SuppressFinalize(this);
			}
		}

		~MediaFoundationTransform()
		{
			this.Dispose(false);
		}

		public int Read(byte[] buffer, int offset, int count)
		{
			if (this.transform == null)
			{
				this.transform = this.CreateTransform();
				this.InitializeTransformForStreaming();
			}
			int i = 0;
			if (this.outputBufferCount > 0)
			{
				i += this.ReadFromOutputBuffer(buffer, offset, count - i);
			}
			while (i < count)
			{
				IMFSample iMFSample = this.ReadFromSource();
				if (iMFSample == null)
				{
					this.EndStreamAndDrain();
					i += this.ReadFromOutputBuffer(buffer, offset + i, count - i);
					break;
				}
				if (!this.initializedForStreaming)
				{
					this.InitializeTransformForStreaming();
				}
				this.transform.ProcessInput(0, iMFSample, 0);
				Marshal.ReleaseComObject(iMFSample);
				this.ReadFromTransform();
				i += this.ReadFromOutputBuffer(buffer, offset + i, count - i);
			}
			return i;
		}

		private void EndStreamAndDrain()
		{
			this.transform.ProcessMessage(MFT_MESSAGE_TYPE.MFT_MESSAGE_NOTIFY_END_OF_STREAM, IntPtr.Zero);
			this.transform.ProcessMessage(MFT_MESSAGE_TYPE.MFT_MESSAGE_COMMAND_DRAIN, IntPtr.Zero);
			int num;
			do
			{
				num = this.ReadFromTransform();
			}
			while (num > 0);
			this.outputBufferCount = 0;
			this.outputBufferOffset = 0;
			this.inputPosition = 0L;
			this.outputPosition = 0L;
			this.transform.ProcessMessage(MFT_MESSAGE_TYPE.MFT_MESSAGE_NOTIFY_END_STREAMING, IntPtr.Zero);
			this.initializedForStreaming = false;
		}

		private int ReadFromTransform()
		{
			MFT_OUTPUT_DATA_BUFFER[] array = new MFT_OUTPUT_DATA_BUFFER[1];
			IMFSample iMFSample = MediaFoundationApi.CreateSample();
			IMFMediaBuffer iMFMediaBuffer = MediaFoundationApi.CreateMemoryBuffer(this.outputBuffer.Length);
			iMFSample.AddBuffer(iMFMediaBuffer);
			iMFSample.SetSampleTime(this.outputPosition);
			array[0].pSample = iMFSample;
			_MFT_PROCESS_OUTPUT_STATUS mFT_PROCESS_OUTPUT_STATUS;
			int num = this.transform.ProcessOutput(_MFT_PROCESS_OUTPUT_FLAGS.None, 1, array, out mFT_PROCESS_OUTPUT_STATUS);
			if (num == -1072861838)
			{
				Marshal.ReleaseComObject(iMFMediaBuffer);
				Marshal.ReleaseComObject(iMFSample);
				return 0;
			}
			if (num != 0)
			{
				Marshal.ThrowExceptionForHR(num);
			}
			IMFMediaBuffer iMFMediaBuffer2;
			array[0].pSample.ConvertToContiguousBuffer(out iMFMediaBuffer2);
			IntPtr source;
			int num2;
			int num3;
			iMFMediaBuffer2.Lock(out source, out num2, out num3);
			this.outputBuffer = BufferHelpers.Ensure(this.outputBuffer, num3);
			Marshal.Copy(source, this.outputBuffer, 0, num3);
			this.outputBufferOffset = 0;
			this.outputBufferCount = num3;
			iMFMediaBuffer2.Unlock();
			this.outputPosition += MediaFoundationTransform.BytesToNsPosition(this.outputBufferCount, this.WaveFormat);
			Marshal.ReleaseComObject(iMFMediaBuffer);
			iMFSample.RemoveAllBuffers();
			Marshal.ReleaseComObject(iMFSample);
			Marshal.ReleaseComObject(iMFMediaBuffer2);
			return num3;
		}

		private static long BytesToNsPosition(int bytes, WaveFormat waveFormat)
		{
			return 10000000L * (long)bytes / (long)waveFormat.AverageBytesPerSecond;
		}

		private IMFSample ReadFromSource()
		{
			int num = this.sourceProvider.Read(this.sourceBuffer, 0, this.sourceBuffer.Length);
			if (num == 0)
			{
				return null;
			}
			IMFMediaBuffer iMFMediaBuffer = MediaFoundationApi.CreateMemoryBuffer(num);
			IntPtr destination;
			int num2;
			int num3;
			iMFMediaBuffer.Lock(out destination, out num2, out num3);
			Marshal.Copy(this.sourceBuffer, 0, destination, num);
			iMFMediaBuffer.Unlock();
			iMFMediaBuffer.SetCurrentLength(num);
			IMFSample expr_53 = MediaFoundationApi.CreateSample();
			expr_53.AddBuffer(iMFMediaBuffer);
			expr_53.SetSampleTime(this.inputPosition);
			long num4 = MediaFoundationTransform.BytesToNsPosition(num, this.sourceProvider.WaveFormat);
			expr_53.SetSampleDuration(num4);
			this.inputPosition += num4;
			Marshal.ReleaseComObject(iMFMediaBuffer);
			return expr_53;
		}

		private int ReadFromOutputBuffer(byte[] buffer, int offset, int needed)
		{
			int num = Math.Min(needed, this.outputBufferCount);
			Array.Copy(this.outputBuffer, this.outputBufferOffset, buffer, offset, num);
			this.outputBufferOffset += num;
			this.outputBufferCount -= num;
			if (this.outputBufferCount == 0)
			{
				this.outputBufferOffset = 0;
			}
			return num;
		}

		public void Reposition()
		{
			if (this.initializedForStreaming)
			{
				this.EndStreamAndDrain();
				this.InitializeTransformForStreaming();
			}
		}
	}
}
