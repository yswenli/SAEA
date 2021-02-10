using SAEA.Audio.NAudio.Utils;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.Wave
{
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public class WaveFormat
    {
        protected WaveFormatEncoding waveFormatTag;

        protected short channels;

        protected int sampleRate;

        protected int averageBytesPerSecond;

        protected short blockAlign;

        protected short bitsPerSample;

        protected short extraSize;

        public WaveFormatEncoding Encoding
        {
            get
            {
                return this.waveFormatTag;
            }
        }

        public int Channels
        {
            get
            {
                return (int)this.channels;
            }
        }

        public int SampleRate
        {
            get
            {
                return this.sampleRate;
            }
        }

        public int AverageBytesPerSecond
        {
            get
            {
                return this.averageBytesPerSecond;
            }
        }

        public virtual int BlockAlign
        {
            get
            {
                return (int)this.blockAlign;
            }
        }

        public int BitsPerSample
        {
            get
            {
                return (int)this.bitsPerSample;
            }
        }

        public int ExtraSize
        {
            get
            {
                return (int)this.extraSize;
            }
        }

        public WaveFormat() : this(44100, 16, 2)
        {
        }

        public WaveFormat(int sampleRate, int channels) : this(sampleRate, 16, channels)
        {
        }

        public int ConvertLatencyToByteSize(int milliseconds)
        {
            int num = (int)((double)this.AverageBytesPerSecond / 1000.0 * (double)milliseconds);
            if (num % this.BlockAlign != 0)
            {
                num = num + this.BlockAlign - num % this.BlockAlign;
            }
            return num;
        }

        public static WaveFormat CreateCustomFormat(WaveFormatEncoding tag, int sampleRate, int channels, int averageBytesPerSecond, int blockAlign, int bitsPerSample)
        {
            return new WaveFormat
            {
                waveFormatTag = tag,
                channels = (short)channels,
                sampleRate = sampleRate,
                averageBytesPerSecond = averageBytesPerSecond,
                blockAlign = (short)blockAlign,
                bitsPerSample = (short)bitsPerSample,
                extraSize = 0
            };
        }

        public static WaveFormat CreateALawFormat(int sampleRate, int channels)
        {
            return WaveFormat.CreateCustomFormat(WaveFormatEncoding.ALaw, sampleRate, channels, sampleRate * channels, channels, 8);
        }

        public static WaveFormat CreateMuLawFormat(int sampleRate, int channels)
        {
            return WaveFormat.CreateCustomFormat(WaveFormatEncoding.MuLaw, sampleRate, channels, sampleRate * channels, channels, 8);
        }

        public WaveFormat(int rate, int bits, int channels)
        {
            if (channels < 1)
            {
                throw new ArgumentOutOfRangeException("channels", "Channels must be 1 or greater");
            }
            this.waveFormatTag = WaveFormatEncoding.Pcm;
            this.channels = (short)channels;
            this.sampleRate = rate;
            this.bitsPerSample = (short)bits;
            this.extraSize = 0;
            this.blockAlign = (short)(channels * (bits / 8));
            this.averageBytesPerSecond = this.sampleRate * (int)this.blockAlign;
        }

        public static WaveFormat CreateIeeeFloatWaveFormat(int sampleRate, int channels)
        {
            WaveFormat waveFormat = new WaveFormat();
            waveFormat.waveFormatTag = WaveFormatEncoding.IeeeFloat;
            waveFormat.channels = (short)channels;
            waveFormat.bitsPerSample = 32;
            waveFormat.sampleRate = sampleRate;
            waveFormat.blockAlign = (short)(4 * channels);
            waveFormat.averageBytesPerSecond = sampleRate * (int)waveFormat.blockAlign;
            waveFormat.extraSize = 0;
            return waveFormat;
        }

        public static WaveFormat MarshalFromPtr(IntPtr pointer)
        {
            WaveFormat waveFormat = MarshalHelpers.PtrToStructure<WaveFormat>(pointer);
            WaveFormatEncoding encoding = waveFormat.Encoding;
            if (encoding <= WaveFormatEncoding.Adpcm)
            {
                if (encoding == WaveFormatEncoding.Pcm)
                {
                    waveFormat.extraSize = 0;
                    return waveFormat;
                }
                if (encoding == WaveFormatEncoding.Adpcm)
                {
                    waveFormat = MarshalHelpers.PtrToStructure<AdpcmWaveFormat>(pointer);
                    return waveFormat;
                }
            }
            else
            {
                if (encoding == WaveFormatEncoding.Gsm610)
                {
                    waveFormat = MarshalHelpers.PtrToStructure<Gsm610WaveFormat>(pointer);
                    return waveFormat;
                }
                if (encoding == WaveFormatEncoding.Extensible)
                {
                    waveFormat = MarshalHelpers.PtrToStructure<WaveFormatExtensible>(pointer);
                    return waveFormat;
                }
            }
            if (waveFormat.ExtraSize > 0)
            {
                waveFormat = MarshalHelpers.PtrToStructure<WaveFormatExtraData>(pointer);
            }
            return waveFormat;
        }

        public static IntPtr MarshalToPtr(WaveFormat format)
        {
            IntPtr intPtr = Marshal.AllocHGlobal(Marshal.SizeOf(format));
            Marshal.StructureToPtr(format, intPtr, false);
            return intPtr;
        }

        public static WaveFormat FromFormatChunk(BinaryReader br, int formatChunkLength)
        {
            WaveFormatExtraData expr_05 = new WaveFormatExtraData();
            expr_05.ReadWaveFormat(br, formatChunkLength);
            expr_05.ReadExtraData(br);
            return expr_05;
        }

        private void ReadWaveFormat(BinaryReader br, int formatChunkLength)
        {
            if (formatChunkLength < 16)
            {
                throw new InvalidDataException("Invalid WaveFormat Structure");
            }
            this.waveFormatTag = (WaveFormatEncoding)br.ReadUInt16();
            this.channels = br.ReadInt16();
            this.sampleRate = br.ReadInt32();
            this.averageBytesPerSecond = br.ReadInt32();
            this.blockAlign = br.ReadInt16();
            this.bitsPerSample = br.ReadInt16();
            if (formatChunkLength > 16)
            {
                this.extraSize = br.ReadInt16();
                if ((int)this.extraSize != formatChunkLength - 18)
                {
                    this.extraSize = (short)(formatChunkLength - 18);
                }
            }
        }

        public WaveFormat(BinaryReader br)
        {
            int formatChunkLength = br.ReadInt32();
            this.ReadWaveFormat(br, formatChunkLength);
        }

        public override string ToString()
        {
            WaveFormatEncoding waveFormatEncoding = this.waveFormatTag;
            if (waveFormatEncoding == WaveFormatEncoding.Pcm || waveFormatEncoding == WaveFormatEncoding.Extensible)
            {
                return string.Format("{0} bit PCM: {1}kHz {2} channels", this.bitsPerSample, this.sampleRate / 1000, this.channels);
            }
            return this.waveFormatTag.ToString();
        }

        public override bool Equals(object obj)
        {
            WaveFormat waveFormat = obj as WaveFormat;
            return waveFormat != null && (this.waveFormatTag == waveFormat.waveFormatTag && this.channels == waveFormat.channels && this.sampleRate == waveFormat.sampleRate && this.averageBytesPerSecond == waveFormat.averageBytesPerSecond && this.blockAlign == waveFormat.blockAlign) && this.bitsPerSample == waveFormat.bitsPerSample;
        }

        public override int GetHashCode()
        {
            return (int)(this.waveFormatTag ^ (WaveFormatEncoding)this.channels) ^ this.sampleRate ^ this.averageBytesPerSecond ^ (int)this.blockAlign ^ (int)this.bitsPerSample;
        }

        public virtual void Serialize(BinaryWriter writer)
        {
            writer.Write((int)(18 + this.extraSize));
            writer.Write((short)this.Encoding);
            writer.Write((short)this.Channels);
            writer.Write(this.SampleRate);
            writer.Write(this.AverageBytesPerSecond);
            writer.Write((short)this.BlockAlign);
            writer.Write((short)this.BitsPerSample);
            writer.Write(this.extraSize);
        }
    }
}
