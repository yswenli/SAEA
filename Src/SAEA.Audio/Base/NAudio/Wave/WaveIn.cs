using SAEA.Audio.Base.NAudio.Mixer;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace SAEA.Audio.Base.NAudio.Wave
{
    public class WaveIn : IWaveIn, IDisposable
    {
        private IntPtr waveInHandle;

        private volatile bool recording;

        private WaveInBuffer[] buffers;

        private readonly WaveInterop.WaveCallback callback;

        private WaveCallbackInfo callbackInfo;

        private readonly SynchronizationContext syncContext;

        private int lastReturnedBufferIndex;

        [method: CompilerGenerated]
        [CompilerGenerated]
        public event EventHandler<WaveInEventArgs> DataAvailable;

        [method: CompilerGenerated]
        [CompilerGenerated]
        public event EventHandler<StoppedEventArgs> RecordingStopped;

        public static int DeviceCount
        {
            get
            {
                return WaveInterop.waveInGetNumDevs();
            }
        }

        public int BufferMilliseconds
        {
            get;
            set;
        }

        public int NumberOfBuffers
        {
            get;
            set;
        }

        public int DeviceNumber
        {
            get;
            set;
        }

        public WaveFormat WaveFormat
        {
            get;
            set;
        }

        public WaveIn() : this(WaveCallbackInfo.NewWindow())
        {
        }

        public WaveIn(IntPtr windowHandle) : this(WaveCallbackInfo.ExistingWindow(windowHandle))
        {
        }

        public WaveIn(WaveCallbackInfo callbackInfo)
        {
            this.syncContext = SynchronizationContext.Current;
            if ((callbackInfo.Strategy == WaveCallbackStrategy.NewWindow || callbackInfo.Strategy == WaveCallbackStrategy.ExistingWindow) && this.syncContext == null)
            {
                throw new InvalidOperationException("Use WaveInEvent to record on a background thread");
            }
            this.DeviceNumber = 0;
            this.WaveFormat = new WaveFormat(8000, 16, 1);
            this.BufferMilliseconds = 100;
            this.NumberOfBuffers = 3;
            this.callback = new WaveInterop.WaveCallback(this.Callback);
            this.callbackInfo = callbackInfo;
            callbackInfo.Connect(this.callback);
        }

        public static WaveInCapabilities GetCapabilities(int devNumber)
        {
            WaveInCapabilities waveInCapabilities = default(WaveInCapabilities);
            int waveInCapsSize = Marshal.SizeOf(waveInCapabilities);
            MmException.Try(WaveInterop.waveInGetDevCaps((IntPtr)devNumber, out waveInCapabilities, waveInCapsSize), "waveInGetDevCaps");
            return waveInCapabilities;
        }

        private void CreateBuffers()
        {
            int num = this.BufferMilliseconds * this.WaveFormat.AverageBytesPerSecond / 1000;
            if (num % this.WaveFormat.BlockAlign != 0)
            {
                num -= num % this.WaveFormat.BlockAlign;
            }
            this.buffers = new WaveInBuffer[this.NumberOfBuffers];
            for (int i = 0; i < this.buffers.Length; i++)
            {
                this.buffers[i] = new WaveInBuffer(this.waveInHandle, num);
            }
        }

        private void Callback(IntPtr waveInHandle, WaveInterop.WaveMessage message, IntPtr userData, WaveHeader waveHeader, IntPtr reserved)
        {
            if (message == WaveInterop.WaveMessage.WaveInData && this.recording)
            {
                WaveInBuffer waveInBuffer = (WaveInBuffer)((GCHandle)waveHeader.userData).Target;
                if (waveInBuffer == null)
                {
                    return;
                }
                this.lastReturnedBufferIndex = Array.IndexOf<WaveInBuffer>(this.buffers, waveInBuffer);
                this.RaiseDataAvailable(waveInBuffer);
                try
                {
                    waveInBuffer.Reuse();
                }
                catch (Exception e)
                {
                    this.recording = false;
                    this.RaiseRecordingStopped(e);
                }
            }
        }

        private void RaiseDataAvailable(WaveInBuffer buffer)
        {
            EventHandler<WaveInEventArgs> expr_06 = this.DataAvailable;
            if (expr_06 == null)
            {
                return;
            }
            expr_06(this, new WaveInEventArgs(buffer.Data, buffer.BytesRecorded));
        }

        private void RaiseRecordingStopped(Exception e)
        {
            EventHandler<StoppedEventArgs> handler = this.RecordingStopped;
            if (handler != null)
            {
                if (this.syncContext == null)
                {
                    handler(this, new StoppedEventArgs(e));
                    return;
                }
                this.syncContext.Post(delegate (object state)
                {
                    handler(this, new StoppedEventArgs(e));
                }, null);
            }
        }

        private void OpenWaveInDevice()
        {
            this.CloseWaveInDevice();
            MmException.Try(this.callbackInfo.WaveInOpen(out this.waveInHandle, this.DeviceNumber, this.WaveFormat, this.callback), "waveInOpen");
            this.CreateBuffers();
        }

        public void StartRecording()
        {
            if (this.recording)
            {
                throw new InvalidOperationException("Already recording");
            }
            this.OpenWaveInDevice();
            this.EnqueueBuffers();
            MmException.Try(WaveInterop.waveInStart(this.waveInHandle), "waveInStart");
            this.recording = true;
        }

        private void EnqueueBuffers()
        {
            WaveInBuffer[] array = this.buffers;
            for (int i = 0; i < array.Length; i++)
            {
                WaveInBuffer waveInBuffer = array[i];
                if (!waveInBuffer.InQueue)
                {
                    waveInBuffer.Reuse();
                }
            }
        }

        public void StopRecording()
        {
            if (this.recording)
            {
                this.recording = false;
                MmException.Try(WaveInterop.waveInStop(this.waveInHandle), "waveInStop");
                for (int i = 0; i < this.buffers.Length; i++)
                {
                    int num = (i + this.lastReturnedBufferIndex + 1) % this.buffers.Length;
                    WaveInBuffer waveInBuffer = this.buffers[num];
                    if (waveInBuffer.Done)
                    {
                        this.RaiseDataAvailable(waveInBuffer);
                    }
                }
                this.RaiseRecordingStopped(null);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.recording)
                {
                    this.StopRecording();
                }
                this.CloseWaveInDevice();
                if (this.callbackInfo != null)
                {
                    this.callbackInfo.Disconnect();
                    this.callbackInfo = null;
                }
            }
        }

        private void CloseWaveInDevice()
        {
            if (this.waveInHandle == IntPtr.Zero)
            {
                return;
            }
            WaveInterop.waveInReset(this.waveInHandle);
            if (this.buffers != null)
            {
                for (int i = 0; i < this.buffers.Length; i++)
                {
                    this.buffers[i].Dispose();
                }
                this.buffers = null;
            }
            WaveInterop.waveInClose(this.waveInHandle);
            this.waveInHandle = IntPtr.Zero;
        }

        public MixerLine GetMixerLine()
        {
            MixerLine result;
            if (this.waveInHandle != IntPtr.Zero)
            {
                result = new MixerLine(this.waveInHandle, 0, MixerFlags.WaveInHandle);
            }
            else
            {
                result = new MixerLine((IntPtr)this.DeviceNumber, 0, MixerFlags.WaveIn);
            }
            return result;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
