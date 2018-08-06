using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Input;
using Caliburn.Micro;
using SAEA.RedisSocket;
using System.Collections.ObjectModel;



namespace RedisClient
{
    public class RedisClientViewModel : NodeViewModel
    {
        public class RedisClientConfigInfo
        {
            public string ConnectionName { get; set; }
            public string ConnectionString { get; set; }
        }
        private const int MaxDBConnectCount = 50;
        private IEventAggregator _eventAggregator;
        public RedisClientViewModel(string connectionName, string connectionString, IEventAggregator eventAggregator)
        {
            this._client = new SAEA.RedisSocket.RedisClient(connectionString);
            this.Items = new ObservableCollection<RedisClient.DbNodeViewModel>();
            this._eventAggregator = eventAggregator;
            this.Config = new RedisClientConfigInfo { ConnectionString = connectionString, ConnectionName = connectionName };
            this.Name = Config.ConnectionName;
        }

        public RedisClientViewModel(RedisClientConfigInfo config, IEventAggregator eventAggregator) :
            this(config.ConnectionName, config.ConnectionString, eventAggregator)
        {
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get { return this._isConnected; }
            private set
            {
                this._isConnected = value;
                this.NotifyOfPropertyChange(() => this.IsConnected);
            }
        }

        public RedisClientConfigInfo Config { get; private set; }


        private async Task<List<Tuple<int, int>>> GetDbs()
        {
            return await Task.Run(() =>
             {
                 int idx = 0;
                 List<Tuple<int, int>> lst = new List<Tuple<int, int>>();
                 while (idx < MaxDBConnectCount)
                 {
                     var success = this._client.Select(idx);
                     if (success)
                         lst.Add(new Tuple<int, int>(idx, this._client.DBSize()));
                     if (this._client.IsCluster)
                         break;
                     else
                         idx++;
                 }

                 return lst;
             });

        }


        public string Name { get; private set; }


        public ObservableCollection<DbNodeViewModel> Items { get; private set; }

        private SAEA.RedisSocket.RedisClient _client;

        public SAEA.RedisSocket.RedisClient Raw
        {
            get { return this._client; }
        }

        private ICommand _openCommand;

        public ICommand OpenCommand
        {
            get
            {
                return this._openCommand ?? (this._openCommand = new RelayCommand(() =>
                {
                    this._eventAggregator.PublishOnUIThread(new RedisClientDetailEventArgs(this.Raw));
                }));
            }
        }

        private ICommand _connectCommand;

        public ICommand ConnectCommand
        {
            get
            {
                return this._connectCommand ?? (this._connectCommand = new RelayCommand(async () =>
                {
                    if (this.IsConnected)
                    {
                        this.IsExpanded = !this.IsExpanded;
                        return;
                    }
                    try
                    {
                        await ExecuteConnect();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(Application.Current.MainWindow, ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    this.IsExpanded = true;
                    this.IsConnected = true;
                    this.Items.Clear();
                    var lst = await this.GetDbs();
                    lst.ForEach(x =>
                    {
                        var dbNode = new DbNodeViewModel(x.Item1, x.Item2, this._client);
                        this.Items.Add(dbNode);
                    });

                }));
            }
        }

        private ICommand _refreshCommand;

        public ICommand RefreshCommand
        {
            get
            {
                return this._refreshCommand ?? (this._refreshCommand = new RelayCommand(async () =>
              {
                  this.IsConnected = false;
                  this.IsExpanded = false;
                  this.IsConnected = true;
                  this.Items.Clear();
                  var lst = await this.GetDbs();
                  lst.ForEach(x =>
                  {
                      var dbNode = new DbNodeViewModel(x.Item1, x.Item2, this._client);
                      this.Items.Add(dbNode);
                  });
                  this.IsExpanded = true;

              }, () => this.IsConnected));
            }
        }



        private async Task ExecuteConnect()
        {
            await Task.Run(() =>
            {
                this._client.Connect();
            });
        }
    }
}
