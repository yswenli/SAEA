using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace RedisClient
{
    public abstract class KeyViewModel : NodeViewModel
    {
        protected KeyViewModel(string key,DbNodeViewModel parent)
        {
            if (key == null)
                throw new ArgumentNullException("key不允许为空。");
            this.Parent = parent;
            this.KeyName = key;
        }

        public DbNodeViewModel Parent { get; private set; }
        

        protected SAEA.RedisSocket.RedisClient RedisClient
        {
            get { return this.Parent.RedisClient; }
        }

        public abstract KeyType KeyType { get; }

       

        private string _keyName;
        public string KeyName
        {
            get { return this._keyName; }
            private set
            {
                this._keyName = value;
                this.NotifyOfPropertyChange(() => this.KeyName);
            }
        }

        private IEventAggregator _eventAggregator;
        protected IEventAggregator EventAggregator
        {
            get
            {
                if (this._eventAggregator == null)
                    this._eventAggregator = IoC.Get<IEventAggregator>();
                return this._eventAggregator;
            }
        }

        private IWindowManager _windowManager;

        protected IWindowManager WindowManager
        {
            get
            {
                if (this._windowManager == null)
                    this._windowManager = IoC.Get<IWindowManager>();
                return this._windowManager;
            }
        }
   

        protected int DBIndex
        {
            get { return this.Parent.Index; }
        }



        public object KeyValue
        {
            get { return this.GetKeyValue(); }
        }

        protected abstract object GetKeyValue();

        public override bool Equals(object obj)
        {
            if (!(obj is KeyViewModel))
                return false;
            
            return this.KeyName==(obj as KeyViewModel).KeyName;
        }

        public override int GetHashCode()
        {  
            return this.KeyName.GetHashCode();
        }
 
        public static KeyViewModel Create(string keyType, string keyName, DbNodeViewModel parent)
        {
            keyType = keyType.Replace("\r\n","");
            switch (keyType)
            {
                case "list":
                    return new ListKeyViewModel(keyName,parent);
                case "zset":
                    return new ZSetKeyViewModel(keyName,parent);
                case "set":
                    return new SetKeyViewModel(keyName,parent);
                case "none":
                    return null;
                case "string":
                    return new StringKeyViewModel(keyName, parent);
                case "hash":
                    return new HashKeyViewModel(keyName, parent);
            }
            return null;
        }

        public static KeyViewModel Create(KeyType keyType, string keyName, DbNodeViewModel parent)
        {
         
            switch (keyType)
            {
         
                case  KeyType.Hash:
                    return new HashKeyViewModel(keyName, parent);
                case KeyType.String:
                    return new StringKeyViewModel(keyName, parent);
              
            }
            return null;
        }

        private ICommand _openCommand;

        public ICommand OpenCommand
        {
            get
            {
                return this._openCommand ?? (this._openCommand = new RelayCommand(() =>
                {
                    this.EventAggregator.PublishOnUIThread(new KeyNodeEventArgs(this, KeyNodeEventType.Select));
                }));
            }
        }

        private ICommand _deleteCommand;

        public ICommand DeleteCommand
        {
            get
            {
                return this._deleteCommand ?? (this._deleteCommand = new RelayCommand(() =>
                {
                    var dr = MessageBox.Show(Application.Current.MainWindow, "确定要删除该实例吗？", "删除", MessageBoxButton.OKCancel);
                    if (dr == MessageBoxResult.Cancel)
                        return;
                    this.RedisClient.GetDataBase(this.DBIndex).Del(this.KeyName);
                    this.Parent.Keys.Remove(this);
                    this.EventAggregator.PublishOnUIThread(new KeyNodeEventArgs(this, KeyNodeEventType.Delete));
                }));
            }
        }

        private ICommand _renameCommand;

        public ICommand RenameCommand
        {
            get
            {
                return this._renameCommand ?? (this._renameCommand = new RelayCommand(() =>
                {
                    var wm = IoC.Get<IWindowManager>();
                    var dvm = new InputBoxViewModel { Title = "请输入新的KeyName：", Content = this.KeyName };
                    var dr=  wm.ShowDialog(dvm);
                    if (dr == false)
                        return;
                   if( this.RedisClient.GetDataBase(this.DBIndex).Rename(this.KeyName, dvm.Content))
                    {
                        MessageBox.Show("修改成功");
                        this.KeyName = dvm.Content;
                    }
                }));
            }
        }

    }

   
}
