using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using Caliburn.Micro;

namespace RedisClient
{
    [Export(typeof(RedisClientDetailViewModel))]
    public  class RedisClientDetailViewModel :PageViewModel,IHandle<RedisClientDetailEventArgs>
    {
        [ImportingConstructor]
        public RedisClientDetailViewModel(IEventAggregator eventAggregator)
        {
            eventAggregator.Subscribe(this);

        }

      

        private string _connectDetail;
   
        public string ClientInfo
        {
            get
            {
                return this._connectDetail;
            }
           private  set
            {
                this._connectDetail = value;
                this.NotifyOfPropertyChange(() => this.ClientInfo);
            }
        }

  
        void IHandle<RedisClientDetailEventArgs>.Handle(RedisClientDetailEventArgs arg)
        {
            if (!arg.Client.IsConnected)
            {
                System.Windows.MessageBox.Show(System.Windows.Application.Current.MainWindow,"指定的Redis库未连接。");
                return;
            }
            this.ClientInfo = arg.Client.Info();
            this.Title = arg.Client.RedisConfig?.IP;
            this.IsVisible = true;
        }
    }
}
