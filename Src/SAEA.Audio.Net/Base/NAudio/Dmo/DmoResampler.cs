using SAEA.Audio.Base.NAudio.CoreAudioApi.Interfaces;
using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Dmo
{
    public class DmoResampler : IDisposable
    {
        private MediaObject mediaObject;

        private IPropertyStore propertyStoreInterface;

        private IWMResamplerProps resamplerPropsInterface;

        private ResamplerMediaComObject mediaComObject;

        public MediaObject MediaObject
        {
            get
            {
                return this.mediaObject;
            }
        }

        public DmoResampler()
        {
            this.mediaComObject = new ResamplerMediaComObject();
            this.mediaObject = new MediaObject((IMediaObject)this.mediaComObject);
            this.propertyStoreInterface = (IPropertyStore)this.mediaComObject;
            this.resamplerPropsInterface = (IWMResamplerProps)this.mediaComObject;
        }

        public void Dispose()
        {
            if (this.propertyStoreInterface != null)
            {
                Marshal.ReleaseComObject(this.propertyStoreInterface);
                this.propertyStoreInterface = null;
            }
            if (this.resamplerPropsInterface != null)
            {
                Marshal.ReleaseComObject(this.resamplerPropsInterface);
                this.resamplerPropsInterface = null;
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
