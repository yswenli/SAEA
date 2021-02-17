using SAEA.Audio.Base.NAudio.CoreAudioApi.Interfaces;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.CoreAudioApi
{
    public class PropertyStore
    {
        private readonly IPropertyStore storeInterface;

        public int Count
        {
            get
            {
                int result;
                Marshal.ThrowExceptionForHR(this.storeInterface.GetCount(out result));
                return result;
            }
        }

        public PropertyStoreProperty this[int index]
        {
            get
            {
                PropertyKey key = this.Get(index);
                PropVariant value;
                Marshal.ThrowExceptionForHR(this.storeInterface.GetValue(ref key, out value));
                return new PropertyStoreProperty(key, value);
            }
        }

        public PropertyStoreProperty this[PropertyKey key]
        {
            get
            {
                for (int i = 0; i < this.Count; i++)
                {
                    PropertyKey propertyKey = this.Get(i);
                    if (propertyKey.formatId == key.formatId && propertyKey.propertyId == key.propertyId)
                    {
                        PropVariant value;
                        Marshal.ThrowExceptionForHR(this.storeInterface.GetValue(ref propertyKey, out value));
                        return new PropertyStoreProperty(propertyKey, value);
                    }
                }
                return null;
            }
        }

        public bool Contains(PropertyKey key)
        {
            for (int i = 0; i < this.Count; i++)
            {
                PropertyKey propertyKey = this.Get(i);
                if (propertyKey.formatId == key.formatId && propertyKey.propertyId == key.propertyId)
                {
                    return true;
                }
            }
            return false;
        }

        public PropertyKey Get(int index)
        {
            PropertyKey result;
            Marshal.ThrowExceptionForHR(this.storeInterface.GetAt(index, out result));
            return result;
        }

        public PropVariant GetValue(int index)
        {
            PropertyKey propertyKey = this.Get(index);
            PropVariant result;
            Marshal.ThrowExceptionForHR(this.storeInterface.GetValue(ref propertyKey, out result));
            return result;
        }

        internal PropertyStore(IPropertyStore store)
        {
            this.storeInterface = store;
        }
    }
}
