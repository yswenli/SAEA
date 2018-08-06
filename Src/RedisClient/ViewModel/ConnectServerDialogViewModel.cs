using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caliburn.Micro;
using System.Windows.Input;

namespace RedisClient
{
    public class ConnectServerDialogViewModel : Screen
    {
        public ConnectServerDialogViewModel(string connectionName = null)
        {
            this.DisplayName = string.IsNullOrEmpty(connectionName) ? "新建连接" : string.Format("编辑连接[{0}]", connectionName);
        }

        private string _connectionName="newConn";

        public string ConnectionName
        {
            get { return this._connectionName; }
            set
            {
                this._connectionName = value;
                this.NotifyOfPropertyChange(() => this.ConnectionName);
            }
        }

        private string _address= "172.31.32.85";

        public string Address
        {
            get { return this._address; }
            set
            {
                this._address = value;
                this.NotifyOfPropertyChange(() => this.Address);
            }
        }

        private string _password= "yswenli";

        public string Password
        {
            get { return this._password; }
            set
            {
                this._password = value;
                this.NotifyOfPropertyChange(() => this.Password);
            }
        }

        private int _port=6379;

        public int Port
        {
            get { return this._port; }
            set
            {
                this._port = value;
                this.NotifyOfPropertyChange(() => this.Port);
            }
        }

        private ICommand _connectCommand;


        private bool _enableTest = true;
        public ICommand TestCommand
        {
            get
            {
                return this._connectCommand ?? (this._connectCommand = new RelayCommand(async () =>
                {
                    _enableTest = false;
                    var cnnStr = string.Format("server={0}:{1};password={2}", this.Address, this.Port, this.Password);
                    try
                    {
                        await this.ExecuteConnect(cnnStr);
                        System.Windows.MessageBox.Show("测试成功！");
                    }
                    catch(Exception ex)
                    {
                        ex.Log("TestConnectCommand");
                        System.Windows.MessageBox.Show( ex.Message ,"测试连接失败");
                    }
                    finally
                    {
                        _enableTest = true;
                        CommandManager.InvalidateRequerySuggested();
                    }


                },()=>this._enableTest));
            }
        }

        private async Task ExecuteConnect(string cnnStr)
        {
            await Task.Run(() =>
           {
               SAEA.RedisSocket.RedisClient redisClient = new SAEA.RedisSocket.RedisClient(cnnStr);
               redisClient.Connect();

           });
        }

    }
}
