using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Caliburn.Micro;
using System.Windows.Input;

namespace RedisClient
{
    public class DbNodeViewModel:NodeViewModel
    {
        private SAEA.RedisSocket.RedisClient _client;
        private int _dbIdx;
        private int _dbSize;

        public DbNodeViewModel(int dbIdx,int dbSize, SAEA.RedisSocket.RedisClient client)
        {
            this._client = client;
            this._dbIdx = dbIdx;
            this.Keys = new ObservableCollection<RedisClient.KeyViewModel>();
            this._dbSize = dbSize;
        }

        private IWindowManager _windowManager;

        private IWindowManager WindowManager
        {
            get
            {
                if (this._windowManager == null)
                    this._windowManager = IoC.Get<IWindowManager>();
                return this._windowManager;
            }
        }

        //private async void LoadKeysAsync()
        //{
        //    this.Keys.Clear();
        //    this._client.Select(this.Index); //选中本节点索引
        //    var keys = this._client.GetDataBase().Keys();
        //    if (keys == null)
        //        return;
        //    var lst = await Task.Run(() =>
        //      {
        //          List<KeyViewModel> result = new List<RedisClient.KeyViewModel>();
        //          foreach (var key in keys)
        //          {
        //              var keyType = this._client.Type(key);

        //              var vm =KeyViewModel.Create (keyType, key,this);
        //              if (vm != null)
        //                  result.Add(vm);
        //          }
        //          return result;
        //      });

        //    lst.ForEach(x => this.Keys.Add(x));
        //}

        private async void LoadKeysAsync()
        {
            this.Keys.Clear();
            this._client.Select(this.Index); //选中本节点索引
            var keys = this._client.GetDataBase().Keys();
            if (keys == null)
                return;
            var lst = await Task.Run(() =>
            {
                List<KeyViewModel> result = new List<RedisClient.KeyViewModel>();
                foreach (var key in keys)
                {
                    var keyType = this._client.Type(key);

                    var vm = KeyViewModel.Create(keyType, key, this);
                    if (vm != null)
                        result.Add(vm);
                }
                return result;
            });

            lst.ForEach(x => this.Keys.Add(x));
        }


        public ObservableCollection<KeyViewModel> Keys { get; private set; }

    
        public int Index
        {
            get { return this._dbIdx; }
        }

        public int DBSize
        {
            get { return this._dbSize; }
        }

        public SAEA.RedisSocket.RedisClient RedisClient
        {
            get { return this._client; }
        }

       public string Name
        {
            get
            {
                return string.Format("db{0}({1})",Index,this._dbSize);
            }
        }

        private ICommand _loadKeysCommand;

        public ICommand LoadKeysCommand
        {
            get
            {
                return this._loadKeysCommand ?? (this._loadKeysCommand = new RelayCommand(() =>
                {
                    this.LoadKeysAsync();
                    this.IsExpanded = true;
                }));
            }
        }

        private ICommand _addKeyCommand;

        public ICommand AddKeyCommand
        {
            get
            {
                return this._addKeyCommand ?? (this._addKeyCommand = new RelayCommand(() =>
                {
                    var vm = new KeyValueDialogViewModel { IsKeyTypeVisible = true };
                    var dr = this.WindowManager.ShowDialog(vm);
                    if (dr == false)
                        return;
                    var db = this.RedisClient.GetDataBase(this._dbIdx);
                    switch (vm.KeyType)
                    {
                        case KeyType.String:
                            db.Set(vm.Key, vm.Value);               
                            break;
                        case KeyType.Hash:
                            db.HSet(vm.Key, vm.SubKey, vm.Value);                       
                            break;
                        case KeyType.Set:
                            db.SAdd(vm.Key, vm.Value);
                            break;
                        case KeyType.ZSet:
                            db.ZAdd(vm.Key, vm.Value, double.Parse(vm.SubKey));
                            break;
                        case KeyType.List:
                            db.LPush(vm.Key,  vm.Value);
                            break;
                    }
                    this.LoadKeysAsync();
            
                }));
            }
        }

    }
    
}
