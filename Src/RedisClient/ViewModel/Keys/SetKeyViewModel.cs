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
  

    public class SetKeyViewModel : KeyViewModel
    {
        private ObservableCollection<string> _values  ;

        public SetKeyViewModel(string key,DbNodeViewModel parent)
            :base(key,parent)
        {
       
        }

        public override KeyType KeyType
        {
            get
            {
                return KeyType.Set;
            }
        }

        private string _selectedKeyValueItem;

        public string SelectedKeyValueItem
        {
            get { return this._selectedKeyValueItem; }
            set
            {
                this._selectedKeyValueItem = value;
                this.NotifyOfPropertyChange(() => this.SelectedKeyValueItem);
                if (value == null)
                    this.EditingKeyValueItem = null;
                else
                    this.EditingKeyValueItem = value;
            }
        }

        private string _selectedEditingKeyValueItem;

        public string EditingKeyValueItem
        {
            get { return this._selectedEditingKeyValueItem; }
            set
            {
                this._selectedEditingKeyValueItem = value;
                this.NotifyOfPropertyChange(() => this.EditingKeyValueItem);
            }
        }

        public new ObservableCollection<string> KeyValue
        {
            get { return base.KeyValue as ObservableCollection<string>; }
        }

        protected override object GetKeyValue()
        {
            if (_values == null)
            {           
                var values = this.RedisClient.GetDataBase(this.DBIndex).SMemebers(this.KeyName);
                this._values = new ObservableCollection<string>(values);
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
                 
                    db.SRemove(this.KeyName, new string[] { this.SelectedKeyValueItem });
                    db.SAdd(this.KeyName, this.EditingKeyValueItem);
                    this.SelectedKeyValueItem  = this.EditingKeyValueItem ;
                    this.KeyValue.Clear();
                    this.RedisClient.GetDataBase(this.DBIndex).SMemebers(this.KeyName).ForEach(x => this.KeyValue.Add(x));

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
                    var vm = new KeyValueDialogViewModel {  IsKeyTypeVisible=false, IsKeyVisible=false}; 
                   var dr = this.WindowManager.ShowDialog(vm);
                    if (dr == false)
                        return;
                    var db = this.RedisClient.GetDataBase(this.DBIndex);
                    db.SAdd(this.KeyName,  vm.Value);
                    this.KeyValue.Add(vm.Value);
          
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
                    var dr =MessageBox.Show(Application.Current.MainWindow, "确定要删除该行吗？", "删除", MessageBoxButton.OKCancel);
                    if (dr == MessageBoxResult.Cancel)
                        return;
                    var db = this.RedisClient.GetDataBase(this.DBIndex);
                    db.SRemove(this.KeyName, new string[] { this.SelectedKeyValueItem });
                    this.KeyValue.Remove(this.SelectedKeyValueItem);
                    this.SelectedKeyValueItem = null;
                }, () => this.SelectedKeyValueItem != null));
            }
        }
    }
}
