using SAEA.Audio.NAudio.Dmo;
using SAEA.Audio.NAudio.Wave;
using System;

namespace SAEA.Audio.NAudio.FileFormats.Mp3
{
    public class DmoMp3FrameDecompressor : IMp3FrameDecompressor, IDisposable
    {
        private WindowsMediaMp3Decoder mp3Decoder;

        private WaveFormat pcmFormat;

        private MediaBuffer inputMediaBuffer;

        private DmoOutputDataBuffer outputBuffer;

        private bool reposition;

        public WaveFormat OutputFormat
        {
            get
            {
                return this.pcmFormat;
            }
        }

        public DmoMp3FrameDecompressor(WaveFormat sourceFormat)
        {
            this.mp3Decoder = new WindowsMediaMp3Decoder();
            if (!this.mp3Decoder.MediaObject.SupportsInputWaveFormat(0, sourceFormat))
            {
                throw new ArgumentException("Unsupported input format");
            }
            this.mp3Decoder.MediaObject.SetInputWaveFormat(0, sourceFormat);
            this.pcmFormat = new WaveFormat(sourceFormat.SampleRate, sourceFormat.Channels);
            if (!this.mp3Decoder.MediaObject.SupportsOutputWaveFormat(0, this.pcmFormat))
            {
                throw new ArgumentException(string.Format("Unsupported output format {0}", this.pcmFormat));
            }
            this.mp3Decoder.MediaObject.SetOutputWaveFormat(0, this.pcmFormat);
            this.inputMediaBuffer = new MediaBuffer(sourceFormat.AverageBytesPerSecond);
            this.outputBuffer = new DmoOutputDataBuffer(this.pcmFormat.AverageBytesPerSecond);
        }

        public int DecompressFrame(Mp3Frame frame, byte[] dest, int destOffset)
        {
            this.inputMediaBuffer.LoadData(frame.RawData, frame.FrameLength);
            if (this.reposition)
            {
                this.mp3Decoder.MediaObject.Flush();
                this.reposition = false;
            }
            this.mp3Decoder.MediaObject.ProcessInput(0, this.inputMediaBuffer, DmoInputDataBufferFlags.None, 0L, 0L);
            this.outputBuffer.MediaBuffer.SetLength(0);
            this.outputBuffer.StatusFlags = DmoOutputDataBufferFlags.None;
            this.mp3Decoder.MediaObject.ProcessOutput(DmoProcessOutputFlags.None, 1, new DmoOutputDataBuffer[]
            {
                this.outputBuffer
            });
            if (this.outputBuffer.Length == 0)
            {
                return 0;
            }
            this.outputBuffer.RetrieveData(dest, destOffset);
            return this.outputBuffer.Length;
        }

        public void Reset()
        {
            this.reposition = true;
        }

        public void Dispose()
        {
            if (this.inputMediaBuffer != null)
            {
                this.inputMediaBuffer.Dispose();
                this.inputMediaBuffer = null;
            }
            this.outputBuffer.Dispose();
            if (this.mp3Decoder != null)
            {
                this.mp3Decoder.Dispose();
                this.mp3Decoder = null;
            }
        }
    }
}
