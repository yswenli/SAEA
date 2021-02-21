using SAEA.Audio.Base.NAudio.CoreAudioApi.Interfaces;
using SAEA.Audio.Base.NAudio.MediaFoundation;
using SAEA.Audio.Base.NAudio.Utils;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Wave
{
    public class MediaFoundationReader : WaveStream
    {
        public class MediaFoundationReaderSettings
        {
            public bool RequestFloatOutput
            {
                get;
                set;
            }

            public bool SingleReaderObject
            {
                get;
                set;
            }

            public bool RepositionInRead
            {
                get;
                set;
            }

            public MediaFoundationReaderSettings()
            {
                this.RepositionInRead = true;
            }
        }

        private WaveFormat waveFormat;

        private long length;

        private MediaFoundationReader.MediaFoundationReaderSettings settings;

        private readonly string file;

        private IMFSourceReader pReader;

        private long position;

        private byte[] decoderOutputBuffer;

        private int decoderOutputOffset;

        private int decoderOutputCount;

        private long repositionTo = -1L;

        [method: CompilerGenerated]
        [CompilerGenerated]
        public event EventHandler WaveFormatChanged;

        public override WaveFormat WaveFormat
        {
            get
            {
                return this.waveFormat;
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
                if (value < 0L)
                {
                    throw new ArgumentOutOfRangeException("value", "Position cannot be less than 0");
                }
                if (this.settings.RepositionInRead)
                {
                    this.repositionTo = value;
                    this.position = value;
                    return;
                }
                this.Reposition(value);
            }
        }

        protected MediaFoundationReader()
        {
        }

        public MediaFoundationReader(string file) : this(file, null)
        {
        }

        public MediaFoundationReader(string file, MediaFoundationReader.MediaFoundationReaderSettings settings)
        {
            this.file = file;
            this.Init(settings);
        }

        protected void Init(MediaFoundationReader.MediaFoundationReaderSettings initialSettings)
        {
            MediaFoundationApi.Startup();
            this.settings = (initialSettings ?? new MediaFoundationReader.MediaFoundationReaderSettings());
            IMFSourceReader iMFSourceReader = this.CreateReader(this.settings);
            this.waveFormat = this.GetCurrentWaveFormat(iMFSourceReader);
            iMFSourceReader.SetStreamSelection(-3, true);
            this.length = this.GetLength(iMFSourceReader);
            if (this.settings.SingleReaderObject)
            {
                this.pReader = iMFSourceReader;
            }
        }

        private WaveFormat GetCurrentWaveFormat(IMFSourceReader reader)
        {
            IMFMediaType mediaType;
            reader.GetCurrentMediaType(-3, out mediaType);
            MediaType expr_10 = new MediaType(mediaType);
            Guid arg_16_0 = expr_10.MajorType;
            Guid subType = expr_10.SubType;
            int channelCount = expr_10.ChannelCount;
            int bitsPerSample = expr_10.BitsPerSample;
            int sampleRate = expr_10.SampleRate;
            if (subType == AudioSubtypes.MFAudioFormat_PCM)
            {
                return new WaveFormat(sampleRate, bitsPerSample, channelCount);
            }
            if (subType == AudioSubtypes.MFAudioFormat_Float)
            {
                return WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channelCount);
            }
            string arg = FieldDescriptionHelper.Describe(typeof(AudioSubtypes), subType);
            throw new InvalidDataException(string.Format("Unsupported audio sub Type {0}", arg));
        }

        private static MediaType GetCurrentMediaType(IMFSourceReader reader)
        {
            IMFMediaType mediaType;
            reader.GetCurrentMediaType(-3, out mediaType);
            return new MediaType(mediaType);
        }

        protected virtual IMFSourceReader CreateReader(MediaFoundationReader.MediaFoundationReaderSettings settings)
        {
            IMFSourceReader iMFSourceReader;
            MediaFoundationInterop.MFCreateSourceReaderFromURL(this.file, null, out iMFSourceReader);
            iMFSourceReader.SetStreamSelection(-2, false);
            iMFSourceReader.SetStreamSelection(-3, true);
            MediaType mediaType = new MediaType();
            mediaType.MajorType = MediaTypes.MFMediaType_Audio;
            mediaType.SubType = (settings.RequestFloatOutput ? AudioSubtypes.MFAudioFormat_Float : AudioSubtypes.MFAudioFormat_PCM);
            MediaType currentMediaType = MediaFoundationReader.GetCurrentMediaType(iMFSourceReader);
            mediaType.ChannelCount = currentMediaType.ChannelCount;
            mediaType.SampleRate = currentMediaType.SampleRate;
            iMFSourceReader.SetCurrentMediaType(-3, IntPtr.Zero, mediaType.MediaFoundationObject);
            Marshal.ReleaseComObject(currentMediaType.MediaFoundationObject);
            return iMFSourceReader;
        }

        private long GetLength(IMFSourceReader reader)
        {
            IntPtr intPtr = Marshal.AllocHGlobal(MarshalHelpers.SizeOf<PropVariant>());
            long result;
            try
            {
                int presentationAttribute = reader.GetPresentationAttribute(-1, MediaFoundationAttributes.MF_PD_DURATION, intPtr);
                if (presentationAttribute == -1072875802)
                {
                    result = 0L;
                }
                else
                {
                    if (presentationAttribute != 0)
                    {
                        Marshal.ThrowExceptionForHR(presentationAttribute);
                    }
                    result = (long)MarshalHelpers.PtrToStructure<PropVariant>(intPtr).Value * (long)this.waveFormat.AverageBytesPerSecond / 10000000L;
                }
            }
            finally
            {
                PropVariant.Clear(intPtr);
                Marshal.FreeHGlobal(intPtr);
            }
            return result;
        }

        private void EnsureBuffer(int bytesRequired)
        {
            if (this.decoderOutputBuffer == null || this.decoderOutputBuffer.Length < bytesRequired)
            {
                this.decoderOutputBuffer = new byte[bytesRequired];
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this.pReader == null)
            {
                this.pReader = this.CreateReader(this.settings);
            }
            if (this.repositionTo != -1L)
            {
                this.Reposition(this.repositionTo);
            }
            int i = 0;
            if (this.decoderOutputCount > 0)
            {
                i += this.ReadFromDecoderBuffer(buffer, offset, count - i);
            }
            while (i < count)
            {
                int num;
                MF_SOURCE_READER_FLAG mF_SOURCE_READER_FLAG;
                ulong num2;
                IMFSample iMFSample;
                this.pReader.ReadSample(-3, 0, out num, out mF_SOURCE_READER_FLAG, out num2, out iMFSample);
                if ((mF_SOURCE_READER_FLAG & MF_SOURCE_READER_FLAG.MF_SOURCE_READERF_ENDOFSTREAM) != MF_SOURCE_READER_FLAG.None)
                {
                    break;
                }
                if ((mF_SOURCE_READER_FLAG & MF_SOURCE_READER_FLAG.MF_SOURCE_READERF_CURRENTMEDIATYPECHANGED) != MF_SOURCE_READER_FLAG.None)
                {
                    this.waveFormat = this.GetCurrentWaveFormat(this.pReader);
                    this.OnWaveFormatChanged();
                }
                else if (mF_SOURCE_READER_FLAG != MF_SOURCE_READER_FLAG.None)
                {
                    throw new InvalidOperationException(string.Format("MediaFoundationReadError {0}", mF_SOURCE_READER_FLAG));
                }
                IMFMediaBuffer iMFMediaBuffer;
                iMFSample.ConvertToContiguousBuffer(out iMFMediaBuffer);
                IntPtr source;
                int num3;
                int bytesRequired;
                iMFMediaBuffer.Lock(out source, out num3, out bytesRequired);
                this.EnsureBuffer(bytesRequired);
                Marshal.Copy(source, this.decoderOutputBuffer, 0, bytesRequired);
                this.decoderOutputOffset = 0;
                this.decoderOutputCount = bytesRequired;
                i += this.ReadFromDecoderBuffer(buffer, offset + i, count - i);
                iMFMediaBuffer.Unlock();
                Marshal.ReleaseComObject(iMFMediaBuffer);
                Marshal.ReleaseComObject(iMFSample);
            }
            this.position += (long)i;
            return i;
        }

        private int ReadFromDecoderBuffer(byte[] buffer, int offset, int needed)
        {
            int num = Math.Min(needed, this.decoderOutputCount);
            Array.Copy(this.decoderOutputBuffer, this.decoderOutputOffset, buffer, offset, num);
            this.decoderOutputOffset += num;
            this.decoderOutputCount -= num;
            if (this.decoderOutputCount == 0)
            {
                this.decoderOutputOffset = 0;
            }
            return num;
        }

        private void Reposition(long desiredPosition)
        {
            PropVariant expr_1F = PropVariant.FromLong(10000000L * this.repositionTo / (long)this.waveFormat.AverageBytesPerSecond);
            IntPtr intPtr = Marshal.AllocHGlobal(Marshal.SizeOf(expr_1F));
            Marshal.StructureToPtr(expr_1F, intPtr, false);
            this.pReader.SetCurrentPosition(Guid.Empty, intPtr);
            Marshal.FreeHGlobal(intPtr);
            this.decoderOutputCount = 0;
            this.decoderOutputOffset = 0;
            this.position = desiredPosition;
            this.repositionTo = -1L;
        }

        protected override void Dispose(bool disposing)
        {
            if (this.pReader != null)
            {
                Marshal.ReleaseComObject(this.pReader);
                this.pReader = null;
            }
            base.Dispose(disposing);
        }

        private void OnWaveFormatChanged()
        {
            EventHandler waveFormatChanged = this.WaveFormatChanged;
            if (waveFormatChanged != null)
            {
                waveFormatChanged(this, EventArgs.Empty);
            }
        }
    }
}
