using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using Caliburn.Micro;

namespace RedisClient
{
    [Export(typeof(KeyContainerViewModel))]
    public  class KeyContainerViewModel :PageViewModel,IHandle<KeyNodeEventArgs>
    {
        [ImportingConstructor]
        public KeyContainerViewModel(IEventAggregator eventAggregator)
        {
            eventAggregator.Subscribe(this);

        }

        
        private KeyViewModel _content;
   
        public KeyViewModel Content
        {
            get
            {
                return this._content;
            }
           private  set
            {
                this._content = value;
                this.NotifyOfPropertyChange(() => this.Content);
            }
        }

  
        

        void IHandle<KeyNodeEventArgs>.Handle(KeyNodeEventArgs arg)
        {
            if (arg.Type == KeyNodeEventType.Select)
            {
                this.Content = arg.Data;
                this.Title = string.Format("{0}::db{1}::{2}", arg.Data.Parent.RedisClient.RedisConfig.IP, arg.Data.Parent.Index, arg.Data.KeyName);
                this.IsVisible = true;
            }
            else if (arg.Type == KeyNodeEventType.Delete)
            {
                if(this.Content!=null && this.Content.Equals(arg.Data))
                {
                    this.Content = null;
                    this.IsVisible = false;
                }
            }
                
        }
    }
}
