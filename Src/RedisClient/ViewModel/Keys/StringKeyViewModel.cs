using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace RedisClient
{
  

    public class StringKeyViewModel : KeyViewModel
    {
        private string _value;
        public StringKeyViewModel(string key,DbNodeViewModel parent)
            :base(key,parent)
        {
            this._value = this.RedisClient.GetDataBase(this.DBIndex).Get(this.KeyName);
        }

        public override KeyType KeyType
        {
            get
            {
                return KeyType.String;
            }
        }

        public new string KeyValue
        {
            get { return base.KeyValue as string; }
            set
            {
                this._value = value;
                this.NotifyOfPropertyChange(() => this.KeyValue);
            }
        }

        protected override object GetKeyValue()
        {
            return this._value;
        }

        private ICommand _updateCommand;

        public ICommand UpdateCommand
        {
            get
            {
                return this._updateCommand ?? (this._updateCommand = new RelayCommand(() =>
                {
                    var db = this.RedisClient.GetDataBase(this.DBIndex);
                    db.Del(this.KeyName );
                    db.Set(this.KeyName, this.KeyValue);
 
                } ));
            }
        }
    }
}
