using SAEA.Audio.Base.NAudio.CoreAudioApi.Interfaces;

namespace SAEA.Audio.Base.NAudio.CoreAudioApi
{
    public class PropertyStoreProperty
    {
        private readonly PropertyKey propertyKey;

        private PropVariant propertyValue;

        public PropertyKey Key
        {
            get
            {
                return this.propertyKey;
            }
        }

        public object Value
        {
            get
            {
                return this.propertyValue.Value;
            }
        }

        internal PropertyStoreProperty(PropertyKey key, PropVariant value)
        {
            this.propertyKey = key;
            this.propertyValue = value;
        }
    }
}
