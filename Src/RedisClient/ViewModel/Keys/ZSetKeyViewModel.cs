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


    public class ZSetKeyViewModel : KeyViewModel
    {
        private ObservableCollection<ZSetKeyItemViewModel> _values;

        public ZSetKeyViewModel(string key, DbNodeViewModel parent)
            : base(key, parent)
        {

        }

        public override KeyType KeyType
        {
            get
            {
                return KeyType.ZSet;
            }
        }

        private ZSetKeyItemViewModel _selectedKeyValueItem;

        public ZSetKeyItemViewModel SelectedKeyValueItem
        {
            get { return this._selectedKeyValueItem; }
            set
            {
                this._selectedKeyValueItem = value;
                this.NotifyOfPropertyChange(() => this.SelectedKeyValueItem);
                if (value == null)
                    this.EditingKeyValueItem = value;
                else
                    this.EditingKeyValueItem = new ZSetKeyItemViewModel { Score = value.Score, Value = value.Value };
            }
        }

        private ZSetKeyItemViewModel _editingKeyValueItem;

        public ZSetKeyItemViewModel EditingKeyValueItem
        {
            get { return this._editingKeyValueItem; }
            set
            {
                this._editingKeyValueItem = value;
                this.NotifyOfPropertyChange(() => this.EditingKeyValueItem);
            }
        }

        public new ObservableCollection<ZSetKeyItemViewModel> KeyValue
        {
            get { return base.KeyValue as ObservableCollection<ZSetKeyItemViewModel>; }
        }

        protected override object GetKeyValue()
        {
            if (_values == null)
            {
                this._values = new ObservableCollection<RedisClient.ZSetKeyItemViewModel>();
        
                 var lst = this.RedisClient.GetDataBase(this.DBIndex).ZRang(this.KeyName);
                lst.ForEach(x =>
                {
                    this._values.Add(new ZSetKeyItemViewModel(x.Score, x.Value));
                });
                   
            
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
                    if (this.SelectedKeyValueItem == null)
                        return;
                    var db = this.RedisClient.GetDataBase(this.DBIndex);
                    
                    db.ZRemove(this.KeyName, new string[] { this.SelectedKeyValueItem.Value });
                    db.ZAdd(this.KeyName, this.EditingKeyValueItem.Value, this.EditingKeyValueItem.Score);
                   
                    this.SelectedKeyValueItem.Score = this.EditingKeyValueItem.Score;
                    this.SelectedKeyValueItem.Value = this.EditingKeyValueItem.Value;
                }, () => this.EditingKeyValueItem != null));
            }
        }

        private ICommand _insertRowCommand;

        public ICommand InsertRowCommand
        {
            get
            {
                return this._insertRowCommand ?? (this._insertRowCommand = new RelayCommand(() =>
                {
                    var vm = new KeyValueDialogViewModel { IsKeyTypeVisible = false };
                    var dr = this.WindowManager.ShowDialog(vm);
                    if (dr == false)
                        return;
                    var db = this.RedisClient.GetDataBase(this.DBIndex);
                    db.ZAdd(this.KeyName, vm.Value, double.Parse( vm.Key));
                    this.KeyValue.Add(new ZSetKeyItemViewModel { Score =double.Parse( vm.Key), Value = vm.Value });

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
                    db.ZRemove(this.KeyName,new string[] { this.SelectedKeyValueItem.Value });
                    this.KeyValue.Remove(this.SelectedKeyValueItem);
                    this.SelectedKeyValueItem = null;
                }, () => this.SelectedKeyValueItem != null));
            }
        }
    }
}
