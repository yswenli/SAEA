using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caliburn.Micro;

namespace RedisClient
{
    public class KeyValueDialogViewModel:Screen
    {
        public KeyValueDialogViewModel()
        {
            this.DisplayName = "设置键";
        }

        private string _key ;
 
        public string Key
        {
            get { return this._key; }
            set
            {
                this._key = value;
                this.NotifyOfPropertyChange(() => this.Key);
            }
        }

        private string _subKey;

        public string SubKey
        {
            get { return this._subKey; }
            set
            {
                this._subKey = value;
                this.NotifyOfPropertyChange(() => this.SubKey);
            }
        }

        private string _value;

        public  string Value
        {
            get { return this._value; }
            set
            {
                this._value = value;
                this.NotifyOfPropertyChange(() => this.Value);
            }
        }

        private bool _isKeyVisible=true;

        public bool IsKeyVisible
        {
            get { return this._isKeyVisible; }
            set
            {
                this._isKeyVisible = value;
                this.NotifyOfPropertyChange(() => this.IsKeyVisible);
            }
        }

        private KeyType _keyType;

        public bool IsKeyTypeVisible { get; set; }

        public KeyType KeyType
        {
            get { return this._keyType; }
            set
            {
                this._keyType = value;
                this.NotifyOfPropertyChange(() => this.KeyType);
            }
        }

        public IEnumerable<KeyType> KeyTypes
        {
            get
            {
                yield return KeyType.String;
                yield return KeyType.Hash;
                yield return KeyType.List;
                yield return KeyType.Set;
                yield return KeyType.ZSet;
            }
        }

    }
}
