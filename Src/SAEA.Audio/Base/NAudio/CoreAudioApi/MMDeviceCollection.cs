using SAEA.Audio.Base.NAudio.CoreAudioApi.Interfaces;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.CoreAudioApi
{
    public class MMDeviceCollection : IEnumerable<MMDevice>, IEnumerable
    {
        private IMMDeviceCollection _MMDeviceCollection;

        public int Count
        {
            get
            {
                int result;
                Marshal.ThrowExceptionForHR(this._MMDeviceCollection.GetCount(out result));
                return result;
            }
        }

        public MMDevice this[int index]
        {
            get
            {
                IMMDevice realDevice;
                this._MMDeviceCollection.Item(index, out realDevice);
                return new MMDevice(realDevice);
            }
        }

        internal MMDeviceCollection(IMMDeviceCollection parent)
        {
            this._MMDeviceCollection = parent;
        }

        public IEnumerator<MMDevice> GetEnumerator()
        {
            int num;
            for (int i = 0; i < this.Count; i = num + 1)
            {
                yield return this[i];
                num = i;
            }
            yield break;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
