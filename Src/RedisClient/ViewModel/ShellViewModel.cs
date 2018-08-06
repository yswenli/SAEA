using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using Caliburn.Micro;
using System.ComponentModel.Composition;
using System.Windows.Input;
using System.Windows;
using Newtonsoft.Json.Serialization;
using SAEA.Common;
using System.IO;
using Newtonsoft.Json;

namespace RedisClient
{
    [Export(typeof(ShellViewModel))]
    public class ShellViewModel:Screen
    {
        private IEventAggregator _eventAggregator;
        private IWindowManager _windowManager;
        private const string CONFIG_FILE = "connecion.json";

        [ImportingConstructor]
        public ShellViewModel(PageModuleViewModel pageModule,IEventAggregator eventAggregator,IWindowManager windowManager)
        {
            this.DisplayName =string.Format( "Redis客户端工具 (Version:{0})",AssemblyHelper.Current.GetName().Version.ToString());
            this.RedisClients = new ObservableCollection<RedisClientViewModel>();
            var infos= JsonConvert.DeserializeObject<RedisClientViewModel.RedisClientConfigInfo[]>(File.ReadAllText(CONFIG_FILE));
            foreach (var info in infos)
                this.RedisClients.Add(new RedisClient.RedisClientViewModel( info,eventAggregator));
            this.PageModule = pageModule;
            this._eventAggregator = eventAggregator;
            this._windowManager = windowManager;
           
        }

        public PageModuleViewModel PageModule { get; private set; }

        public ObservableCollection<RedisClientViewModel> RedisClients { get; private set; }

        private ICommand _connectCommand;

        public ICommand ConnectCommand
        {
            get
            {
                return this._connectCommand ?? (this._connectCommand = new RelayCommand(() =>
                {
                    var dvm = new ConnectServerDialogViewModel();
                    var dr = this._windowManager.ShowDialog(dvm);
                    if (dr == false)
                        return;
                    var cnnStr = string.Format("server={0}:{1};password={2}", dvm.Address, dvm.Port, dvm.Password);
                    //    var cnnStr = "server=172.31.32.85:6379;password=yswenli";               
      
                    this.RedisClients.Add(new RedisClientViewModel(dvm.ConnectionName, cnnStr,this._eventAggregator));
                    var cnns = this.RedisClients.Select(x =>x.Config).ToArray();
                    File.WriteAllText(CONFIG_FILE, JsonConvert.SerializeObject(cnns));
                }));
            }
        }

      

    }
}
