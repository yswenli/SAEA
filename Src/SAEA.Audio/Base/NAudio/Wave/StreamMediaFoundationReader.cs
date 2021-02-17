using SAEA.Audio.Base.NAudio.MediaFoundation;
using System;
using System.IO;

namespace SAEA.Audio.Base.NAudio.Wave
{
    public class StreamMediaFoundationReader : MediaFoundationReader
    {
        private readonly Stream stream;

        public StreamMediaFoundationReader(Stream stream, MediaFoundationReader.MediaFoundationReaderSettings settings = null)
        {
            this.stream = stream;
            base.Init(settings);
        }

        protected override IMFSourceReader CreateReader(MediaFoundationReader.MediaFoundationReaderSettings settings)
        {
            IMFSourceReader expr_15 = MediaFoundationApi.CreateSourceReaderFromByteStream(MediaFoundationApi.CreateByteStream(new ComStream(this.stream)));
            expr_15.SetStreamSelection(-2, false);
            expr_15.SetStreamSelection(-3, true);
            expr_15.SetCurrentMediaType(-3, IntPtr.Zero, new MediaType
            {
                MajorType = MediaTypes.MFMediaType_Audio,
                SubType = settings.RequestFloatOutput ? AudioSubtypes.MFAudioFormat_Float : AudioSubtypes.MFAudioFormat_PCM
            }.MediaFoundationObject);
            return expr_15;
        }
    }
}
