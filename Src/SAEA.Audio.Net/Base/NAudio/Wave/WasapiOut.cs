using SAEA.Audio.NAudio.CoreAudioApi;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace SAEA.Audio.NAudio.Wave
{
    public class WasapiOut : IWavePlayer, IDisposable, IWavePosition
    {
        private AudioClient audioClient;

        private readonly MMDevice mmDevice;

        private readonly AudioClientShareMode shareMode;

        private AudioRenderClient renderClient;

        private IWaveProvider sourceProvider;

        private int latencyMilliseconds;

        private int bufferFrameCount;

        private int bytesPerFrame;

        private readonly bool isUsingEventSync;

        private EventWaitHandle frameEventWaitHandle;

        private byte[] readBuffer;

        private volatile PlaybackState playbackState;

        private Thread playThread;

        private WaveFormat outputFormat;

        private bool dmoResamplerNeeded;

        private readonly SynchronizationContext syncContext;

        [method: CompilerGenerated]
        [CompilerGenerated]
        public event EventHandler<StoppedEventArgs> PlaybackStopped;

        public WaveFormat OutputWaveFormat
        {
            get
            {
                return this.outputFormat;
            }
        }

        public PlaybackState PlaybackState
        {
            get
            {
                return this.playbackState;
            }
        }

        public float Volume
        {
            get
            {
                return this.mmDevice.AudioEndpointVolume.MasterVolumeLevelScalar;
            }
            set
            {
                if (value < 0f)
                {
                    throw new ArgumentOutOfRangeException("value", "Volume must be between 0.0 and 1.0");
                }
                if (value > 1f)
                {
                    throw new ArgumentOutOfRangeException("value", "Volume must be between 0.0 and 1.0");
                }
                this.mmDevice.AudioEndpointVolume.MasterVolumeLevelScalar = value;
            }
        }

        public AudioStreamVolume AudioStreamVolume
        {
            get
            {
                if (this.shareMode == AudioClientShareMode.Exclusive)
                {
                    throw new InvalidOperationException("AudioStreamVolume is ONLY supported for shared audio streams.");
                }
                return this.audioClient.AudioStreamVolume;
            }
        }

        public WasapiOut() : this(WasapiOut.GetDefaultAudioEndpoint(), AudioClientShareMode.Shared, true, 200)
        {
        }

        public WasapiOut(AudioClientShareMode shareMode, int latency) : this(WasapiOut.GetDefaultAudioEndpoint(), shareMode, true, latency)
        {
        }

        public WasapiOut(AudioClientShareMode shareMode, bool useEventSync, int latency) : this(WasapiOut.GetDefaultAudioEndpoint(), shareMode, useEventSync, latency)
        {
        }

        public WasapiOut(MMDevice device, AudioClientShareMode shareMode, bool useEventSync, int latency)
        {
            this.audioClient = device.AudioClient;
            this.mmDevice = device;
            this.shareMode = shareMode;
            this.isUsingEventSync = useEventSync;
            this.latencyMilliseconds = latency;
            this.syncContext = SynchronizationContext.Current;
            this.outputFormat = this.audioClient.MixFormat;
        }

        private static MMDevice GetDefaultAudioEndpoint()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                throw new NotSupportedException("WASAPI supported only on Windows Vista and above");
            }
            return new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
        }

        private void PlayThread()
        {
            ResamplerDmoStream resamplerDmoStream = null;
            IWaveProvider playbackProvider = this.sourceProvider;
            Exception e = null;
            try
            {
                if (this.dmoResamplerNeeded)
                {
                    resamplerDmoStream = new ResamplerDmoStream(this.sourceProvider, this.outputFormat);
                    playbackProvider = resamplerDmoStream;
                }
                this.bufferFrameCount = this.audioClient.BufferSize;
                this.bytesPerFrame = this.outputFormat.Channels * this.outputFormat.BitsPerSample / 8;
                this.readBuffer = new byte[this.bufferFrameCount * this.bytesPerFrame];
                this.FillBuffer(playbackProvider, this.bufferFrameCount);
                WaitHandle[] waitHandles = new WaitHandle[]
                {
                    this.frameEventWaitHandle
                };
                this.audioClient.Start();
                while (this.playbackState != PlaybackState.Stopped)
                {
                    int num = 0;
                    if (this.isUsingEventSync)
                    {
                        num = WaitHandle.WaitAny(waitHandles, 3 * this.latencyMilliseconds, false);
                    }
                    else
                    {
                        Thread.Sleep(this.latencyMilliseconds / 2);
                    }
                    if (this.playbackState == PlaybackState.Playing && num != 258)
                    {
                        int num2;
                        if (this.isUsingEventSync)
                        {
                            num2 = ((this.shareMode == AudioClientShareMode.Shared) ? this.audioClient.CurrentPadding : 0);
                        }
                        else
                        {
                            num2 = this.audioClient.CurrentPadding;
                        }
                        int num3 = this.bufferFrameCount - num2;
                        if (num3 > 10)
                        {
                            this.FillBuffer(playbackProvider, num3);
                        }
                    }
                }
                Thread.Sleep(this.latencyMilliseconds / 2);
                this.audioClient.Stop();
                if (this.playbackState == PlaybackState.Stopped)
                {
                    this.audioClient.Reset();
                }
            }
            catch (Exception ex)
            {
            }
            finally
            {
                if (resamplerDmoStream != null)
                {
                    resamplerDmoStream.Dispose();
                }
                this.RaisePlaybackStopped(e);
            }
        }

        private void RaisePlaybackStopped(Exception e)
        {
            EventHandler<StoppedEventArgs> handler = this.PlaybackStopped;
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

        private void FillBuffer(IWaveProvider playbackProvider, int frameCount)
        {
            IntPtr buffer = this.renderClient.GetBuffer(frameCount);
            int count = frameCount * this.bytesPerFrame;
            int num = playbackProvider.Read(this.readBuffer, 0, count);
            if (num == 0)
            {
                this.playbackState = PlaybackState.Stopped;
            }
            Marshal.Copy(this.readBuffer, 0, buffer, num);
            int numFramesWritten = num / this.bytesPerFrame;
            this.renderClient.ReleaseBuffer(numFramesWritten, AudioClientBufferFlags.None);
        }

        public long GetPosition()
        {
            if (this.playbackState == PlaybackState.Stopped)
            {
                return 0L;
            }
            return (long)this.audioClient.AudioClockClient.AdjustedPosition;
        }

        public void Play()
        {
            if (this.playbackState != PlaybackState.Playing)
            {
                if (this.playbackState == PlaybackState.Stopped)
                {
                    this.playThread = new Thread(new ThreadStart(this.PlayThread));
                    this.playbackState = PlaybackState.Playing;
                    this.playThread.Start();
                    return;
                }
                this.playbackState = PlaybackState.Playing;
            }
        }

        public void Stop()
        {
            if (this.playbackState != PlaybackState.Stopped)
            {
                this.playbackState = PlaybackState.Stopped;
                this.playThread.Join();
                this.playThread = null;
            }
        }

        public void Pause()
        {
            if (this.playbackState == PlaybackState.Playing)
            {
                this.playbackState = PlaybackState.Paused;
            }
        }

        public void Init(IWaveProvider waveProvider)
        {
            long num = (long)(this.latencyMilliseconds * 10000);
            this.outputFormat = waveProvider.WaveFormat;
            WaveFormatExtensible waveFormatExtensible;
            if (!this.audioClient.IsFormatSupported(this.shareMode, this.outputFormat, out waveFormatExtensible))
            {
                if (waveFormatExtensible == null)
                {
                    WaveFormat waveFormat = this.audioClient.MixFormat;
                    if (!this.audioClient.IsFormatSupported(this.shareMode, waveFormat))
                    {
                        WaveFormatExtensible[] array = new WaveFormatExtensible[]
                        {
                            new WaveFormatExtensible(this.outputFormat.SampleRate, 32, this.outputFormat.Channels),
                            new WaveFormatExtensible(this.outputFormat.SampleRate, 24, this.outputFormat.Channels),
                            new WaveFormatExtensible(this.outputFormat.SampleRate, 16, this.outputFormat.Channels)
                        };
                        for (int i = 0; i < array.Length; i++)
                        {
                            waveFormat = array[i];
                            if (this.audioClient.IsFormatSupported(this.shareMode, waveFormat))
                            {
                                break;
                            }
                            waveFormat = null;
                        }
                        if (waveFormat == null)
                        {
                            waveFormat = new WaveFormatExtensible(this.outputFormat.SampleRate, 16, 2);
                            if (!this.audioClient.IsFormatSupported(this.shareMode, waveFormat))
                            {
                                throw new NotSupportedException("Can't find a supported format to use");
                            }
                        }
                    }
                    this.outputFormat = waveFormat;
                }
                else
                {
                    this.outputFormat = waveFormatExtensible;
                }
                using (new ResamplerDmoStream(waveProvider, this.outputFormat))
                {
                }
                this.dmoResamplerNeeded = true;
            }
            else
            {
                this.dmoResamplerNeeded = false;
            }
            this.sourceProvider = waveProvider;
            if (this.isUsingEventSync)
            {
                if (this.shareMode == AudioClientShareMode.Shared)
                {
                    this.audioClient.Initialize(this.shareMode, AudioClientStreamFlags.EventCallback, num, 0L, this.outputFormat, Guid.Empty);
                    long streamLatency = this.audioClient.StreamLatency;
                    if (streamLatency != 0L)
                    {
                        this.latencyMilliseconds = (int)(streamLatency / 10000L);
                    }
                }
                else
                {
                    this.audioClient.Initialize(this.shareMode, AudioClientStreamFlags.EventCallback, num, num, this.outputFormat, Guid.Empty);
                }
                this.frameEventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
                this.audioClient.SetEventHandle(this.frameEventWaitHandle.SafeWaitHandle.DangerousGetHandle());
            }
            else
            {
                this.audioClient.Initialize(this.shareMode, AudioClientStreamFlags.None, num, 0L, this.outputFormat, Guid.Empty);
            }
            this.renderClient = this.audioClient.AudioRenderClient;
        }

        public void Dispose()
        {
            if (this.audioClient != null)
            {
                this.Stop();
                this.audioClient.Dispose();
                this.audioClient = null;
                this.renderClient = null;
            }
        }
    }
}
