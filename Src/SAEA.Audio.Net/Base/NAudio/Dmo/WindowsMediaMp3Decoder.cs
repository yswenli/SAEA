using SAEA.Audio.Base.NAudio.CoreAudioApi.Interfaces;
using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Dmo
{
    public class WindowsMediaMp3Decoder : IDisposable
    {
        private MediaObject mediaObject;

        private IPropertyStore propertyStoreInterface;

        private WindowsMediaMp3DecoderComObject mediaComObject;

        public MediaObject MediaObject
        {
            get
            {
                return this.mediaObject;
            }
        }

        public WindowsMediaMp3Decoder()
        {
            this.mediaComObject = new WindowsMediaMp3DecoderComObject();
            this.mediaObject = new MediaObject((IMediaObject)this.mediaComObject);
            this.propertyStoreInterface = (IPropertyStore)this.mediaComObject;
        }

        public void Dispose()
        {
            if (this.propertyStoreInterface != null)
            {
                Marshal.ReleaseComObject(this.propertyStoreInterface);
                this.propertyStoreInterface = null;
            }
            if (this.mediaObject != null)
            {
                this.mediaObject.Dispose();
                this.mediaObject = null;
            }
            if (this.mediaComObject != null)
            {
                Marshal.ReleaseComObject(this.mediaComObject);
                this.mediaComObject = null;
            }
        }
    }
}
