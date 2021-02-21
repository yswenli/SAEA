using SAEA.Audio.Base.NAudio.Mixer;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace SAEA.Audio.Base.NAudio.Wave
{
    public class WaveInEvent : IWaveIn, IDisposable
    {
        private readonly AutoResetEvent callbackEvent;

        private readonly SynchronizationContext syncContext;

        private IntPtr waveInHandle;

        private volatile bool recording;

        private WaveInBuffer[] buffers;

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

        public WaveInEvent()
        {
            this.callbackEvent = new AutoResetEvent(false);
            this.syncContext = SynchronizationContext.Current;
            this.DeviceNumber = 0;
            this.WaveFormat = new WaveFormat(8000, 16, 1);
            this.BufferMilliseconds = 100;
            this.NumberOfBuffers = 3;
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

        private void OpenWaveInDevice()
        {
            this.CloseWaveInDevice();
            MmException.Try(WaveInterop.waveInOpenWindow(out this.waveInHandle, (IntPtr)this.DeviceNumber, this.WaveFormat, this.callbackEvent.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, WaveInterop.WaveInOutOpenFlags.CallbackEvent), "waveInOpen");
            this.CreateBuffers();
        }

        public void StartRecording()
        {
            if (this.recording)
            {
                throw new InvalidOperationException("Already recording");
            }
            this.OpenWaveInDevice();
            MmException.Try(WaveInterop.waveInStart(this.waveInHandle), "waveInStart");
            this.recording = true;
            ThreadPool.QueueUserWorkItem(delegate (object state)
            {
                this.RecordThread();
            }, null);
        }

        private void RecordThread()
        {
            Exception e = null;
            try
            {
                this.DoRecording();
            }
            catch (Exception ex)
            {
            }
            finally
            {
                this.recording = false;
                this.RaiseRecordingStoppedEvent(e);
            }
        }

        private void DoRecording()
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
            while (this.recording)
            {
                if (this.callbackEvent.WaitOne() && this.recording)
                {
                    array = this.buffers;
                    for (int i = 0; i < array.Length; i++)
                    {
                        WaveInBuffer waveInBuffer2 = array[i];
                        if (waveInBuffer2.Done)
                        {
                            if (this.DataAvailable != null)
                            {
                                this.DataAvailable(this, new WaveInEventArgs(waveInBuffer2.Data, waveInBuffer2.BytesRecorded));
                            }
                            waveInBuffer2.Reuse();
                        }
                    }
                }
            }
        }

        private void RaiseRecordingStoppedEvent(Exception e)
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

        public void StopRecording()
        {
            this.recording = false;
            this.callbackEvent.Set();
            MmException.Try(WaveInterop.waveInStop(this.waveInHandle), "waveInStop");
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
            }
        }

        private void CloseWaveInDevice()
        {
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
