using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SAEA.Common;
using System.Windows.Input;
using System.Windows;

namespace RedisClient
{
  

    public class HashKeyViewModel : KeyViewModel
    {
        private ObservableCollection<HashKeyItemViewModel> _values  ;

        public HashKeyViewModel(string key,DbNodeViewModel parent)
            :base(key,parent)
        {
       
        }

        public override KeyType KeyType
        {
            get
            {
                return KeyType.Hash;
            }
        }

        private HashKeyItemViewModel _selectedKeyValueItem;

        public HashKeyItemViewModel SelectedKeyValueItem
        {
            get { return this._selectedKeyValueItem; }
            set
            {
                this._selectedKeyValueItem = value;
                this.NotifyOfPropertyChange(() => this.SelectedKeyValueItem);
                if (value == null)
                    this.EditingKeyValueItem = value;
                else
                    this.EditingKeyValueItem = new HashKeyItemViewModel { Key = value.Key, Value = value.Value };
            }
        }

        private HashKeyItemViewModel _editingKeyValueItem;

        public HashKeyItemViewModel EditingKeyValueItem
        {
            get { return this._editingKeyValueItem; }
            set
            {
                this._editingKeyValueItem = value;
                this.NotifyOfPropertyChange(() => this.EditingKeyValueItem);
            }
        }

        public new ObservableCollection<HashKeyItemViewModel> KeyValue
        {
            get { return base.KeyValue as ObservableCollection<HashKeyItemViewModel>; }
        }

        protected override object GetKeyValue()
        {
            if (_values == null)
            {
                this._values = new ObservableCollection<RedisClient.HashKeyItemViewModel>();
                var hkeys = this.RedisClient.GetDataBase(this.DBIndex).GetHKeys(this.KeyName);
                foreach (var hkey in hkeys)
                {
                    var val = this.RedisClient.GetDataBase(this.DBIndex).HGet(this.KeyName, hkey);

                    this._values.Add(new HashKeyItemViewModel(hkey,val ));
                }
            }
            return this._values;
        }

        private ICommand _updateCommand;

        public ICommand UpdateCommand
        {
            get
            {
                return this._updateCommand ?? (this._updateCommand = new RelayCommand(() =>
                {
                    var db = this.RedisClient.GetDataBase(this.DBIndex);
                    db.HSet(this.KeyName, this.EditingKeyValueItem.Key, this.EditingKeyValueItem.Value);
                    db.HDel(this.KeyName, this.SelectedKeyValueItem.Key);
                    this.SelectedKeyValueItem.Key = this.EditingKeyValueItem.Key;
                    this.SelectedKeyValueItem.Value = this.EditingKeyValueItem.Value;
                },()=>this.EditingKeyValueItem!=null));
            }
        }

        private ICommand _insertRowCommand;

        public ICommand InsertRowCommand
        {
            get
            {
                return this._insertRowCommand ?? (this._insertRowCommand = new RelayCommand(() =>
                {
                    var vm = new KeyValueDialogViewModel {  IsKeyTypeVisible=false}; 
                   var dr = this.WindowManager.ShowDialog(vm);
                    if (dr == false)
                        return;
                    var db = this.RedisClient.GetDataBase(this.DBIndex);
                    db.HSet(this.KeyName, vm.Key, vm.Value);
                    this.KeyValue.Add(new HashKeyItemViewModel {  Key=vm.Key ,Value=vm.Value});
          
                }));
            }
        }

        private ICommand _deleteRowCommand;

        public ICommand DeleteRowCommand
        {
            get
            {
                return this._deleteRowCommand ?? (this._deleteRowCommand = new RelayCommand(() =>
                {
                    var dr = MessageBox.Show(Application.Current.MainWindow, "确定要删除该行吗？", "删除", MessageBoxButton.OKCancel);
                    if (dr == MessageBoxResult.Cancel)
                        return;
                    var db = this.RedisClient.GetDataBase(this.DBIndex);
                    db.HDel(this.KeyName, this.SelectedKeyValueItem.Key);
                    this.KeyValue.Remove(this.SelectedKeyValueItem);
                    this.SelectedKeyValueItem = null;
                }, () => this.SelectedKeyValueItem != null));
            }
        }
    }
}
