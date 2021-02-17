using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;

namespace SAEA.Audio.Base.NAudio.Wave
{
    public class DirectSoundOut : IWavePlayer, IDisposable
    {
        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        internal class BufferDescription
        {
            public int dwSize;

            [MarshalAs(UnmanagedType.U4)]
            public DirectSoundOut.DirectSoundBufferCaps dwFlags;

            public uint dwBufferBytes;

            public int dwReserved;

            public IntPtr lpwfxFormat;

            public Guid guidAlgo;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        internal class BufferCaps
        {
            public int dwSize;

            public int dwFlags;

            public int dwBufferBytes;

            public int dwUnlockTransferRate;

            public int dwPlayCpuOverhead;
        }

        internal enum DirectSoundCooperativeLevel : uint
        {
            DSSCL_NORMAL = 1u,
            DSSCL_PRIORITY,
            DSSCL_EXCLUSIVE,
            DSSCL_WRITEPRIMARY
        }

        [Flags]
        internal enum DirectSoundPlayFlags : uint
        {
            DSBPLAY_LOOPING = 1u,
            DSBPLAY_LOCHARDWARE = 2u,
            DSBPLAY_LOCSOFTWARE = 4u,
            DSBPLAY_TERMINATEBY_TIME = 8u,
            DSBPLAY_TERMINATEBY_DISTANCE = 16u,
            DSBPLAY_TERMINATEBY_PRIORITY = 32u
        }

        internal enum DirectSoundBufferLockFlag : uint
        {
            None,
            FromWriteCursor,
            EntireBuffer
        }

        [Flags]
        internal enum DirectSoundBufferStatus : uint
        {
            DSBSTATUS_PLAYING = 1u,
            DSBSTATUS_BUFFERLOST = 2u,
            DSBSTATUS_LOOPING = 4u,
            DSBSTATUS_LOCHARDWARE = 8u,
            DSBSTATUS_LOCSOFTWARE = 16u,
            DSBSTATUS_TERMINATED = 32u
        }

        [Flags]
        internal enum DirectSoundBufferCaps : uint
        {
            DSBCAPS_PRIMARYBUFFER = 1u,
            DSBCAPS_STATIC = 2u,
            DSBCAPS_LOCHARDWARE = 4u,
            DSBCAPS_LOCSOFTWARE = 8u,
            DSBCAPS_CTRL3D = 16u,
            DSBCAPS_CTRLFREQUENCY = 32u,
            DSBCAPS_CTRLPAN = 64u,
            DSBCAPS_CTRLVOLUME = 128u,
            DSBCAPS_CTRLPOSITIONNOTIFY = 256u,
            DSBCAPS_CTRLFX = 512u,
            DSBCAPS_STICKYFOCUS = 16384u,
            DSBCAPS_GLOBALFOCUS = 32768u,
            DSBCAPS_GETCURRENTPOSITION2 = 65536u,
            DSBCAPS_MUTE3DATMAXDISTANCE = 131072u,
            DSBCAPS_LOCDEFER = 262144u
        }

        internal struct DirectSoundBufferPositionNotify
        {
            public uint dwOffset;

            public IntPtr hEventNotify;
        }

        [Guid("279AFA83-4981-11CE-A521-0020AF0BE560"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
        [ComImport]
        internal interface IDirectSound
        {
            void CreateSoundBuffer([In] DirectSoundOut.BufferDescription desc, [MarshalAs(UnmanagedType.Interface)] out object dsDSoundBuffer, IntPtr pUnkOuter);

            void GetCaps(IntPtr caps);

            void DuplicateSoundBuffer([MarshalAs(UnmanagedType.Interface)] [In] DirectSoundOut.IDirectSoundBuffer bufferOriginal, [MarshalAs(UnmanagedType.Interface)] [In] DirectSoundOut.IDirectSoundBuffer bufferDuplicate);

            void SetCooperativeLevel(IntPtr HWND, [MarshalAs(UnmanagedType.U4)] [In] DirectSoundOut.DirectSoundCooperativeLevel dwLevel);

            void Compact();

            void GetSpeakerConfig(IntPtr pdwSpeakerConfig);

            void SetSpeakerConfig(uint pdwSpeakerConfig);

            void Initialize([MarshalAs(UnmanagedType.LPStruct)] [In] Guid guid);
        }

        [Guid("279AFA85-4981-11CE-A521-0020AF0BE560"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
        [ComImport]
        internal interface IDirectSoundBuffer
        {
            void GetCaps([MarshalAs(UnmanagedType.LPStruct)] DirectSoundOut.BufferCaps pBufferCaps);

            void GetCurrentPosition(out uint currentPlayCursor, out uint currentWriteCursor);

            void GetFormat();

            [return: MarshalAs(UnmanagedType.I4)]
            int GetVolume();

            void GetPan(out uint pan);

            [return: MarshalAs(UnmanagedType.I4)]
            int GetFrequency();

            [return: MarshalAs(UnmanagedType.U4)]
            DirectSoundOut.DirectSoundBufferStatus GetStatus();

            void Initialize([MarshalAs(UnmanagedType.Interface)] [In] DirectSoundOut.IDirectSound directSound, [In] DirectSoundOut.BufferDescription desc);

            void Lock(int dwOffset, uint dwBytes, out IntPtr audioPtr1, out int audioBytes1, out IntPtr audioPtr2, out int audioBytes2, [MarshalAs(UnmanagedType.U4)] DirectSoundOut.DirectSoundBufferLockFlag dwFlags);

            void Play(uint dwReserved1, uint dwPriority, [MarshalAs(UnmanagedType.U4)] [In] DirectSoundOut.DirectSoundPlayFlags dwFlags);

            void SetCurrentPosition(uint dwNewPosition);

            void SetFormat([In] WaveFormat pcfxFormat);

            void SetVolume(int volume);

            void SetPan(uint pan);

            void SetFrequency(uint frequency);

            void Stop();

            void Unlock(IntPtr pvAudioPtr1, int dwAudioBytes1, IntPtr pvAudioPtr2, int dwAudioBytes2);

            void Restore();
        }

        [Guid("b0210783-89cd-11d0-af08-00a0c925cd16"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
        [ComImport]
        internal interface IDirectSoundNotify
        {
            void SetNotificationPositions(uint dwPositionNotifies, [MarshalAs(UnmanagedType.LPArray)] [In] DirectSoundOut.DirectSoundBufferPositionNotify[] pcPositionNotifies);
        }

        private delegate bool DSEnumCallback(IntPtr lpGuid, IntPtr lpcstrDescription, IntPtr lpcstrModule, IntPtr lpContext);

        private PlaybackState playbackState;

        private WaveFormat waveFormat;

        private int samplesTotalSize;

        private int samplesFrameSize;

        private int nextSamplesWriteIndex;

        private int desiredLatency;

        private Guid device;

        private byte[] samples;

        private IWaveProvider waveStream;

        private DirectSoundOut.IDirectSound directSound;

        private DirectSoundOut.IDirectSoundBuffer primarySoundBuffer;

        private DirectSoundOut.IDirectSoundBuffer secondaryBuffer;

        private EventWaitHandle frameEventWaitHandle1;

        private EventWaitHandle frameEventWaitHandle2;

        private EventWaitHandle endEventWaitHandle;

        private Thread notifyThread;

        private SynchronizationContext syncContext;

        private long bytesPlayed;

        private object m_LockObject = new object();

        private static List<DirectSoundDeviceInfo> devices;

        public static readonly Guid DSDEVID_DefaultPlayback = new Guid("DEF00000-9C6D-47ED-AAF1-4DDA8F2B5C03");

        public static readonly Guid DSDEVID_DefaultCapture = new Guid("DEF00001-9C6D-47ED-AAF1-4DDA8F2B5C03");

        public static readonly Guid DSDEVID_DefaultVoicePlayback = new Guid("DEF00002-9C6D-47ED-AAF1-4DDA8F2B5C03");

        public static readonly Guid DSDEVID_DefaultVoiceCapture = new Guid("DEF00003-9C6D-47ED-AAF1-4DDA8F2B5C03");

        [method: CompilerGenerated]
        [CompilerGenerated]
        public event EventHandler<StoppedEventArgs> PlaybackStopped;

        public static IEnumerable<DirectSoundDeviceInfo> Devices
        {
            get
            {
                DirectSoundOut.devices = new List<DirectSoundDeviceInfo>();
                DirectSoundOut.DirectSoundEnumerate(new DirectSoundOut.DSEnumCallback(DirectSoundOut.EnumCallback), IntPtr.Zero);
                return DirectSoundOut.devices;
            }
        }

        public TimeSpan PlaybackPosition
        {
            get
            {
                return TimeSpan.FromMilliseconds((double)(this.GetPosition() / (long)(this.waveFormat.Channels * this.waveFormat.BitsPerSample / 8)) * 1000.0 / (double)this.waveFormat.SampleRate);
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
                return 1f;
            }
            set
            {
                if (value != 1f)
                {
                    throw new InvalidOperationException("Setting volume not supported on DirectSoundOut, adjust the volume on your WaveProvider instead");
                }
            }
        }

        private static bool EnumCallback(IntPtr lpGuid, IntPtr lpcstrDescription, IntPtr lpcstrModule, IntPtr lpContext)
        {
            DirectSoundDeviceInfo directSoundDeviceInfo = new DirectSoundDeviceInfo();
            if (lpGuid == IntPtr.Zero)
            {
                directSoundDeviceInfo.Guid = Guid.Empty;
            }
            else
            {
                byte[] array = new byte[16];
                Marshal.Copy(lpGuid, array, 0, 16);
                directSoundDeviceInfo.Guid = new Guid(array);
            }
            directSoundDeviceInfo.Description = Marshal.PtrToStringAnsi(lpcstrDescription);
            directSoundDeviceInfo.ModuleName = Marshal.PtrToStringAnsi(lpcstrModule);
            DirectSoundOut.devices.Add(directSoundDeviceInfo);
            return true;
        }

        public DirectSoundOut() : this(DirectSoundOut.DSDEVID_DefaultPlayback)
        {
        }

        public DirectSoundOut(Guid device) : this(device, 40)
        {
        }

        public DirectSoundOut(int latency) : this(DirectSoundOut.DSDEVID_DefaultPlayback, latency)
        {
        }

        public DirectSoundOut(Guid device, int latency)
        {
            if (device == Guid.Empty)
            {
                device = DirectSoundOut.DSDEVID_DefaultPlayback;
            }
            this.device = device;
            this.desiredLatency = latency;
            this.syncContext = SynchronizationContext.Current;
        }

        ~DirectSoundOut()
        {
            this.Dispose();
        }

        public void Play()
        {
            if (this.playbackState == PlaybackState.Stopped)
            {
                this.notifyThread = new Thread(new ThreadStart(this.PlaybackThreadFunc));
                this.notifyThread.Priority = ThreadPriority.Normal;
                this.notifyThread.IsBackground = true;
                this.notifyThread.Start();
            }
            object lockObject = this.m_LockObject;
            lock (lockObject)
            {
                this.playbackState = PlaybackState.Playing;
            }
        }

        public void Stop()
        {
            if (Monitor.TryEnter(this.m_LockObject, 50))
            {
                this.playbackState = PlaybackState.Stopped;
                Monitor.Exit(this.m_LockObject);
                return;
            }
            if (this.notifyThread != null)
            {
                this.notifyThread.Abort();
                this.notifyThread = null;
            }
        }

        public void Pause()
        {
            object lockObject = this.m_LockObject;
            lock (lockObject)
            {
                this.playbackState = PlaybackState.Paused;
            }
        }

        public long GetPosition()
        {
            if (this.playbackState != PlaybackState.Stopped)
            {
                DirectSoundOut.IDirectSoundBuffer directSoundBuffer = this.secondaryBuffer;
                if (directSoundBuffer != null)
                {
                    uint num;
                    uint num2;
                    directSoundBuffer.GetCurrentPosition(out num, out num2);
                    return (long)((ulong)num + (ulong)this.bytesPlayed);
                }
            }
            return 0L;
        }

        public void Init(IWaveProvider waveProvider)
        {
            this.waveStream = waveProvider;
            this.waveFormat = waveProvider.WaveFormat;
        }

        private void InitializeDirectSound()
        {
            object lockObject = this.m_LockObject;
            lock (lockObject)
            {
                this.directSound = null;
                DirectSoundOut.DirectSoundCreate(ref this.device, out this.directSound, IntPtr.Zero);
                if (this.directSound != null)
                {
                    this.directSound.SetCooperativeLevel(DirectSoundOut.GetDesktopWindow(), DirectSoundOut.DirectSoundCooperativeLevel.DSSCL_PRIORITY);
                    DirectSoundOut.BufferDescription bufferDescription = new DirectSoundOut.BufferDescription();
                    bufferDescription.dwSize = Marshal.SizeOf(bufferDescription);
                    bufferDescription.dwBufferBytes = 0u;
                    bufferDescription.dwFlags = DirectSoundOut.DirectSoundBufferCaps.DSBCAPS_PRIMARYBUFFER;
                    bufferDescription.dwReserved = 0;
                    bufferDescription.lpwfxFormat = IntPtr.Zero;
                    bufferDescription.guidAlgo = Guid.Empty;
                    object obj;
                    this.directSound.CreateSoundBuffer(bufferDescription, out obj, IntPtr.Zero);
                    this.primarySoundBuffer = (DirectSoundOut.IDirectSoundBuffer)obj;
                    this.primarySoundBuffer.Play(0u, 0u, DirectSoundOut.DirectSoundPlayFlags.DSBPLAY_LOOPING);
                    this.samplesFrameSize = this.MsToBytes(this.desiredLatency);
                    DirectSoundOut.BufferDescription bufferDescription2 = new DirectSoundOut.BufferDescription();
                    bufferDescription2.dwSize = Marshal.SizeOf(bufferDescription2);
                    bufferDescription2.dwBufferBytes = (uint)(this.samplesFrameSize * 2);
                    bufferDescription2.dwFlags = (DirectSoundOut.DirectSoundBufferCaps.DSBCAPS_CTRLVOLUME | DirectSoundOut.DirectSoundBufferCaps.DSBCAPS_CTRLPOSITIONNOTIFY | DirectSoundOut.DirectSoundBufferCaps.DSBCAPS_STICKYFOCUS | DirectSoundOut.DirectSoundBufferCaps.DSBCAPS_GLOBALFOCUS | DirectSoundOut.DirectSoundBufferCaps.DSBCAPS_GETCURRENTPOSITION2);
                    bufferDescription2.dwReserved = 0;
                    GCHandle gCHandle = GCHandle.Alloc(this.waveFormat, GCHandleType.Pinned);
                    bufferDescription2.lpwfxFormat = gCHandle.AddrOfPinnedObject();
                    bufferDescription2.guidAlgo = Guid.Empty;
                    this.directSound.CreateSoundBuffer(bufferDescription2, out obj, IntPtr.Zero);
                    this.secondaryBuffer = (DirectSoundOut.IDirectSoundBuffer)obj;
                    gCHandle.Free();
                    DirectSoundOut.BufferCaps bufferCaps = new DirectSoundOut.BufferCaps();
                    bufferCaps.dwSize = Marshal.SizeOf(bufferCaps);
                    this.secondaryBuffer.GetCaps(bufferCaps);
                    this.nextSamplesWriteIndex = 0;
                    this.samplesTotalSize = bufferCaps.dwBufferBytes;
                    this.samples = new byte[this.samplesTotalSize];
                    DirectSoundOut.IDirectSoundNotify arg_26F_0 = (DirectSoundOut.IDirectSoundNotify)obj;
                    this.frameEventWaitHandle1 = new EventWaitHandle(false, EventResetMode.AutoReset);
                    this.frameEventWaitHandle2 = new EventWaitHandle(false, EventResetMode.AutoReset);
                    this.endEventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
                    DirectSoundOut.DirectSoundBufferPositionNotify[] array = new DirectSoundOut.DirectSoundBufferPositionNotify[3];
                    array[0] = default(DirectSoundOut.DirectSoundBufferPositionNotify);
                    array[0].dwOffset = 0u;
                    array[0].hEventNotify = this.frameEventWaitHandle1.SafeWaitHandle.DangerousGetHandle();
                    array[1] = default(DirectSoundOut.DirectSoundBufferPositionNotify);
                    array[1].dwOffset = (uint)this.samplesFrameSize;
                    array[1].hEventNotify = this.frameEventWaitHandle2.SafeWaitHandle.DangerousGetHandle();
                    array[2] = default(DirectSoundOut.DirectSoundBufferPositionNotify);
                    array[2].dwOffset = 4294967295u;
                    array[2].hEventNotify = this.endEventWaitHandle.SafeWaitHandle.DangerousGetHandle();
                    arg_26F_0.SetNotificationPositions(3u, array);
                }
            }
        }

        public void Dispose()
        {
            this.Stop();
            GC.SuppressFinalize(this);
        }

        private bool IsBufferLost()
        {
            return (this.secondaryBuffer.GetStatus() & DirectSoundOut.DirectSoundBufferStatus.DSBSTATUS_BUFFERLOST) != (DirectSoundOut.DirectSoundBufferStatus)0u;
        }

        private int MsToBytes(int ms)
        {
            int expr_13 = ms * (this.waveFormat.AverageBytesPerSecond / 1000);
            return expr_13 - expr_13 % this.waveFormat.BlockAlign;
        }

        private void PlaybackThreadFunc()
        {
            bool flag = false;
            bool flag2 = false;
            this.bytesPlayed = 0L;
            Exception ex = null;
            try
            {
                this.InitializeDirectSound();
                int num = 1;
                if (this.PlaybackState == PlaybackState.Stopped)
                {
                    this.secondaryBuffer.SetCurrentPosition(0u);
                    this.nextSamplesWriteIndex = 0;
                    num = this.Feed(this.samplesTotalSize);
                }
                if (num > 0)
                {
                    object lockObject = this.m_LockObject;
                    lock (lockObject)
                    {
                        this.playbackState = PlaybackState.Playing;
                    }
                    this.secondaryBuffer.Play(0u, 0u, DirectSoundOut.DirectSoundPlayFlags.DSBPLAY_LOOPING);
                    WaitHandle[] waitHandles = new WaitHandle[]
                    {
                        this.frameEventWaitHandle1,
                        this.frameEventWaitHandle2,
                        this.endEventWaitHandle
                    };
                    bool flag3 = true;
                    while (this.PlaybackState > PlaybackState.Stopped & flag3)
                    {
                        int num2 = WaitHandle.WaitAny(waitHandles, 3 * this.desiredLatency, false);
                        if (num2 == 258)
                        {
                            this.StopPlayback();
                            flag = true;
                            throw new Exception("DirectSound buffer timeout");
                        }
                        if (num2 == 2)
                        {
                            this.StopPlayback();
                            flag = true;
                            flag3 = false;
                        }
                        else
                        {
                            if (num2 == 0)
                            {
                                if (flag2)
                                {
                                    this.bytesPlayed += (long)(this.samplesFrameSize * 2);
                                }
                            }
                            else
                            {
                                flag2 = true;
                            }
                            num2 = ((num2 == 0) ? 1 : 0);
                            this.nextSamplesWriteIndex = num2 * this.samplesFrameSize;
                            if (this.Feed(this.samplesFrameSize) == 0)
                            {
                                this.StopPlayback();
                                flag = true;
                                flag3 = false;
                            }
                        }
                    }
                }
            }
            catch (Exception ex1)
            {
            }
            finally
            {
                if (!flag)
                {
                    try
                    {
                        this.StopPlayback();
                    }
                    catch (Exception ex2)
                    {
                        if (ex == null)
                        {
                            ex = ex2;
                        }
                    }
                }
                object lockObject = this.m_LockObject;
                lock (lockObject)
                {
                    this.playbackState = PlaybackState.Stopped;
                }
                this.bytesPlayed = 0L;
                this.RaisePlaybackStopped(ex);
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

        private void StopPlayback()
        {
            object lockObject = this.m_LockObject;
            lock (lockObject)
            {
                if (this.secondaryBuffer != null)
                {
                    this.secondaryBuffer.Stop();
                    this.secondaryBuffer = null;
                }
                if (this.primarySoundBuffer != null)
                {
                    this.primarySoundBuffer.Stop();
                    this.primarySoundBuffer = null;
                }
            }
        }

        private int Feed(int bytesToCopy)
        {
            int num = bytesToCopy;
            if (this.IsBufferLost())
            {
                this.secondaryBuffer.Restore();
            }
            if (this.playbackState == PlaybackState.Paused)
            {
                Array.Clear(this.samples, 0, this.samples.Length);
            }
            else
            {
                num = this.waveStream.Read(this.samples, 0, bytesToCopy);
                if (num == 0)
                {
                    Array.Clear(this.samples, 0, this.samples.Length);
                    return 0;
                }
            }
            IntPtr intPtr;
            int num2;
            IntPtr intPtr2;
            int dwAudioBytes;
            this.secondaryBuffer.Lock(this.nextSamplesWriteIndex, (uint)num, out intPtr, out num2, out intPtr2, out dwAudioBytes, DirectSoundOut.DirectSoundBufferLockFlag.None);
            if (intPtr != IntPtr.Zero)
            {
                Marshal.Copy(this.samples, 0, intPtr, num2);
                if (intPtr2 != IntPtr.Zero)
                {
                    Marshal.Copy(this.samples, 0, intPtr, num2);
                }
            }
            this.secondaryBuffer.Unlock(intPtr, num2, intPtr2, dwAudioBytes);
            return num;
        }

        [DllImport("dsound.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern void DirectSoundCreate(ref Guid GUID, [MarshalAs(UnmanagedType.Interface)] out DirectSoundOut.IDirectSound directSound, IntPtr pUnkOuter);

        [DllImport("dsound.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, EntryPoint = "DirectSoundEnumerateA", ExactSpelling = true, SetLastError = true)]
        private static extern void DirectSoundEnumerate(DirectSoundOut.DSEnumCallback lpDSEnumCallback, IntPtr lpContext);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();
    }
}
