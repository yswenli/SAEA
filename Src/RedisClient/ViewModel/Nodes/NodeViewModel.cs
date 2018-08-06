using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caliburn.Micro;

namespace RedisClient
{
    public abstract  class NodeViewModel:PropertyChangedBase
    {
        private bool _isExpanded;

        public bool IsExpanded
        {
            get { return this._isExpanded; }
            set
            {
                this._isExpanded = value;
                this.NotifyOfPropertyChange(() => this.IsExpanded);
            }
        }
    }
}
